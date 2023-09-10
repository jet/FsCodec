namespace FsCodec

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

module private EncodedMaybeDeflated =

    module Encoding =
        let [<Literal>] Direct = 0
        let [<Literal>] Deflate = 1
        let [<Literal>] Brotli = 2
    type Encoded = (struct (int * ReadOnlyMemory<byte>))
    let empty : Encoded = int Encoding.Direct, ReadOnlyMemory.Empty

    let private brotliDecompress (data: ReadOnlyMemory<byte>): byte[] =
        let s = new System.IO.MemoryStream(data.ToArray(), writable = false)
        use decompressor = new System.IO.Compression.BrotliStream(s, System.IO.Compression.CompressionMode.Decompress)
        use output = new System.IO.MemoryStream()
        decompressor.CopyTo(output)
        output.ToArray()

    (* EncodedBody can potentially hold compressed content, that we'll inflate on demand *)

    let private inflate (data : ReadOnlyMemory<byte>) : byte[] =
        let s = new System.IO.MemoryStream(data.ToArray(), writable = false)
        let decompressor = new System.IO.Compression.DeflateStream(s, System.IO.Compression.CompressionMode.Decompress, leaveOpen = true)
        let output = new System.IO.MemoryStream()
        decompressor.CopyTo(output)
        output.ToArray()
    let decode struct (encoding, data) : ReadOnlyMemory<byte> =
        match encoding with
        | Encoding.Deflate    -> inflate data |> ReadOnlyMemory
        | Encoding.Brotli     -> brotliDecompress data |> ReadOnlyMemory
        | Encoding.Direct | _ -> data

    (* Compression is conditional on the input meeting a minimum size, and the result meeting a required gain *)

    let private brotliCompress (eventBody: ReadOnlyMemory<byte>): System.IO.MemoryStream =
        let output = new System.IO.MemoryStream()
        use compressor = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
        compressor.Write(eventBody.Span)
        compressor.Close()
        output

    let private deflate (eventBody : ReadOnlyMemory<byte>) : System.IO.MemoryStream =
        let output = new System.IO.MemoryStream()
        let compressor = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
        compressor.Write(eventBody.Span)
        compressor.Flush()
        output
    let encodeUncompressed (raw : ReadOnlyMemory<byte>) : Encoded = 0, raw
    let encode minSize minGain (raw : ReadOnlyMemory<byte>) : Encoded =
        if raw.Length < minSize then encodeUncompressed raw
        else match brotliCompress raw with
             | tmp when raw.Length > int tmp.Length + minGain -> int Encoding.Brotli, tmp.ToArray() |> ReadOnlyMemory
             | _ -> encodeUncompressed raw

type [<Struct>] CompressionOptions = { minSize : int; minGain : int } with
    /// Attempt to compress anything possible
    // TL;DR in general it's worth compressing everything to minimize RU consumption both on insert and update
    // For DynamoStore, every time we need to calve from the tip, the RU impact of using TransactWriteItems is significant,
    // so preventing or delaying that is of critical significance
    // Empirically not much JSON below 48 bytes actually compresses - while we don't assume that, it is what is guiding the derivation of the default
    static member Default = { minSize = 48; minGain = 4 }
    /// Encode the data without attempting to compress, regardless of size
    static member Uncompressed = { minSize = Int32.MaxValue; minGain = 0 }

[<Extension; AbstractClass; Sealed>]
type Compress =

    static member Utf8ToEncodedDirect (x : ReadOnlyMemory<byte>) : struct (int * ReadOnlyMemory<byte>) =
        EncodedMaybeDeflated.encodeUncompressed x
    static member Utf8ToEncodedTryCompress options (x : ReadOnlyMemory<byte>) : struct (int * ReadOnlyMemory<byte>) =
        EncodedMaybeDeflated.encode options.minSize options.minGain x
    static member EncodedToUtf8(x) : ReadOnlyMemory<byte> =
        EncodedMaybeDeflated.decode x
    static member EncodedToByteArray(x) : byte[] =
        let u8 = EncodedMaybeDeflated.decode x in u8.ToArray()

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory<byte></c> Event Bodies to attempt to compress the UTF-8 data.<br/>
    /// If sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> conveys a flag indicating whether compression was applied.</summary>
    [<Extension>]
    static member EncodeTryCompress<'Event, 'Context>(native : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>, [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, struct (int * ReadOnlyMemory<byte>), 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        FsCodec.Core.EventCodec.Map(native, Compress.Utf8ToEncodedTryCompress opts, Func<_, _> Compress.EncodedToUtf8)

    /// Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory<byte></c> Event Bodies to encode as per <c>EncodeTryCompress</c>, but without attempting compression.<br/>
    [<Extension>]
    static member EncodeUncompressed<'Event, 'Context>(native : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, struct (int * ReadOnlyMemory<byte>), 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compress.Utf8ToEncodedDirect, Func<_, _> Compress.EncodedToUtf8)

    /// Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory<byte></c> Event Bodies to render and/or consume from Uncompressed <c>ReadOnlyMemory<byte></c>.
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native : IEventCodec<'Event, struct (int * ReadOnlyMemory<byte>), 'Context>)
        : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compress.EncodedToUtf8, Func<_, _> Compress.Utf8ToEncodedDirect)

    /// Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory<byte></c> Event Bodies to render and/or consume from Uncompressed <c>byte[]</c>.
    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native : IEventCodec<'Event, struct (int * ReadOnlyMemory<byte>), 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compress.EncodedToByteArray, Func<_, _> Compress.Utf8ToEncodedDirect)
