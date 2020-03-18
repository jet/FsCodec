namespace FsCodec.SystemTextJson

open FSharp.Reflection
open System

[<NoComparison; NoEquality>]
type private Union =
    {
        cases: UnionCaseInfo[]
        tagReader: obj -> int
        fieldReader: (obj -> obj[])[]
        caseConstructor: (obj[] -> obj)[]
    }

module private Union =
    let private _tryGetUnion t =
        if not (FSharpType.IsUnion(t, true)) then
            None
        else
            let cases = FSharpType.GetUnionCases(t, true)
            {
                cases = cases
                tagReader = FSharpValue.PreComputeUnionTagReader(t, true)
                fieldReader = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionReader(c, true))
                caseConstructor = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionConstructor(c, true))
            } |> Some
    let tryGetUnion : Type -> Union option = memoize _tryGetUnion
