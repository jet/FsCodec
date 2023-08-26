namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System

/// Maps strings to/from Union cases; refuses to convert for values not in the Union
type TypeSafeEnumConverter() =
    inherit JsonConverter()

    override _.CanConvert(t: Type) =
        FsCodec.TypeSafeEnum.isTypeSafeEnum t

    override _.WriteJson(writer: JsonWriter, value: obj, _: JsonSerializer) =
        let t = value.GetType()
        let str = FsCodec.Union.caseNameT t value
        writer.WriteValue str

    override _.ReadJson(reader : JsonReader, t: Type, _: obj, _: JsonSerializer) =
        if reader.TokenType <> JsonToken.String then
            sprintf "Unexpected token when reading TypeSafeEnum: %O" reader.TokenType |> JsonSerializationException |> raise
        let str = reader.Value :?> string
        FsCodec.TypeSafeEnum.parseT t str
