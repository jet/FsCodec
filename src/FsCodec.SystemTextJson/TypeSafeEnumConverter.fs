namespace FsCodec.SystemTextJson

open System
open System.Collections.Generic
open System.Text.Json

/// Utilities for working with DUs where none of the cases have a value
module TypeSafeEnum =

    let isTypeSafeEnum : Type -> bool = function
        | Union.TypeSafeEnum -> true
        | Union.NotUnion | Union.Other -> false

    let tryParseT (t : Type) predicate =
        let u = Union.getUnion t
        u.cases
        |> Array.tryFindIndex (fun c -> predicate c.Name)
        |> Option.map (fun tag -> u.caseConstructor.[tag] [||])
        // TOCONSIDER memoize and/or push into `Union` https://github.com/jet/FsCodec/pull/41#discussion_r394473137
    let tryParse<'T> (str : string) = tryParseT typeof<'T> ((=) str) |> Option.map (fun e -> e :?> 'T)

    let parseT (t : Type) (str : string)  =
        match tryParseT t ((=) str) with
        | Some e -> e
        | None   ->
            // Keep exception compat, but augment with a meaningful message.
            raise (KeyNotFoundException(sprintf "Could not find case '%s' for type '%s'" str t.FullName))
    let parse<'T> (str : string) = parseT typeof<'T> str :?> 'T

    let toString<'t> (x : 't) =
        let u = Union.getUnion typeof<'t>
        let tag = u.tagReader (box x)
        // TOCONSIDER memoize and/or push into `Union` https://github.com/jet/FsCodec/pull/41#discussion_r394473137
        u.cases.[tag].Name

/// Maps strings to/from Union cases; refuses to convert for values not in the Union
type TypeSafeEnumConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    override _.CanConvert(t : Type) =
        t = typedefof<'T> && TypeSafeEnum.isTypeSafeEnum typedefof<'T>

    override _.Write(writer, value, _options) =
        let str = TypeSafeEnum.toString value
        writer.WriteStringValue str

    override _.Read(reader, _t, _options) =
        if reader.TokenType <> JsonTokenType.String then
            sprintf "Unexpected token when reading TypeSafeEnum: %O" reader.TokenType |> JsonException |> raise
        let str = reader.GetString()
        TypeSafeEnum.parse<'T> str
