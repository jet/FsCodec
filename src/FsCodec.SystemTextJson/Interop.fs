namespace FsCodec.SystemTextJson

open System.Runtime.CompilerServices
open System
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

    static member private JsonElementToUtf8Bytes(x : JsonElement) =
        // Avoid introduction of HTML escaping for things like quotes etc (Options.Default uses Options.Create(), which defaults to unsafeRelaxedJsonEscaping=true)
        JsonSerializer.SerializeToUtf8Bytes(x, options = Options.Default)

    (* ========================
       Adapt an IEventCodec that uses JsonElement Event Bodies to instead use ReadOnlyMemory<byte>
       Ideally not necessary; using Codec instead of CodecJsonElement in the first instance is preferable where possible *)

    static member private Utf8ToJsonElement(x : ReadOnlyMemory<byte>) : JsonElement =
        if x.IsEmpty then JsonElement()
        else JsonSerializer.Deserialize<JsonElement>(x.Span)

    static member private JsonElementToUtf8(x : JsonElement) : ReadOnlyMemory<byte> =
        if x.ValueKind = JsonValueKind.Undefined then ReadOnlyMemory.Empty
        else InteropExtensions.JsonElementToUtf8Bytes x |> ReadOnlyMemory

    /// Adapts an IEventCodec that's rendering to <c>JsonElement</c> Event Bodies to handle <c>ReadOnlyMemory<byte></c> bodies instead.<br/>
    /// NOTE where possible, it's better to use <c>Codec</c> in preference to <c>CodecJsonElement</c> to encode directly in order to avoid this mapping process.
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, JsonElement, 'Context>)
        : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.JsonElementToUtf8, InteropExtensions.Utf8ToJsonElement)

    (* ========================
       Adapt an IEventCodec that handles ReadOnlyMemory<byte> Event Bodies to instead use byte[]
       Ideally not used as it makes pooling problematic; only provided for interop/porting scaffolding wrt Equinox V3 and EventStore.Client etc *)

    static member private BytesToReadOnlyMemory(x : byte[]) : ReadOnlyMemory<byte> =
        if x = null then ReadOnlyMemory.Empty
        else ReadOnlyMemory x
    static member private ReadOnlyMemoryToBytes(x : ReadOnlyMemory<byte>) : byte[] =
        if x.IsEmpty then null
        else x.ToArray()

    [<Extension>]
    static member ToByteArrayCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : FsCodec.IEventCodec<'Event, byte[], 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.ReadOnlyMemoryToBytes, InteropExtensions.BytesToReadOnlyMemory)

    (* ========================
       Shim layer to allow interop with FsCodec.NewtonsoftJson, which natively renders to byte[] *)

    static member private BytesToJsonElement(x : byte[]) : JsonElement =
        if x = null then JsonElement()
        else JsonSerializer.Deserialize<JsonElement>(ReadOnlySpan.op_Implicit x)

    static member private JsonElementToBytes(x : JsonElement) : byte[] =
        if x.ValueKind = JsonValueKind.Undefined then null
        else InteropExtensions.JsonElementToUtf8Bytes x

    /// Facilitates interop with FsCodec.NewtonsoftJson, which renders natively as byte[]
    [<Extension>]
    static member ToJsonElementCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, byte[], 'Context>)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.BytesToJsonElement, InteropExtensions.JsonElementToBytes)

    /// Adapts an IEventCodec that's rendering to <c>byte[]</c> Event Bodies to handle <c>ReadOnlyMemory<byte></c> bodies instead.<br/>
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, byte[], 'Context>)
        : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        InteropExtensions.Adapt(native, InteropExtensions.BytesToReadOnlyMemory, InteropExtensions.ReadOnlyMemoryToBytes)
