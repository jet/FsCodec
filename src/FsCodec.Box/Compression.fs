namespace FsCodec

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type [<Struct>] CompressionOptions = { minSize: int; minGain: int } with
    static member Default = { minSize = 48; minGain = 4 }
    static member Uncompressed = { minSize = Int32.MaxValue; minGain = 0 }

[<Extension; AbstractClass; Sealed; Obsolete "Please use FsCodec.Encoder instead">]
type Compression private () =

    static member Utf8ToEncodedDirect(x: ReadOnlyMemory<byte>): Encoded =
        FsCodec.Encoding.OfBlob x
    static member Utf8ToEncodedTryCompress(options, x: ReadOnlyMemory<byte>): Encoded =
        FsCodec.Encoding.OfBlobCompress({ minSize = options.minSize; minGain = options.minGain }, x)
    static member EncodedToUtf8(x: Encoded): ReadOnlyMemory<byte> =
        FsCodec.Encoding.ToBlob x
    /// NOTE if this is for use with System.Text.Encoding.UTF8.GetString, then EncodedToUtf8 >> _.Span is more efficient
    static member EncodedToByteArray(x: Encoded): byte[] =
        FsCodec.Encoding.ToBlob(x).ToArray()

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to attempt to compress the data.<br/>
    /// If sufficient compression, as defined by <c>options</c> is not achieved, the body is saved as-is.<br/>
    /// The <c>int</c> conveys a value that must be round tripped alongside the body in order for the decoding process to correctly interpret it.</summary>
    [<Extension>]
    static member EncodeTryCompress<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>, [<Optional; DefaultParameterValue null>] ?options)
        : IEventCodec<'Event, Encoded, 'Context> =
        let opts = defaultArg options CompressionOptions.Default
        let opts: FsCodec.CompressionOptions = { minSize = opts.minSize; minGain = opts.minGain }
        FsCodec.Core.EventCodec.mapBodies (fun d -> Encoding.OfBlobCompress(opts, d)) Encoding.ToBlob native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to encode as per <c>EncodeTryCompress</c>, but without attempting compression.</summary>
    [<Extension>]
    static member EncodeUncompressed<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, Encoded, 'Context> =
        Encoder.Uncompressed native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to render and/or consume from Uncompressed <c>ReadOnlyMemory&lt;byte&gt;</c>.</summary>
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native: IEventCodec<'Event, Encoded, 'Context>)
        : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        Encoder.AsBlob native

    /// <summary>Adapts an <c>IEventCodec</c> rendering to <c>int * ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to render and/or consume from Uncompressed <c>byte[]</c>.</summary>
    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native: IEventCodec<'Event, Encoded, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        Encoder.AsByteArray native
