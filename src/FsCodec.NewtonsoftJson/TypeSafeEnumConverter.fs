namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System
open System.Collections.Generic

/// Utilities for working with DUs where none of the cases have a value
module TypeSafeEnum =

    let isTypeSafeEnum (t : Type) =
        Union.isUnion t
        && Union.hasOnlyNullaryCases t

    let tryParseT (t : Type) (str : string) =
        let u = Union.getInfo t
        u.cases
        |> Array.tryFindIndex (fun c -> c.Name = str)
        |> Option.map (fun tag -> u.caseConstructor[tag] [||])
    let tryParse<'T> (str : string) = tryParseT typeof<'T> str |> Option.map (fun e -> e :?> 'T)

    let parseT (t : Type) (str : string) =
        match tryParseT t str with
        | Some e -> e
        | None   ->
            // Keep exception compat, but augment with a meaningful message.
            raise (KeyNotFoundException(sprintf "Could not find case '%s' for type '%s'" str t.FullName))
    let parse<'T> (str : string) = parseT typeof<'T> str :?> 'T

    let toString<'t> (x : 't) =
        let u = Union.getInfo typeof<'t>
        let tag = u.tagReader x
        u.cases[tag].Name

/// Maps strings to/from Union cases; refuses to convert for values not in the Union
type TypeSafeEnumConverter() =
    inherit JsonConverter()

    override _.CanConvert(t : Type) =
        TypeSafeEnum.isTypeSafeEnum t

    override _.WriteJson(writer : JsonWriter, value : obj, _ : JsonSerializer) =
        let str = TypeSafeEnum.toString value
        writer.WriteValue str

    override _.ReadJson(reader : JsonReader, t : Type, _ : obj, _ : JsonSerializer) =
        if reader.TokenType <> JsonToken.String then
            sprintf "Unexpected token when reading TypeSafeEnum: %O" reader.TokenType |> JsonSerializationException |> raise
        let str = reader.Value :?> string
        TypeSafeEnum.parseT t str
