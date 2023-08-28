namespace FsCodec.NewtonsoftJson

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
                let case = let t = value.GetType() in (FsCodec.Union.Info.get t).getCase value
                case.deconstruct value |> Array.exactlyOne

        serializer.Serialize(writer, value)

    override _.ReadJson(reader: JsonReader, t: Type, _existingValue: obj, serializer: JsonSerializer) =
        let innerType =
            let innerType = t.GetGenericArguments().[0]
            if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType(innerType)
            else innerType

        let u = FsCodec.Union.Info.get t
        let inline none () = u.cases[0].construct Array.empty
        if reader.TokenType = JsonToken.Null then
            none ()
        else
            let value = serializer.Deserialize(reader, innerType)
            if value = null then none ()
            else u.cases[1].construct (Array.singleton value)
