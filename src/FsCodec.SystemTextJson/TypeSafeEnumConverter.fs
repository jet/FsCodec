namespace FsCodec.SystemTextJson

open System
open System.Collections.Generic
open System.Text.Json

/// Utilities for working with DUs where none of the cases have a value
module TypeSafeEnum =

    let private _isTypeSafeEnum (t : Type) =
        Union.tryGetUnion t
        |> Option.exists (fun u -> u.cases |> Seq.forall (fun case -> case.GetFields().Length = 0))
    let isTypeSafeEnum = memoize _isTypeSafeEnum

    let tryParseT (t : Type) (str : string) =
        match Union.tryGetUnion t with
        | None -> invalidArg "t" "Type must be a FSharpUnion."
        | Some u ->
            u.cases
            |> Array.tryFindIndex (fun case -> case.Name = str)
            |> Option.map (fun tag -> u.caseConstructor.[tag] [||])
            // TOCONSIDER memoize and/or push into `Union` https://github.com/jet/FsCodec/pull/41#discussion_r394473137
    let tryParse<'T> (str : string) = tryParseT typeof<'T> str |> Option.map (fun e -> e :?> 'T)

    let parseT (t : Type) (str : string) =
        match tryParseT t str with
        | Some e -> e
        | None   ->
            // Keep exception compat, but augment with a meaningful message.
            raise (KeyNotFoundException(sprintf "Could not find case '%s' for type '%s'" str t.FullName))
    let parse<'T> (str : string) = parseT typeof<'T> str :?> 'T

    let toString (x : obj) =
        let union = x.GetType() |> Union.tryGetUnion |> Option.get
        let tag = union.tagReader x
        // TOCONSIDER memoize and/or push into `Union` https://github.com/jet/FsCodec/pull/41#discussion_r394473137
        union.cases.[tag].Name

/// Maps strings to/from Union cases; refuses to convert for values not in the Union
type TypeSafeEnumConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    override __.CanConvert(t : Type) =
        TypeSafeEnum.isTypeSafeEnum t

    override __.Write(writer, value, _options) =
        let str = TypeSafeEnum.toString value
        writer.WriteStringValue str

    override __.Read(reader, _t, _options) =
        if reader.TokenType <> JsonTokenType.String then
            sprintf "Unexpected token when reading TypeSafeEnum: %O" reader.TokenType |> JsonException |> raise
        let str = reader.GetString()
        TypeSafeEnum.parse<'T> str
