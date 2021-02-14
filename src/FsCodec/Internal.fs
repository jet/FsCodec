module internal Internal

open TypeShape.Core

let checkIfSupported<'Contract> requireRecordFields =
    if not requireRecordFields then
        let shape =
            match shapeof<'Contract> with
            | Shape.FSharpUnion (:? ShapeFSharpUnion<'Contract> as s) -> s
            | _ ->
                sprintf "Type '%O' is not an F# union" typeof<'Contract>
                |> invalidArg "Union"
        let isAllowed (scase : ShapeFSharpUnionCase<_>) =
            match scase.Fields with
            | [| field |] ->
                match field.Member with
                // non-primitives
                | Shape.FSharpRecord _
                | Shape.Guid _

                // primitives
                | Shape.Bool _
                | Shape.Byte _
                | Shape.SByte _
                | Shape.Int16 _
                | Shape.Int32 _
                | Shape.Int64 _
                //| Shape.IntPtr _ // unsupported
                | Shape.UInt16 _
                | Shape.UInt32 _
                | Shape.UInt64 _
                //| Shape.UIntPtr _ // unsupported
                | Shape.Single _
                | Shape.Double _
                | Shape.Char _ -> true
                | _ -> false
            | [||] -> true // allows all nullary cases, but a subsequent check is done by UnionContractEncoder.Create with `allowNullaryCases`
            | _ -> false
        shape.UnionCases
        |> Array.tryFind (not << isAllowed)
        |> function
        | None -> ()
        | Some x -> failwithf "The '%s' case has an unsupported type: '%s'" x.CaseInfo.Name x.Fields.[0].Member.Type.FullName
