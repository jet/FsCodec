namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System.Collections.Generic
open System

/// Utilities for working with DUs where none of the cases have a value
module TypeSafeEnum =

    let isTypeSafeEnum (t : Type) =
        Union.isUnion t
        && (Union.getUnion t).cases |> Seq.forall (fun case -> case.GetFields().Length = 0)

    let tryParseT (t : Type) (str : string) =
        let union = Union.getUnion t
        union.cases
        |> Array.tryFindIndex (fun case -> case.Name = str)
        |> Option.map (fun tag -> (union.caseConstructor.[tag] [||]))
    let tryParse<'T> (str : string) = tryParseT typeof<'T> str |> Option.map (fun e -> e :?> 'T)

    let parseT (t : Type) (str : string) =
        match tryParseT t str with
        | Some e -> e
        | None   ->
            // Keep exception compat, but augment with a meaningful message.
            raise (KeyNotFoundException(sprintf "Could not find case '%s' for type '%s'" str t.FullName))
    let parse<'T> (str : string) = parseT typeof<'T> str :?> 'T

    let toString (x : obj) =
        let union = Union.getUnion (x.GetType())
        let tag = union.tagReader x
        union.cases.[tag].Name

/// Maps strings to/from Union cases; refuses to convert for values not in the Union
type TypeSafeEnumConverter() =
    inherit JsonConverter()

    override __.CanConvert (t : Type) = TypeSafeEnum.isTypeSafeEnum t

    override __.WriteJson(writer : JsonWriter, value : obj, _ : JsonSerializer) =
        let str = TypeSafeEnum.toString value
        writer.WriteValue str

    override __.ReadJson(reader : JsonReader, t : Type, _ : obj, _ : JsonSerializer) =
        if reader.TokenType <> JsonToken.String then
            sprintf "Unexpected token when reading TypeSafeEnum: %O" reader.TokenType |> JsonSerializationException |> raise
        let str = reader.Value :?> string
        TypeSafeEnum.parseT t str
