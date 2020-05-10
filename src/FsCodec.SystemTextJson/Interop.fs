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
            member __.Encode(context, event) =
                let encoded = native.Encode(context, event)
                { new FsCodec.IEventData<_> with
                    member __.EventType = encoded.EventType
                    member __.Data = up encoded.Data
                    member __.Meta = up encoded.Meta
                    member __.EventId = encoded.EventId
                    member __.CorrelationId = encoded.CorrelationId
                    member __.CausationId = encoded.CausationId
                    member __.Timestamp = encoded.Timestamp }

            member __.TryDecode encoded =
                let mapped =
                    { new FsCodec.ITimelineEvent<_> with
                        member __.Index = encoded.Index
                        member __.IsUnfold = encoded.IsUnfold
                        member __.Context = encoded.Context
                        member __.EventType = encoded.EventType
                        member __.Data = down encoded.Data
                        member __.Meta = down encoded.Meta
                        member __.EventId = encoded.EventId
                        member __.CorrelationId = encoded.CorrelationId
                        member __.CausationId = encoded.CausationId
                        member __.Timestamp = encoded.Timestamp }
                native.TryDecode mapped }

    static member private MapFrom(x : byte[]) : JsonElement =
        if x = null then JsonElement()
        else JsonSerializer.Deserialize(System.ReadOnlySpan.op_Implicit x)
    static member private MapTo(x: JsonElement) : byte[] =
        if x.ValueKind = JsonValueKind.Undefined then null
        else JsonSerializer.SerializeToUtf8Bytes(x, InteropExtensions.NoOverEscapingOptions)
    // Avoid introduction of HTML escaping for things like quotes etc (as standard Options.Create() profile does)
    static member private NoOverEscapingOptions =
        System.Text.Json.JsonSerializerOptions(Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, JsonElement, 'Context>)
        : FsCodec.IEventCodec<'Event, byte[], 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.MapTo, InteropExtensions.MapFrom)

    [<Extension>]
    static member ToJsonElementCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, byte[], 'Context>)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.MapFrom, InteropExtensions.MapTo)
