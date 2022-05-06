namespace FsCodec.Interop

open System.Runtime.CompilerServices
open System

[<Extension>]
type InteropHelpers =

    static member BytesToReadOnlyMemory(x : byte array) : ReadOnlyMemory<byte> =
        if x = null then ReadOnlyMemory.Empty
        else ReadOnlyMemory x

    static member ReadOnlyMemoryToBytes(x : ReadOnlyMemory<byte>) : byte array =
        if x.IsEmpty then null
        else x.ToArray()

    /// Adapt an IEventCodec that handles ReadOnlyMemory<byte> Event Bodies to instead use byte array
    /// Ideally not used as it makes pooling problematic; only provided for interop/porting scaffolding wrt Equinox V3 and EventStore.Client etc
    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : FsCodec.IEventCodec<'Event, byte array, 'Context> =

        FsCodec.Core.EventCodec.Map(native, InteropHelpers.ReadOnlyMemoryToBytes, InteropHelpers.BytesToReadOnlyMemory)
