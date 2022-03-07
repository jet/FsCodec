namespace FsCodec.SystemTextJson

open System.Runtime.CompilerServices
open System.Text.Json

[<Extension>]
type InteropExtensions =
    static member private Adapt<'From, 'To, 'Event, 'Context>
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

    static member private MapFrom(x : byte[]) : JsonElement =
        if x = null then JsonElement()
        else JsonSerializer.Deserialize(System.ReadOnlySpan.op_Implicit x)
    static member private MapTo(x: JsonElement) : byte[] =
        if x.ValueKind = JsonValueKind.Undefined then null
        // Avoid introduction of HTML escaping for things like quotes etc (Options.Default uses Options.Create(), which defaults to unsafeRelaxedJsonEscaping=true)
        else JsonSerializer.SerializeToUtf8Bytes(x, options = Options.Default)

    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, JsonElement, 'Context>)
        : FsCodec.IEventCodec<'Event, byte[], 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.MapTo, InteropExtensions.MapFrom)

    [<Extension>]
    static member ToJsonElementCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, byte[], 'Context>)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.MapFrom, InteropExtensions.MapTo)
