/// Utilities for working with F# DUs that have no bodies (i.e. pass both the <c>Union.isUnion</c> and <c>Union.isNullary</c> tests)
module FsCodec.TypeSafeEnum

open System
open System.Collections.Generic
open System.ComponentModel

let isTypeSafeEnum t = Union.isUnion t && Union.isNullary t

[<EditorBrowsable(EditorBrowsableState.Never)>]
let tryParseTF (t: Type) =
    let u = Union.Info.get t
    fun predicate ->
        u.cases
        |> Array.tryFindIndex (fun c -> predicate c.Name)
        |> Option.map (fun tag -> u.caseConstructor[tag] Array.empty)
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

let tryParseF<'T> =
    let p = tryParseTF typeof<'T>
    fun f str -> p (f str) |> Option.map (fun e -> e :?> 'T)
let tryParse<'T> = tryParseF<'T> (=)
let parseF<'T> f =
    let p = parseTF typeof<'T> f
    fun (str: string) -> p str :?> 'T
let parse<'T> = parseF<'T> (=)

let toString<'t> = Union.caseName<'t>
