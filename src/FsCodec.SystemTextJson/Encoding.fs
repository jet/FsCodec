namespace FsCodec.SystemTextJson

open FsCodec
open FsCodec.SystemTextJson.Interop
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json

/// Represents the body of an Event (or its Metadata), holding the encoded form of the buffer together with an enum value identifying the encoding scheme.
/// Enables the decoding side to transparently inflate the data on loading without burdening the application layer with tracking the encoding scheme used.
type Encoded = (struct(int * JsonElement))

module private Impl =

    (* Decompression logic: triggered by extension methods below at the point where the Codec's Decode retrieves the Data or Meta properties *)

    // Equinox.Cosmos / Equinox.CosmosStore Deflate logic was as below:
    // let private deflate (uncompressedBytes: byte[]) =
    //     let output = new MemoryStream()
    //     let compressor = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
    //     compressor.Write(uncompressedBytes)
    //     compressor.Flush() // Could `Close`, but not required
    //     output.ToArray()
    let private inflateTo output (compressedBytes: byte[]) =
        let input = new System.IO.MemoryStream(compressedBytes)
        let decompressor = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress, leaveOpen = true)
        decompressor.CopyTo output
    let private brotliDecompressTo output (data: byte[]) =
        let s = new System.IO.MemoryStream(data, writable = false)
        use decompressor = new System.IO.Compression.BrotliStream(s, System.IO.Compression.CompressionMode.Decompress)
        decompressor.CopyTo output
    let private unpack post alg compressedBytes =
        use output = new System.IO.MemoryStream()
        compressedBytes |> alg output
        output.ToArray() |> post
    let decode_ direct expand (struct (encoding, data: JsonElement) as x) =
        match encoding, data.ValueKind with
        | Encoding.Deflate, JsonValueKind.String -> data.GetBytesFromBase64() |> expand inflateTo
        | Encoding.Brotli,  JsonValueKind.String -> data.GetBytesFromBase64() |> expand brotliDecompressTo
        | _ -> direct data
    let decode = decode_ unbox (unpack InteropHelpers.Utf8ToJsonElement)
    let private blobToBase64StringJsonElement = Convert.ToBase64String >> JsonSerializer.SerializeToElement
    let direct (raw: JsonElement): Encoded = Encoding.Direct, raw
    let ofUtf8Encoded struct (encoding, data: ReadOnlyMemory<byte>): Encoded =
        match encoding with
        | Encoding.Deflate -> Encoding.Deflate, data.ToArray() |> blobToBase64StringJsonElement
        | Encoding.Brotli ->  Encoding.Brotli,  data.ToArray() |> blobToBase64StringJsonElement
        | _ -> Encoding.Direct, data |> InteropHelpers.Utf8ToJsonElement
    let decodeUtf8 = decode_ InteropHelpers.JsonElementToUtf8 (unpack ReadOnlyMemory<byte>)
    let toUtf8Encoded struct (encoding, data: JsonElement): FsCodec.Encoded =
        match encoding, data.ValueKind with
        | Encoding.Deflate, JsonValueKind.String -> Encoding.Deflate, data.GetBytesFromBase64() |> ReadOnlyMemory
        | Encoding.Brotli,  JsonValueKind.String -> Encoding.Brotli,  data.GetBytesFromBase64() |> ReadOnlyMemory
        | _ -> Encoding.Direct, data |> InteropHelpers.JsonElementToUtf8

    (* Conditional compression logic: triggered as storage layer pulls Data/Meta fields
       Bodies under specified minimum size, or not meeting a required compression gain are stored directly, equivalent to if compression had not been wired in *)

    let private brotliCompress (eventBody: ReadOnlyMemory<byte>): System.IO.MemoryStream =
        let output = new System.IO.MemoryStream()
        use compressor = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
        compressor.Write eventBody.Span
        compressor.Close() // NOTE Close, not Flush; we want the output fully terminated to reduce surprises when decompressing
        output
    let compress minSize minGain (raw: JsonElement): Encoded =
        let utf8: ReadOnlyMemory<byte> = InteropHelpers.JsonElementToUtf8 raw
        if utf8.Length < minSize then direct raw else

        let brotli = brotliCompress utf8
        if utf8.Length <= int brotli.Length + minGain then direct raw else
        Encoding.Brotli, brotli.ToArray() |> blobToBase64StringJsonElement
    let directUtf8 (raw: ReadOnlyMemory<byte>): Encoded = Encoding.Direct, InteropHelpers.Utf8ToJsonElement raw
    let compressUtf8 minSize minGain (utf8: ReadOnlyMemory<byte>): Encoded =
        if utf8.Length < minSize then directUtf8 utf8 else

        let brotli = brotliCompress utf8
        if utf8.Length <= int brotli.Length + minGain then directUtf8 utf8 else
        Encoding.Brotli, brotli.ToArray() |> blobToBase64StringJsonElement

