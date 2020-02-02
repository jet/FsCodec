namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System

/// Manages injecting prepared JSON into the data being submitted to a store such as CosmosDB as-is, on the basis we can trust it to be valid json
type VerbatimUtf8JsonConverter() =
    inherit JsonConverter()

    static let enc = System.Text.Encoding.UTF8

    override __.CanConvert(t : Type) =
        typeof<byte[]>.Equals(t)

    override __.WriteJson(writer : JsonWriter, value : obj, jsonSerializer : JsonSerializer) =
        let array = value :?> byte[]
        if array = null || array.Length = 0 then jsonSerializer.Serialize(writer, null)
        else writer.WriteRawValue(enc.GetString(array))

    override __.ReadJson(reader : JsonReader, _ : Type, _ : obj, _ : JsonSerializer) =
        let token = JToken.Load reader
        if token.Type = JTokenType.Null then null
        else token |> string |> enc.GetBytes |> box
