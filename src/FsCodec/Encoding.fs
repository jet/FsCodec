namespace FsCodec

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// Represents the body of an Event (or its Metadata), holding the encoded form of the buffer together with an enum value signifying the encoding scheme.
/// Enables the decoding side to transparently inflate the data on loading without burdening the application layer with tracking the encoding scheme used
type Encoded = (struct(int * ReadOnlyMemory<byte>))

module Encoding =
    let [<Literal>] Direct = 0 // Assumed for all values not listed here
    let [<Literal>] Deflate = 1 // Deprecated encoding produced by versions pre 3.0.0-rc.13; no longer produced
    let [<Literal>] Brotli = 2 // Default encoding as of 3.0.0-rc.13

module private Impl =

    (* Decompression logic: triggered by extension methods below at the point where the Codec's Decode retrieves the Data or Meta properties *)

    // In versions pre 3.0.0-rc.13, the compression was implemented as follows; NOTE: use of Flush vs Close saves space but is unconventional
    // let private deflate (eventBody: ReadOnlyMemory<byte>): System.IO.MemoryStream =
    //     let output = new System.IO.MemoryStream()
    //     let compressor = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
    //     compressor.Write(eventBody.Span)
    //     compressor.Flush() // NOTE: using Flush in lieu of close means the result is not padded, which can hinder interop
    //     output
    let private inflateTo output (data: ReadOnlyMemory<byte>) =
        let input = new System.IO.MemoryStream(data.ToArray(), writable = false)
        let decompressor = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress, leaveOpen = true)
        decompressor.CopyTo output
    let private brotliDecompressTo output (data: ReadOnlyMemory<byte>) =
        let input = new System.IO.MemoryStream(data.ToArray(), writable = false)
        use decompressor = new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress)
        decompressor.CopyTo output
    let private unpack alg compressedBytes =
        use output = new System.IO.MemoryStream()
        compressedBytes |> alg output
        output.ToArray() |> ReadOnlyMemory
    let decode struct (encoding, data): ReadOnlyMemory<byte> =
        match encoding with
        | Encoding.Deflate ->       data |> unpack inflateTo
        | Encoding.Brotli ->        data |> unpack brotliDecompressTo
        | Encoding.Direct | _ ->    data

    (* Conditional compression logic: triggered as storage layer pulls Data/Meta fields
       Bodies under specified minimum size, or not meeting a required compression gain are stored directly, equivalent to if compression had not been wired in *)

    let private brotliCompress (eventBody: ReadOnlyMemory<byte>): System.IO.MemoryStream =
        let output = new System.IO.MemoryStream()
        use compressor = new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
        compressor.Write(eventBody.Span)
        compressor.Close() // NOTE Close, not Flush; we want the output fully terminated to reduce surprises when decompressing
        output
    let encodeUncompressed (raw: ReadOnlyMemory<byte>): Encoded = Encoding.Direct, raw
    let tryCompress minSize minGain (raw: ReadOnlyMemory<byte>): Encoded =
        if raw.Length < minSize then encodeUncompressed raw
        else match brotliCompress raw with
             | tmp when raw.Length > int tmp.Length + minGain -> Encoding.Brotli, tmp.ToArray() |> ReadOnlyMemory
             | _ -> encodeUncompressed raw

type [<Struct>] CompressionOptions = { minSize: int; minGain: int } with
    /// Attempt to compress anything possible
    // TL;DR in general it's worth compressing everything to minimize RU consumption both on insert and update
    // For DynamoStore, every time we need to calve from the tip, the RU impact of using TransactWriteItems is significant,
    // so preventing or delaying that is of critical importance
    // Empirically not much JSON below 48 bytes actually compresses - while we don't assume that, it is what is guiding the derivation of the default
    static member Default = { minSize = 48; minGain = 4 }

[<AbstractClass; Sealed>]
type Encoding private () =

    static member OfBlob(x: ReadOnlyMemory<byte>): Encoded =
        Impl.encodeUncompressed x
    static member OfBlobCompress(options, x: ReadOnlyMemory<byte>): Encoded =
        Impl.tryCompress options.minSize options.minGain x
    static member ToBlob(x: Encoded): ReadOnlyMemory<byte> =
        Impl.decode x
    static member GetStringUtf8(x: Encoded): string =
        System.Text.Encoding.UTF8.GetString(Encoding.ToBlob(x).Span)
    static member ByteCount((_encoding, data): Encoded) =
        data.Length

[<Extension; AbstractClass; Sealed>]
type Encoder private () =

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to attempt to compress the data.<br/>
    /// If sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> conveys a value that must be round tripped alongside the body in order for the decoding process to correctly interpret it.</summary>
    [<Extension>]
    static member Compressed<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>, [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, Encoded, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        FsCodec.Core.EventCodec.mapBodies (fun d -> Encoding.OfBlobCompress(opts, d)) Encoding.ToBlob native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to encode as per <c>Compressed</c>, but without attempting compression.</summary>
    [<Extension>]
    static member Uncompressed<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, Encoded, 'Context> =
        FsCodec.Core.EventCodec.mapBodies Encoding.OfBlob Encoding.ToBlob native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to render and/or consume from Uncompressed <c>ReadOnlyMemory&lt;byte&gt;</c>.</summary>
    [<Extension>]
    static member AsBlob<'Event, 'Context>(native: IEventCodec<'Event, Encoded, 'Context>)
        : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Core.EventCodec.mapBodies Encoding.ToBlob Encoding.OfBlob native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to render and/or consume from Uncompressed <c>byte[]</c>.</summary>
    [<Extension>]
    static member AsByteArray<'Event, 'Context>(native: IEventCodec<'Event, Encoded, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.mapBodies (Encoding.ToBlob >> _.ToArray()) Encoding.OfBlob native
