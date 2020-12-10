namespace FsCodec.SystemTextJson

open FSharp.Reflection
open System

[<NoComparison; NoEquality>]
type private Union =
    {
        cases : UnionCaseInfo[]
        tagReader : obj -> int
        fieldReader : (obj -> obj[])[]
        caseConstructor : (obj[] -> obj)[]
    }

module private Union =

    let isUnion : Type -> bool = memoize (fun t -> FSharpType.IsUnion(t, true))
    let getUnionCases = memoize (fun t -> FSharpType.GetUnionCases(t, true))

    let private createUnion t =
        let cases = getUnionCases t
        {
            cases = cases
            tagReader = FSharpValue.PreComputeUnionTagReader(t, true)
            fieldReader = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionReader(c, true))
            caseConstructor = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionConstructor(c, true))
        }
    let getUnion : Type -> Union = memoize createUnion
