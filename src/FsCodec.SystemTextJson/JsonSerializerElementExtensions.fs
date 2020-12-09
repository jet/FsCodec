namespace FsCodec.SystemTextJson.Core

open System
open System.Buffers
open System.Runtime.InteropServices
open System.Text.Json

[<AutoOpen>]
module internal JsonSerializerExtensions =

    type JsonSerializer with
        static member SerializeToElement(value: 'T, [<Optional; DefaultParameterValue(null)>] ?options: JsonSerializerOptions) =
            let span = ReadOnlySpan.op_Implicit(JsonSerializer.SerializeToUtf8Bytes(value, defaultArg options null))
            JsonSerializer.Deserialize<JsonElement>(span)

        static member DeserializeElement<'T>(element: JsonElement, [<Optional; DefaultParameterValue(null)>] ?options: JsonSerializerOptions) =
#if NETSTANDARD2_0
            let json = element.GetRawText()
            JsonSerializer.Deserialize<'T>(json, defaultArg options null)
#else
            let bufferWriter = ArrayBufferWriter<byte>()
            (
                use jsonWriter = new Utf8JsonWriter(bufferWriter)
                element.WriteTo(jsonWriter)
            )
            JsonSerializer.Deserialize<'T>(bufferWriter.WrittenSpan, defaultArg options null)
#endif

        static member DeserializeElement(element : JsonElement, t : Type, [<Optional; DefaultParameterValue(null)>] ?options: JsonSerializerOptions) =
#if NETSTANDARD2_0
            JsonSerializer.Deserialize(write element, t, defaultArg options null)
#else
            JsonSerializer.Deserialize(element.GetRawText(), t, defaultArg options null)
#endif
