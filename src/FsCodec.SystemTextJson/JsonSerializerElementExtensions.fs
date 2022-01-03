namespace FsCodec.SystemTextJson.Core

open System
open System.Buffers
open System.Runtime.InteropServices
open System.Text.Json

[<AutoOpen>]
module internal JsonSerializerExtensions =

    type JsonSerializer with
        static member DeserializeElement<'T>(element: JsonElement, [<Optional; DefaultParameterValue(null)>] ?options: JsonSerializerOptions) =
            let bufferWriter = ArrayBufferWriter<byte>()
            (
                use jsonWriter = new Utf8JsonWriter(bufferWriter)
                element.WriteTo(jsonWriter)
            )
            JsonSerializer.Deserialize<'T>(bufferWriter.WrittenSpan, defaultArg options null)

        static member DeserializeElement(element : JsonElement, t : Type, [<Optional; DefaultParameterValue(null)>] ?options: JsonSerializerOptions) =
            JsonSerializer.Deserialize(element.GetRawText(), t, defaultArg options null)
