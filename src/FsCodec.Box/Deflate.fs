namespace FsCodec

open System
open System.Runtime.CompilerServices

module private MaybeDeflatedBody =

    type Encoded = (struct (int * ReadOnlyMemory<byte>))
    let empty : Encoded = 0, ReadOnlyMemory.Empty

    (* EncodedBody can potentially hold compressed content, that we'll inflate on demand *)

    let private inflate (data : ReadOnlyMemory<byte>) : byte array =
        let s = new System.IO.MemoryStream(data.ToArray(), writable = false)
        let decompressor = new System.IO.Compression.DeflateStream(s, System.IO.Compression.CompressionMode.Decompress, leaveOpen = true)
        let output = new System.IO.MemoryStream()
        decompressor.CopyTo(output)
        output.ToArray()
    let decode struct (encoding, data) : ReadOnlyMemory<byte> =
        if encoding <> 0 then inflate data |> ReadOnlyMemory
        else data

    (* Compression is conditional on the input meeting a minimum size, and the result meeting a required gain *)

    let private deflate (eventBody : ReadOnlyMemory<byte>) : System.IO.MemoryStream =
        let output = new System.IO.MemoryStream()
        let compressor = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen = true)
        compressor.Write(eventBody.Span)
        compressor.Flush()
        output
    let private encodeUncompressed (raw : ReadOnlyMemory<byte>) : Encoded = 0, raw
    let encode minSize minGain (raw : ReadOnlyMemory<byte>) : Encoded =
        if raw.Length < minSize then encodeUncompressed raw
        else match deflate raw with
             | tmp when raw.Length > int tmp.Length + minGain -> 1, tmp.ToArray() |> ReadOnlyMemory
             | _ -> encodeUncompressed raw

type [<Struct>] CompressionOptions = { minSize : int; minGain : int } with
    /// Attempt to compress anything possible
    // TL;DR in general it's worth compressing everything to minimize RU consumption both on insert and update
    // For DynamoStore, every time we need to calve from the tip, the RU impact of using TransactWriteItems is significant,
    // so preventing or delaying that is of critical significance
    // Empirically not much JSON below 48 bytes actually compresses - while we don't assume that, it is what is guiding the derivation of the default
    static member Default = { minSize = 48; minGain = 4 }
    /// Encode the data without attempting to Deflate, regardless of size
    static member NoCompression = { minSize = Int32.MaxValue; minGain = 0 }

[<Extension>]
type DeflateHelpers =

    static member Utf8ToMaybeDeflateEncoded options (x : ReadOnlyMemory<byte>) : struct (int * ReadOnlyMemory<byte>) =
        MaybeDeflatedBody.encode options.minSize options.minGain x

    static member EncodedToUtf8(x) : ReadOnlyMemory<byte> =
        MaybeDeflatedBody.decode x

    /// Adapts an IEventCodec that's rendering to <c>ReadOnlyMemory<byte></c> Event Bodies to attempt to compress the UTF-8 data.<br/>
    [<Extension>]
    static member EncodeWithTryDeflate<'Event, 'Context>(native : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>, ?options)
        : IEventCodec<'Event, struct (int * ReadOnlyMemory<byte>), 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        FsCodec.Core.EventCodec.Map(native, DeflateHelpers.Utf8ToMaybeDeflateEncoded opts, DeflateHelpers.EncodedToUtf8)

    /// Adapts an IEventCodec that's rendering to <c>ReadOnlyMemory<byte></c> Event Bodies to attempt to compress the UTF-8 data.<br/>
    [<Extension>]
    static member EncodeWithoutCompression<'Event, 'Context>(native : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, struct (int * ReadOnlyMemory<byte>), 'Context> =
        let opts = CompressionOptions.NoCompression
        FsCodec.Core.EventCodec.Map(native, DeflateHelpers.Utf8ToMaybeDeflateEncoded opts, DeflateHelpers.EncodedToUtf8)
