namespace FsCodec.SystemTextJson.Interop

open FsCodec.SystemTextJson
open System.Runtime.CompilerServices
open System
open System.Text.Json

[<Extension>]
type InteropExtensions =

    static member private Utf8ToJsonElement(x : ReadOnlyMemory<byte>) : JsonElement =
        if x.IsEmpty then JsonElement()
        else JsonSerializer.Deserialize<JsonElement>(x.Span)

    static member private JsonElementToUtf8(x : JsonElement) : ReadOnlyMemory<byte> =
        if x.ValueKind = JsonValueKind.Undefined then ReadOnlyMemory.Empty
        // Avoid introduction of HTML escaping for things like quotes etc (Options.Default uses Options.Create(), which defaults to unsafeRelaxedJsonEscaping=true)
        else JsonSerializer.SerializeToUtf8Bytes(x, options = Options.Default) |> ReadOnlyMemory

    /// Adapts an IEventCodec that's rendering to <c>JsonElement</c> Event Bodies to handle <c>ReadOnlyMemory<byte></c> bodies instead.<br/>
    /// NOTE where possible, it's better to use <c>Codec</c> in preference to <c>CodecJsonElement</c> to encode directly in order to avoid this mapping process.
    [<Extension>]
    static member ToUtf8Codec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, JsonElement, 'Context>)
        : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Interop.InteropExtensions.Adapt(native, InteropExtensions.JsonElementToUtf8, InteropExtensions.Utf8ToJsonElement)

    /// Adapts an IEventCodec that's rendering to <c>ReadOnlyMemory<byte></c> Event Bodies to handle <c>JsonElement</c> bodies instead.<br/>
    /// NOTE where possible, it's better to use <c>CodecJsonElement</c> in preference to <c>Codec/c> to encode directly in order to avoid this mapping process.
    [<Extension>]
    static member ToJsonElementCodec<'Event, 'Context>(native : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context>)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =
        FsCodec.Interop.InteropExtensions.Adapt(native, InteropExtensions.Utf8ToJsonElement, InteropExtensions.JsonElementToUtf8)
