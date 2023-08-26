namespace FsCodec.NewtonsoftJson

open FSharp.Reflection
open Newtonsoft.Json
open System

/// For Some 1 generates "1", for None generates "null"
type OptionConverter() =
    inherit JsonConverter()

    override _.CanConvert(t: Type) = t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override _.WriteJson(writer: JsonWriter, value: obj, serializer: JsonSerializer) =
        let value =
            if value = null then null
            else
                let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
                fields[0]

        serializer.Serialize(writer, value)

    override _.ReadJson(reader: JsonReader, t: Type, _existingValue: obj, serializer: JsonSerializer) =
        let innerType =
            let innerType = t.GetGenericArguments().[0]
            if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType(innerType)
            else innerType

        let cases = let ui = FsCodec.Union.Info.get t in ui.cases
        if reader.TokenType = JsonToken.Null then FSharpValue.MakeUnion(cases[0], Array.empty)
        else
            let value = serializer.Deserialize(reader, innerType)
            if value = null then FSharpValue.MakeUnion(cases[0], Array.empty)
            else FSharpValue.MakeUnion(cases[1], [| value |])
