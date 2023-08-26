namespace FsCodec.SystemTextJson

open System
open System.Text.Json

/// Maps strings to/from Union cases; refuses to convert for values not in the Union
type TypeSafeEnumConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    override _.CanConvert(t: Type) =
        let tt = typedefof<'T>
        t = tt && FsCodec.Union.isUnion tt && FsCodec.Union.isNullary tt

    override _.Write(writer, value, _options) =
        let str = FsCodec.TypeSafeEnum.toString value
        writer.WriteStringValue str

    override _.Read(reader, _t, _options) =
        if reader.TokenType <> JsonTokenType.String then
            sprintf "Unexpected token when reading TypeSafeEnum: %O" reader.TokenType |> JsonException |> raise
        let str = reader.GetString()
        FsCodec.TypeSafeEnum.parse<'T> str
