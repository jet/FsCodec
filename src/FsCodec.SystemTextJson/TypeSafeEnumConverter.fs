namespace FsCodec.SystemTextJson

open System
open System.Collections.Generic
open System.ComponentModel
open System.Text.Json

/// Utilities for working with DUs where none of the cases have a value
module TypeSafeEnum =

    let isTypeSafeEnum (t: Type) =
        Union.isUnion t
        && Union.hasOnlyNullaryCases t

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let tryParseTF (t: Type) =
        let u = Union.getInfo t
        fun predicate ->
            u.cases
            |> Array.tryFindIndex (fun c -> predicate c.Name)
            |> Option.map (fun tag -> u.caseConstructor[tag] [||])
    let tryParseF<'T> = let p = tryParseTF typeof<'T> in fun f str -> p (f str) |> Option.map (fun e -> e :?> 'T)
    let tryParse<'T> = tryParseF<'T> (=)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let parseTF (t: Type) predicate =
        let p = tryParseTF t
        fun (str: string) ->
            match p (predicate str) with
            | Some e -> e
            | None ->
                // Keep exception compat, but augment with a meaningful message.
                raise (KeyNotFoundException(sprintf "Could not find case '%s' for type '%s'" str t.FullName))
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let parseT (t: Type) = parseTF t (=)
    let parseF<'T> f =
        let p = parseTF typeof<'T> f
        fun (str: string) -> p str :?> 'T
    let parse<'T> = parseF<'T> (=)

    let toString<'t> =
        let u = Union.getInfo typeof<'t>
        fun (x: 't) ->
            let tag = u.tagReader x
            u.cases[tag].Name

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
