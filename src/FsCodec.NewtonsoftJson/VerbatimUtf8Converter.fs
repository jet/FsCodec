namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open Newtonsoft.Json.Linq

/// Manages injecting prepared json into the data being submitted to a store such as CosmosDB as-is, on the basis we can trust it to be valid json
type VerbatimUtf8JsonConverter() =
    inherit JsonConverter()

    static let enc = System.Text.Encoding.UTF8

    override __.ReadJson(reader, _, _, _) =
        let token = JToken.Load reader
        if token.Type = JTokenType.Null then null
        else token |> string |> enc.GetBytes |> box

    override __.CanConvert(objectType) =
        typeof<byte[]>.Equals(objectType)

    override __.WriteJson(writer, value, serializer) =
        let array = value :?> byte[]
        if array = null || array.Length = 0 then serializer.Serialize(writer, null)
        else writer.WriteRawValue(enc.GetString(array))