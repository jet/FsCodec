namespace FsCodec.SystemTextJson

open FsCodec
open FsCodec.SystemTextJson.Interop
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json

/// Represents the body of an Event (or its Metadata), holding the encoded form of the buffer together with an enum value identifying the encoding scheme.
/// Enables the decoding side to transparently inflate the data on loading without burdening the application layer with tracking the encoding scheme used.
type EncodedBody = (struct(int * JsonElement))

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
    let decode_ direct expand struct (encoding, data: JsonElement) =
        match encoding, data.ValueKind with
        | Encoding.Deflate, JsonValueKind.String -> data.GetBytesFromBase64() |> expand inflateTo
        | Encoding.Brotli, JsonValueKind.String -> data.GetBytesFromBase64() |> expand brotliDecompressTo
        | _ -> data |> direct
    let decode = decode_ id (unpack InteropHelpers.Utf8ToJsonElement)
    let decodeUtf8 = decode_ InteropHelpers.JsonElementToUtf8 (unpack ReadOnlyMemory<byte>)

    (* Conditional compression logic: triggered as storage layer pulls Data/Meta fields
       Bodies under specified minimum size, or not meeting a required compression gain are stored directly, equivalent to if compression had not been wired in *)

    let encodeUncompressed (raw: JsonElement): EncodedBody = Encoding.Direct, raw
    let private blobToBase64StringJsonElement = Convert.ToBase64String >> JsonSerializer.SerializeToElement
    let private brotliCompress (eventBody: ReadOnlyMemory<byte>): System.IO.MemoryStream =
        let output = new System.IO.MemoryStream()
        use compressor = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
        compressor.Write eventBody.Span
        compressor.Close() // NOTE Close, not Flush; we want the output fully terminated to reduce surprises when decompressing
        output
    let tryCompress minSize minGain (raw: JsonElement): EncodedBody =
        let utf8: ReadOnlyMemory<byte> = InteropHelpers.JsonElementToUtf8 raw
        if utf8.Length < minSize then encodeUncompressed raw else

        let brotli = brotliCompress utf8
        if utf8.Length <= int brotli.Length + minGain then encodeUncompressed raw else
        Encoding.Brotli, brotli.ToArray() |> blobToBase64StringJsonElement
    let encodeUncompressedUtf8 (raw: ReadOnlyMemory<byte>): EncodedBody = Encoding.Direct, InteropHelpers.Utf8ToJsonElement raw
    let tryCompressUtf8 minSize minGain (utf8: ReadOnlyMemory<byte>): EncodedBody =
        if utf8.Length < minSize then encodeUncompressedUtf8 utf8 else

        let brotli = brotliCompress utf8
        if utf8.Length <= int brotli.Length + minGain then encodeUncompressedUtf8 utf8 else
        Encoding.Brotli, brotli.ToArray() |> blobToBase64StringJsonElement

[<Extension; AbstractClass; Sealed>]
type Encoding private () =

    static member FromJsonElement(x: JsonElement): EncodedBody =
        Impl.encodeUncompressed x
    static member FromUtf8(x: ReadOnlyMemory<byte>): EncodedBody =
        Impl.encodeUncompressedUtf8 x
    static member FromJsonElementTryCompress(options, x: JsonElement): EncodedBody =
        Impl.tryCompress options.minSize options.minGain x
    static member FromUtf8TryCompress(options, x: ReadOnlyMemory<byte>): EncodedBody =
        Impl.tryCompressUtf8 options.minSize options.minGain x
    static member DecodeToJsonElement(x: EncodedBody): JsonElement =
        Impl.decode x
    static member DecodeToUtf8(x: EncodedBody): ReadOnlyMemory<byte> =
        Impl.decodeUtf8 x
    static member ExpandTo(ms: System.IO.Stream, x: EncodedBody) =
        Impl.decode_ (fun el -> JsonSerializer.Serialize(ms, el)) (fun dec -> dec ms) x

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>JsonElement</c> Event Bodies to encode as per <c>EncodeTryCompress</c>, but without attempting compression.</summary>
    [<Extension>]
    static member EncodeUncompressed<'Event, 'Context>(native: IEventCodec<'Event, JsonElement, 'Context>)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        FsCodec.Core.EventCodec.mapBodies Encoding.FromJsonElement Encoding.DecodeToJsonElement native

    /// <summary>The body will be saved as-is under the following circumstances:<br/>
    /// - the <c>shouldCompress</c> predicate is not satisfied for the event in question.<br/>
    /// - sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> produced when <c>Encode</c>ing conveys the encoding used, and must be round tripped alongside the body as a required input of a future <c>Decode</c>.</summary>
    /// <remarks>NOTE this is intended for interoperability only; a Codec (such as <c>CodecJsonElement</c>) that encodes to <c>JsonElement</c> is strongly recommended unless you don't have a choice.</remarks>
    [<Extension>]
    static member EncodeUtf8TryCompress<'Event, 'Context>(
            native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>,
            [<Optional; DefaultParameterValue null>] ?shouldCompress: Func<IEventData<ReadOnlyMemory<byte>>, bool>,
            [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        let encode = shouldCompress |> function
            | None -> fun _x (d: ReadOnlyMemory<byte>) -> Encoding.FromUtf8TryCompress(opts, d)
            | Some predicate -> fun x d -> if predicate.Invoke x then Encoding.FromUtf8TryCompress(opts, d) else Encoding.FromUtf8 d
        FsCodec.Core.EventCodec.mapBodies_ encode Encoding.DecodeToUtf8 native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>JsonElement</c> Event Bodies to attempt to compress the data.<br/>
    /// The body will be saved as-is under the following circumstances:<br/>
    /// - the <c>shouldCompress</c> predicate is not satisfied for the event in question.<br/>
    /// - sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> produced when <c>Encode</c>ing conveys the encoding used, and must be round tripped alongside the body as a required input of a future <c>Decode</c>.</summary>
    [<Extension>]
    static member EncodeTryCompress<'Event, 'Context>(
            native: IEventCodec<'Event, JsonElement, 'Context>,
            [<Optional; DefaultParameterValue null>] ?shouldCompress: Func<IEventData<JsonElement>, bool>,
            [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        let encode = shouldCompress |> function
            | None -> fun _x (d: JsonElement) -> Encoding.FromJsonElementTryCompress(opts, d)
            | Some predicate -> fun x d -> if predicate.Invoke x then Encoding.FromJsonElementTryCompress(opts, d) else Encoding.FromJsonElement d
        FsCodec.Core.EventCodec.mapBodies_ encode  Encoding.DecodeToJsonElement native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * JsonElement</c> Event Bodies to render and/or consume Uncompressed <c>ReadOnlyMemory&lt;byte&gt;</c>.</summary>
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native: IEventCodec<'Event, EncodedBody, 'Context>)
        : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Core.EventCodec.mapBodies Encoding.DecodeToUtf8 Encoding.FromUtf8 native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * JsonElement</c> Event Bodies to render and/or consume Uncompressed <c>byte[]</c>.</summary>
    [<Extension>]
    static member ToUtf8ArrayCodec<'Event, 'Context>(native: IEventCodec<'Event, EncodedBody, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.mapBodies (Encoding.DecodeToUtf8 >> _.ToArray()) Encoding.FromUtf8 native