[<AbstractClass; Sealed>]
type Encoding private () =

    static member OfJsonElement(x: JsonElement): Encoded =
        Impl.direct x
    static member OfJsonElementCompress(options, x: JsonElement): Encoded =
        Impl.compress options.minSize options.minGain x
    static member OfUtf8(x: ReadOnlyMemory<byte>): Encoded =
        Impl.directUtf8 x
    static member OfUtf8Compress(options, x: ReadOnlyMemory<byte>): Encoded =
        Impl.compressUtf8 options.minSize options.minGain x
    static member OfUtf8Encoded(x: FsCodec.Encoded): Encoded =
        Impl.ofUtf8Encoded x
    static member Utf8EncodedToJsonElement(x: FsCodec.Encoded): JsonElement =
        Encoding.OfUtf8Encoded x |> Encoding.ToJsonElement
    static member ByteCount((_encoding, data): Encoded) =
        data.GetRawText() |> System.Text.Encoding.UTF8.GetByteCount
    static member ByteCountExpanded(x: Encoded) =
        Impl.decode x |> _.GetRawText() |> System.Text.Encoding.UTF8.GetByteCount
    static member ToJsonElement(x: Encoded): JsonElement =
        Impl.decode x
    static member ToUtf8(x: Encoded): ReadOnlyMemory<byte> =
        Impl.decodeUtf8 x
    static member ToEncodedUtf8(x: Encoded): FsCodec.Encoded =
        Impl.toUtf8Encoded x
    static member ToStream(ms: System.IO.Stream, x: Encoded) =
        Impl.decode_ (fun el -> JsonSerializer.Serialize(ms, el)) (fun dec -> dec ms) x

[<Extension; AbstractClass; Sealed>]
type Encoder private () =

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>JsonElement</c> Event Bodies to encode as per <c>Compress</c>, but without attempting compression.</summary>
    [<Extension>]
    static member Uncompressed<'Event, 'Context>(native: IEventCodec<'Event, JsonElement, 'Context>)
        : IEventCodec<'Event, Encoded, 'Context> =
        FsCodec.Core.EventCodec.mapBodies Encoding.OfJsonElement Encoding.ToJsonElement native

    /// <summary>The body will be saved as-is under the following circumstances:<br/>
    /// - the <c>shouldCompress</c> predicate is not satisfied for the event in question.<br/>
    /// - sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> produced when <c>Encode</c>ing conveys the encoding used, and must be round tripped alongside the body as a required input of a future <c>Decode</c>.</summary>
    /// <remarks>NOTE this is intended for interoperability only; a Codec (such as <c>CodecJsonElement</c>) that encodes to <c>JsonElement</c> is strongly recommended unless you don't have a choice.</remarks>
    [<Extension>]
    static member CompressedUtf8<'Event, 'Context>(
            native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>,
            [<Optional; DefaultParameterValue null>] ?shouldCompress: Func<IEventData<ReadOnlyMemory<byte>>, bool>,
            [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, Encoded, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        let encode = shouldCompress |> function
            | None -> fun _x (d: ReadOnlyMemory<byte>) -> Encoding.OfUtf8Compress(opts, d)
            | Some predicate -> fun x d -> if predicate.Invoke x then Encoding.OfUtf8Compress(opts, d) else Encoding.OfUtf8 d
        FsCodec.Core.EventCodec.mapBodies_ encode Encoding.ToUtf8 native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>JsonElement</c> Event Bodies to attempt to compress the data.<br/>
    /// The body will be saved as-is under the following circumstances:<br/>
    /// - the <c>shouldCompress</c> predicate is not satisfied for the event in question.<br/>
    /// - sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> produced when <c>Encode</c>ing conveys the encoding used, and must be round tripped alongside the body as a required input of a future <c>Decode</c>.</summary>
    [<Extension>]
    static member Compressed<'Event, 'Context>(
            native: IEventCodec<'Event, JsonElement, 'Context>,
            [<Optional; DefaultParameterValue null>] ?shouldCompress: Func<IEventData<JsonElement>, bool>,
            [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, Encoded, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        let encode = shouldCompress |> function
            | None -> fun _x (d: JsonElement) -> Encoding.OfJsonElementCompress(opts, d)
            | Some predicate -> fun x d -> if predicate.Invoke x then Encoding.OfJsonElementCompress(opts, d) else Encoding.OfJsonElement d
        FsCodec.Core.EventCodec.mapBodies_ encode Encoding.ToJsonElement native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * JsonElement</c> Event Bodies to render and/or consume uncompressed <c>ReadOnlyMemory&lt;byte&gt;</c>.</summary>
    [<Extension>]
    static member AsUtf8<'Event, 'Context>(native: IEventCodec<'Event, Encoded, 'Context>)
        : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Core.EventCodec.mapBodies Encoding.ToUtf8 Encoding.OfUtf8 native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * JsonElement</c> Event Bodies to render and/or consume uncompressed <c>byte[]</c>.</summary>
    [<Extension>]
    static member AsUtf8ByteArray<'Event, 'Context>(native: IEventCodec<'Event, Encoded, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.mapBodies (Encoding.ToUtf8 >> _.ToArray()) Encoding.OfUtf8 native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to encode to JsonElement, with the Decode side of the roundtrip not attempting to Compress.</summary>
    [<Extension>]
    static member Utf8AsJsonElement<'Event, 'Context>(native: IEventCodec<'Event, FsCodec.Encoded, 'Context>)
        : IEventCodec<'Event, JsonElement, 'Context> =
        FsCodec.Core.EventCodec.mapBodies (Encoding.OfUtf8Encoded >> Encoding.ToJsonElement) (Encoding.OfJsonElement >> Encoding.ToEncodedUtf8) native
