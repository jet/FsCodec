namespace FsCodec.SystemTextJson.Core

open System.Runtime.CompilerServices
open System.Text.Json

[<Extension>]
type internal Utf8JsonReaderExtension =
    [<Extension>]
    static member ValidateTokenType(reader: Utf8JsonReader, expectedTokenType) =
        if reader.TokenType <> expectedTokenType then
            sprintf "Expected a %A token, but encountered a %A token when parsing JSON." expectedTokenType (reader.TokenType)
            |> JsonException
            |> raise
