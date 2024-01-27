module FsCodec.Union

open Microsoft.FSharp.Reflection
open System
open System.ComponentModel

let private memoize (f: 'T -> 'S): 'T -> 'S =
    let cache = System.Collections.Concurrent.ConcurrentDictionary<'T, 'S>()
    fun t -> cache.GetOrAdd(t, f)

[<Struct; NoComparison; NoEquality; EditorBrowsable(EditorBrowsableState.Never)>]
type CaseInfo = { name: string; fields: System.Reflection.PropertyInfo[]; construct: obj[] -> obj; deconstruct: obj -> obj[] }

[<Struct; NoComparison; NoEquality; EditorBrowsable(EditorBrowsableState.Never)>]
type Info = { cases: CaseInfo[]; getCase: obj -> CaseInfo }

[<EditorBrowsable(EditorBrowsableState.Never)>]
module Info =
    let get: Type -> Info = memoize (fun t ->
        let cases = FSharpType.GetUnionCases(t, true) |> Array.map (fun i ->
             {  name = i.Name
                fields = i.GetFields()
                construct = FSharpValue.PreComputeUnionConstructor(i, true)
                deconstruct = FSharpValue.PreComputeUnionReader(i, true) })
        let getTag = FSharpValue.PreComputeUnionTagReader(t, true)
        let getCase value = cases[getTag value]
        { cases = cases; getCase = getCase })
    let tryFindCaseWithName u (predicate: string -> bool): CaseInfo option = u.cases |> Array.tryFind (fun c -> predicate c.name)
    let private caseValues: Type -> obj[] = memoize (fun t -> (get t).cases |> Array.map (fun c -> c.construct Array.empty))
    let tryFindCaseValueWithName (t: Type): (string -> bool) -> obj option =
        let u = get t
        let caseValue = let values = caseValues t in fun i -> values[i]
        fun predicate -> u.cases |> Array.tryFindIndex (fun c -> predicate c.name) |> Option.map caseValue

/// Determines whether the type is a Union
let isUnion: Type -> bool = memoize (fun t -> FSharpType.IsUnion(t, true))

/// Determines whether a union has no bodies (and hence can use a TypeSafeEnum.parse and/or TypeSafeEnumConverter)
let isNullary (t: Type) = let u = Info.get t in u.cases |> Array.forall (fun case -> case.fields.Length = 0)

[<EditorBrowsable(EditorBrowsableState.Never)>]
let caseNameT (t: Type) (x: obj) = ((Info.get t).getCase x).name

/// <summary>Yields the case name for a given value, regardless of whether it <c>isNullary</c> or not.</summary>
let caseName<'t>(x: 't) = ((Info.get typeof<'t>).getCase x).name
