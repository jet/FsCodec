namespace FsCodec.SystemTextJson

open System.Text.Json

/// Maps strings to/from Union cases; refuses to convert for values not in the Union
type TypeSafeEnumConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    override _.CanConvert t =
        t = typeof<'T> && FsCodec.Union.isUnion t && FsCodec.Union.isNullary t

    override _.Write(writer, value, _options) =
        value |> FsCodec.TypeSafeEnum.toString |> writer.WriteStringValue

    override _.Read(reader, _t, _options) =
        if reader.TokenType <> JsonTokenType.String then
            sprintf "Unexpected token when reading TypeSafeEnum: %O" reader.TokenType |> JsonException |> raise
        reader.GetString() |> FsCodec.TypeSafeEnum.parse<'T>
