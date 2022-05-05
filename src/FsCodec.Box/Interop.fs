namespace FsCodec.Interop

open System.Runtime.CompilerServices
open System

[<Extension>]
type InteropExtensions =

    static member public Adapt<'From, 'To, 'Event, 'Context>
        (   native : FsCodec.IEventCodec<'Event, 'From, 'Context>,
            up : 'From -> 'To,
            down : 'To -> 'From) : FsCodec.IEventCodec<'Event, 'To, 'Context> =

        { new FsCodec.IEventCodec<'Event, 'To, 'Context> with
            member _.Encode(context, event) =
                let encoded = native.Encode(context, event)
                { new FsCodec.IEventData<_> with
                    member _.EventType = encoded.EventType
                    member _.Data = up encoded.Data
                    member _.Meta = up encoded.Meta
                    member _.EventId = encoded.EventId
                    member _.CorrelationId = encoded.CorrelationId
                    member _.CausationId = encoded.CausationId
                    member _.Timestamp = encoded.Timestamp }

            member _.TryDecode encoded =
                let mapped =
                    { new FsCodec.ITimelineEvent<_> with
                        member _.Index = encoded.Index
                        member _.IsUnfold = encoded.IsUnfold
                        member _.Context = encoded.Context
                        member _.EventType = encoded.EventType
                        member _.Data = down encoded.Data
                        member _.Meta = down encoded.Meta
                        member _.EventId = encoded.EventId
                        member _.CorrelationId = encoded.CorrelationId
                        member _.CausationId = encoded.CausationId
                        member _.Timestamp = encoded.Timestamp }
                native.TryDecode mapped }

    static member private BytesToReadOnlyMemory(x : byte[]) : ReadOnlyMemory<byte> =
        if x = null then ReadOnlyMemory.Empty
        else ReadOnlyMemory x
    static member private ReadOnlyMemoryToBytes(x : ReadOnlyMemory<byte>) : byte[] =
        if x.IsEmpty then null
        else x.ToArray()

    /// Adapt an IEventCodec that handles ReadOnlyMemory<byte> Event Bodies to instead use byte[]
    /// Ideally not used as it makes pooling problematic; only provided for interop/porting scaffolding wrt Equinox V3 and EventStore.Client etc
    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : FsCodec.IEventCodec<'Event, byte[], 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.ReadOnlyMemoryToBytes, InteropExtensions.BytesToReadOnlyMemory)
