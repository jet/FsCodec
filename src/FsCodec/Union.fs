module FsCodec.Union

open System
open System.ComponentModel
open Microsoft.FSharp.Reflection

/// Provides a thread-safe memoization wrapper for supplied function
let private memoize: ('T -> 'S) -> 'T -> 'S =
    fun f ->
        let cache = System.Collections.Concurrent.ConcurrentDictionary<'T, 'S>()
        fun t -> cache.GetOrAdd(t, f)

[<NoComparison; NoEquality; EditorBrowsable(EditorBrowsableState.Never)>]
type Info =
    {   cases: UnionCaseInfo[]
        tagReader: obj -> int
        caseConstructor: (obj[] -> obj)[] }
[<EditorBrowsable(EditorBrowsableState.Never)>]
module Info =
    let get: Type -> Info = memoize (fun t ->
        let cases = FSharpType.GetUnionCases(t, true)
        {   cases = cases
            tagReader = FSharpValue.PreComputeUnionTagReader(t, true)
            caseConstructor = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionConstructor(c, true)) })

/// Determines whether the type is a Union
let isUnion: Type -> bool = memoize (fun t -> FSharpType.IsUnion(t, true))

/// Determines whether a union has no bodies (and hence can use a TypeSafeEnum.parse and/or TypeSafeEnumConverter)
let isNullary (t: Type) =
    let u = Info.get t
    u.cases |> Array.forall (fun case -> case.GetFields().Length = 0)

[<EditorBrowsable(EditorBrowsableState.Never)>]
let caseNameT t =
    let u = Info.get t
    fun x -> u.cases[u.tagReader x].Name

/// Yields the case name for a given value, regardless of whether it <c>isNullary</c> or not.
let caseName<'t> =
    let u = Info.get typeof<'t>
    fun (x: 't) -> u.cases[u.tagReader x].Name
