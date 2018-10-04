namespace Newtonsoft.Json.Converters.FSharp

open FSharp.Reflection
open Newtonsoft.Json
open System

/// For Some 1 generates "1", for None generates "null"
type OptionConverter() =
    inherit JsonConverter()

    override __.CanConvert(t) = t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override __.WriteJson(writer, value, serializer) =
        let value =
            if value = null then null
            else
                let _,fields = FSharpValue.GetUnionFields(value, value.GetType())
                fields.[0]
        serializer.Serialize(writer, value)

    override __.ReadJson(reader, t, _existingValue, serializer) =
        let innerType =
            let innerType = t.GetGenericArguments().[0]
            if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType(innerType)
            else innerType

        let cases = Union.getUnionCases t
        if reader.TokenType = JsonToken.Null then FSharpValue.MakeUnion(cases.[0], Array.empty)
        else
            let value = serializer.Deserialize(reader, innerType)
            if value = null then FSharpValue.MakeUnion(cases.[0], Array.empty)
            else FSharpValue.MakeUnion(cases.[1], [|value|])