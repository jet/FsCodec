namespace FsCodec.SystemTextJson.Interop

open System
open System.Runtime.CompilerServices
open System.Text.Json

[<Extension; AbstractClass; Sealed>]
type InteropHelpers private () =

    static member Utf8ToJsonElement(x: ReadOnlyMemory<byte>): JsonElement =
        if x.IsEmpty then JsonElement()
        else JsonSerializer.Deserialize<JsonElement>(x.Span)

    static member JsonElementToUtf8(x: JsonElement): ReadOnlyMemory<byte> =
        if x.ValueKind = JsonValueKind.Undefined then ReadOnlyMemory.Empty
        // Avoid introduction of HTML escaping for things like quotes etc (Options.Default uses Options.Create(), which defaults to unsafeRelaxedJsonEscaping = true)
        else JsonSerializer.SerializeToUtf8Bytes(x, options = FsCodec.SystemTextJson.Options.Default) |> ReadOnlyMemory

    /// <summary>Adapts an IEventCodec that's rendering to <c>JsonElement</c> Event Bodies to handle <c>ReadOnlyMemory&lt;byte&gt;</c> bodies instead.<br/>
    /// NOTE where possible, it's better to use <c>Codec</c> in preference to <c>CodecJsonElement</c> to encode directly in order to avoid this mapping process.</summary>
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native: FsCodec.IEventCodec<'Event, JsonElement, 'Context>)
        : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> InteropHelpers.JsonElementToUtf8, Func<_, _> InteropHelpers.Utf8ToJsonElement)

    /// <summary>Adapts an IEventCodec that's rendering to <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies to handle <c>JsonElement</c> bodies instead.<br/>
    /// NOTE where possible, it's better to use <c>CodecJsonElement</c> in preference to <c>Codec</c> to encode directly in order to avoid this mapping process.</summary>
    [<Extension>]
    static member ToJsonElementCodec<'Event, 'Context>(native: FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =
        FsCodec.Core.EventCodec.Map(native, Func<_, _> InteropHelpers.Utf8ToJsonElement, Func<_, _> InteropHelpers.JsonElementToUtf8)
