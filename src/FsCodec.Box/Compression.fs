namespace FsCodec

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// Represents the body of an Event (or its Metadata), holding the encoded form of the buffer together with an enum value signifying the encoding scheme.
/// Enables the decoding side to transparently inflate the data on loading without burdening the application layer with tracking the encoding scheme used
type EncodedBody = (struct(int * ReadOnlyMemory<byte>))

module private EncodedMaybeCompressed =

    module Encoding =
        let [<Literal>] Direct = 0 // Assumed for all values not listed here
        let [<Literal>] Deflate = 1 // Deprecated encoding produced by versions pre 3.0.0-rc.13; no longer produced
        let [<Literal>] Brotli = 2 // Default encoding as of 3.0.0-rc.13

    (* Decompression logic: triggered by extension methods below at the point where the Codec's TryDecode retrieves the Data or Meta properties *)

    // In versions pre 3.0.0-rc.13, the compression was implemented as follows; NOTE: use of Flush vs Close saves space but is unconventional
    // let private deflate (eventBody: ReadOnlyMemory<byte>): System.IO.MemoryStream =
    //     let output = new System.IO.MemoryStream()
    //     let compressor = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
    //     compressor.Write(eventBody.Span)
    //     compressor.Flush() // NOTE: using Flush in lieu of close means the result is not padded, which can hinder interop
    //     output
    let private inflate (data: ReadOnlyMemory<byte>): byte[] =
        let s = new System.IO.MemoryStream(data.ToArray(), writable = false)
        let decompressor = new System.IO.Compression.DeflateStream(s, System.IO.Compression.CompressionMode.Decompress, leaveOpen = true)
        let output = new System.IO.MemoryStream()
        decompressor.CopyTo(output)
        output.ToArray()
    let private brotliDecompress (data: ReadOnlyMemory<byte>): byte[] =
        let s = new System.IO.MemoryStream(data.ToArray(), writable = false)
        use decompressor = new System.IO.Compression.BrotliStream(s, System.IO.Compression.CompressionMode.Decompress)
        use output = new System.IO.MemoryStream()
        decompressor.CopyTo(output)
        output.ToArray()
    let decode struct (encoding, data): ReadOnlyMemory<byte> =
        match encoding with
        | Encoding.Deflate    -> inflate data |> ReadOnlyMemory
        | Encoding.Brotli     -> brotliDecompress data |> ReadOnlyMemory
        | Encoding.Direct | _ -> data

    (* Conditional compression logic: triggered as storage layer pulls Data/Meta fields
       Bodies under specified minimum size, or not meeting a required compression gain are stored directly, equivalent to if compression had not been wired in *)

    let private brotliCompress (eventBody: ReadOnlyMemory<byte>): System.IO.MemoryStream =
        let output = new System.IO.MemoryStream()
        use compressor = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
        compressor.Write(eventBody.Span)
        compressor.Close() // NOTE Close, not Flush; we want the output fully terminated to reduce surprises when decompressing
        output
    let encodeUncompressed (raw: ReadOnlyMemory<byte>): EncodedBody = Encoding.Direct, raw
    let encode minSize minGain (raw: ReadOnlyMemory<byte>): EncodedBody =
        if raw.Length < minSize then encodeUncompressed raw
        else match brotliCompress raw with
             | tmp when raw.Length > int tmp.Length + minGain -> Encoding.Brotli, tmp.ToArray() |> ReadOnlyMemory
             | _ -> encodeUncompressed raw

type [<Struct>] CompressionOptions = { minSize: int; minGain: int } with
    /// Attempt to compress anything possible
    // TL;DR in general it's worth compressing everything to minimize RU consumption both on insert and update
    // For DynamoStore, every time we need to calve from the tip, the RU impact of using TransactWriteItems is significant,
    // so preventing or delaying that is of critical significance
    // Empirically not much JSON below 48 bytes actually compresses - while we don't assume that, it is what is guiding the derivation of the default
    static member Default = { minSize = 48; minGain = 4 }
    /// Encode the data without attempting to compress, regardless of size
    static member Uncompressed = { minSize = Int32.MaxValue; minGain = 0 }

[<Extension; AbstractClass; Sealed>]
type Compression =

    static member Utf8ToEncodedDirect(x: ReadOnlyMemory<byte>): EncodedBody =
        EncodedMaybeCompressed.encodeUncompressed x
    static member Utf8ToEncodedTryCompress(options, x: ReadOnlyMemory<byte>): EncodedBody =
        EncodedMaybeCompressed.encode options.minSize options.minGain x
    static member EncodedToUtf8(x: EncodedBody): ReadOnlyMemory<byte> =
        EncodedMaybeCompressed.decode x
    static member EncodedToByteArray(x: EncodedBody): byte[] =
        Compression.EncodedToUtf8(x).ToArray()

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to attempt to compress the data.<br/>
    /// If sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> conveys a value that must be round tripped alongside the body in order for the decoding process to correctly interpret it.</summary>
    [<Extension>]
    static member EncodeTryCompress<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>, [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        FsCodec.Core.EventCodec.Map(native, (fun x -> Compression.Utf8ToEncodedTryCompress(opts, x)), Func<_, _> Compression.EncodedToUtf8)

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to encode as per <c>EncodeTryCompress</c>, but without attempting compression.</summary>
    [<Extension>]
    static member EncodeUncompressed<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, EncodedBody, 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compression.Utf8ToEncodedDirect, Func<_, _> Compression.EncodedToUtf8)

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory&lt;byte&rt;</c> Event Bodies to render and/or consume from Uncompressed <c>ReadOnlyMemory&lt;byte&gt;</c>.</summary>
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native: IEventCodec<'Event, EncodedBody, 'Context>)
        : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compression.EncodedToUtf8, Func<_, _> Compression.Utf8ToEncodedDirect)

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to render and/or consume from Uncompressed <c>byte[]</c>.</summary>
    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native: IEventCodec<'Event, EncodedBody, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> Compression.EncodedToByteArray, Func<_, _> Compression.Utf8ToEncodedDirect)
