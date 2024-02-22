/// <summary>Utilities for working with F# DUs that have no bodies (i.e. pass both the <c>Union.isUnion</c> and <c>Union.isNullary</c> tests)</summary>
module FsCodec.TypeSafeEnum

open System
open System.Collections.Generic
open System.ComponentModel

let isTypeSafeEnum t = Union.isUnion t && Union.isNullary t

[<EditorBrowsable(EditorBrowsableState.Never)>]
let tryParseTF (t: Type) = Union.Info.tryFindCaseValueWithName t
[<EditorBrowsable(EditorBrowsableState.Never)>]
let parseTF (t: Type) =
    let tryParseF = tryParseTF t
    let fail value = sprintf "Could not find case '%s' for type '%s'" value t.FullName |> KeyNotFoundException |> raise
    fun predicate (str: string) ->
        match predicate str |> tryParseF with
        | Some e -> e
        | None -> fail str
[<EditorBrowsable(EditorBrowsableState.Never)>]
let parseT (t: Type) = parseTF t (=)

let tryParseF<'T> =
    let tryParse = tryParseTF typeof<'T>
    fun predicate str -> predicate str |> tryParse |> Option.map (fun e -> e :?> 'T)
let tryParse<'T> = tryParseF<'T> (=)
let parseF<'T> f =
    let p = parseTF typeof<'T> f
    fun (str: string) -> p str :?> 'T
let parse<'T> = parseF<'T> (=)

let toString<'t> : 't -> string = Union.caseName<'t>

/// <summary>Yields all the cases available for <c>'t</c>, which must be a <c>TypeSafeEnum</c>, i.e. have only nullary cases.</summary>
let caseValues<'t>: 't[] = Union.Info.caseValues<'t>
