namespace FsCodec

open System.Runtime.CompilerServices
open System

[<Extension; AbstractClass; Sealed>]
type ByteArray private () =

    static member BytesToReadOnlyMemory(x : byte[]) : ReadOnlyMemory<byte> =
        if x = null then ReadOnlyMemory.Empty
        else ReadOnlyMemory x

    static member ReadOnlyMemoryToBytes(x : ReadOnlyMemory<byte>) : byte[] =
        if x.IsEmpty then null
        else x.ToArray()

    /// Adapt an IEventCodec that handles ReadOnlyMemory<byte> Event Bodies to instead use byte[]
    /// Ideally not used as it makes pooling problematic; only provided for interop/porting scaffolding wrt Equinox V3 and EventStore.Client etc
    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native : IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : IEventCodec<'Event, byte[], 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> ByteArray.ReadOnlyMemoryToBytes, Func<_, _> ByteArray.BytesToReadOnlyMemory)
