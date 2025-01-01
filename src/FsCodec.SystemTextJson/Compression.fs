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

module private EncodedMaybeCompressed =

    module Encoding =
        let [<Literal>] Direct = 0 // Assumed for all values not listed here
        let [<Literal>] Deflate = 1 // Deprecated encoding produced by Equinox.Cosmos/CosmosStore < v 4.1.0; no longer produced
        let [<Literal>] Brotli = 2 // Default encoding

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
    let expand post alg compressedBytes =
        use output = new System.IO.MemoryStream()
        compressedBytes |> alg output
        output.ToArray() |> post
    let decode direct expand struct (encoding, data: JsonElement) =
        match encoding, data.ValueKind with
        | (Encoding.Direct | Encoding.Deflate), JsonValueKind.String -> data.GetBytesFromBase64() |> expand inflateTo
        | Encoding.Brotli, JsonValueKind.String -> data.GetBytesFromBase64() |> expand brotliDecompressTo
        | _ -> data |> direct

    (* Conditional compression logic: triggered as storage layer pulls Data/Meta fields
       Bodies under specified minimum size, or not meeting a required compression gain are stored directly, equivalent to if compression had not been wired in *)

    let encodeUncompressed (raw: JsonElement): EncodedBody = Encoding.Direct, raw
    let private blobToStringElement = Convert.ToBase64String >> JsonSerializer.SerializeToElement
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
        Encoding.Brotli, brotli.ToArray() |> blobToStringElement
    let encodeUncompressedUtf8 (raw: ReadOnlyMemory<byte>): EncodedBody = Encoding.Direct, InteropHelpers.Utf8ToJsonElement raw
    let tryCompressUtf8 minSize minGain (utf8: ReadOnlyMemory<byte>): EncodedBody =
        if utf8.Length < minSize then encodeUncompressedUtf8 utf8 else

        let brotli = brotliCompress utf8
        if utf8.Length <= int brotli.Length + minGain then encodeUncompressedUtf8 utf8 else
        Encoding.Brotli, brotli.ToArray() |> blobToStringElement

type [<Struct>] CompressionOptions = { minSize: int; minGain: int } with
    /// Attempt to compress anything possible
    // TL;DR in general it's worth compressing everything to minimize RU consumption both on insert and update
    // For CosmosStore, every time we touch the tip, the RU impact of the write is significant,
    // so preventing or delaying that is of critical importance
    // Empirically not much JSON below 48 bytes actually compresses - while we don't assume that, it is what is guiding the derivation of the default
    static member Default = { minSize = 48; minGain = 4 }

[<Extension; AbstractClass; Sealed>]
type Compression private () =

    static member Direct(x: JsonElement): EncodedBody =
        EncodedMaybeCompressed.encodeUncompressed x
    static member Direct(x: ReadOnlyMemory<byte>): EncodedBody =
        EncodedMaybeCompressed.encodeUncompressedUtf8 x
    static member MaybeCompress(options, x: JsonElement): EncodedBody =
        EncodedMaybeCompressed.tryCompress options.minSize options.minGain x
    static member MaybeCompress(options, x: ReadOnlyMemory<byte>): EncodedBody =
        EncodedMaybeCompressed.tryCompressUtf8 options.minSize options.minGain x
    static member ToJsonElement(x: EncodedBody): JsonElement =
        EncodedMaybeCompressed.decode id (EncodedMaybeCompressed.expand InteropHelpers.Utf8ToJsonElement) x
    static member ToUtf8(x: EncodedBody): ReadOnlyMemory<byte> =
        EncodedMaybeCompressed.decode InteropHelpers.JsonElementToUtf8 (EncodedMaybeCompressed.expand ReadOnlyMemory<byte>) x
    static member ToByteArray(x: EncodedBody): byte[] =
        Compression.ToUtf8(x).ToArray()
    static member ExpandTo(ms: System.IO.Stream, x: EncodedBody) =
        EncodedMaybeCompressed.decode (fun el -> JsonSerializer.Serialize(ms, el)) (fun dec -> dec ms) x

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>JsonElement</c> Event Bodies to attempt to compress the data.<br/>
    /// If sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> conveys a value that must be round tripped alongside the body in order for the decoding process to correctly interpret it.</summary>
    [<Extension>]
    static member EncodeTryCompress<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>, [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        FsCodec.Core.EventCodec.Map(native, (fun x -> Compression.MaybeCompress(opts, x)), Func<_, _> Compression.ToUtf8)

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>JsonElement</c> Event Bodies to attempt to compress the data.<br/>
    /// If sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> conveys a value that must be round tripped alongside the body in order for the decoding process to correctly interpret it.</summary>
    [<Extension>]
    static member EncodeTryCompress<'Event, 'Context>(native: IEventCodec<'Event, JsonElement, 'Context>, [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        FsCodec.Core.EventCodec.Map(native, (fun x -> Compression.MaybeCompress(opts, x)), Func<_, _> Compression.ToJsonElement)

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>JsonElement</c> Event Bodies to encode as per <c>EncodeTryCompress</c>, but without attempting compression.</summary>
    [<Extension>]
    static member EncodeUncompressed<'Event, 'Context>(native: IEventCodec<'Event, JsonElement, 'Context>)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compression.Direct, Func<_, _> Compression.ToJsonElement)

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * JsonElement</c> Event Bodies to render and/or consume Uncompressed <c>ReadOnlyMemory&lt;byte&gt;</c>.</summary>
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native: IEventCodec<'Event, EncodedBody, 'Context>)
        : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compression.ToUtf8, Func<_, _> Compression.Direct)

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * JsonElement</c> Event Bodies to render and/or consume Uncompressed <c>byte[]</c>.</summary>
    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native: IEventCodec<'Event, EncodedBody, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compression.ToByteArray, Func<_, _> Compression.Direct)
