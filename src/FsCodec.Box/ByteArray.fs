namespace FsCodec

open System
open System.Runtime.CompilerServices

[<Extension; AbstractClass; Sealed>]
type ByteArray private () =

    static member BytesToReadOnlyMemory(x: byte[]): ReadOnlyMemory<byte> =
        if x = null then ReadOnlyMemory.Empty
        else ReadOnlyMemory x

    static member ReadOnlyMemoryToBytes(x: ReadOnlyMemory<byte>): byte[] =
        if x.IsEmpty then null
        else x.ToArray()

    /// <summary>Adapt an IEventCodec that handles ReadOnlyMemory&lt;byte&gt; Event Bodies to instead use <c>byte[]</c><br/>
    /// Ideally not used as it makes pooling problematic; only provided for interop/porting scaffolding wrt Equinox V3 and EventStore.Client etc</summary>
    [<Extension>]
    static member AsByteArray<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.mapBodies ByteArray.ReadOnlyMemoryToBytes ByteArray.BytesToReadOnlyMemory native

    [<Extension; Obsolete "Renamed to AsByteArray for consistency">]
    static member ToByteArrayCodec<'Event, 'Context>(native: IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.mapBodies ByteArray.ReadOnlyMemoryToBytes ByteArray.BytesToReadOnlyMemory native
