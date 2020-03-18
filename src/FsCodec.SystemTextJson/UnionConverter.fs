﻿namespace FsCodec.SystemTextJson

open FSharp.Reflection
open System
open System.Reflection
open System.Text.Json

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


    /// Paralells F# behavior wrt how it generates a DU's underlying .NET Type
    let inline isInlinedIntoUnionItem (t : Type) =
        t = typeof<string>
        || t.IsValueType
        || t.IsArray
        || (t.IsGenericType
           && (typedefof<Option<_>> = t.GetGenericTypeDefinition()
                || t.GetGenericTypeDefinition().IsValueType)) // Nullable<T>

    let typeHasJsonConverterAttribute = memoize (fun (t : Type) -> t.IsDefined(typeof<Serialization.JsonConverterAttribute>, false))

    let propTypeRequiresConstruction (propertyType : Type) =
        not (isInlinedIntoUnionItem propertyType)
        && not (typeHasJsonConverterAttribute propertyType)

    /// Prepare arguments for the Case class ctor based on the kind of case and how F# maps that to a Type
    /// and/or whether we need to let json.net step in to convert argument types
    let mapTargetCaseArgs (inputJObject : JsonDocument) serializer : PropertyInfo[] -> obj [] = function
        | [| singleCaseArg |] when propTypeRequiresConstruction singleCaseArg.PropertyType ->
            [| inputJObject.(singleCaseArg.PropertyType, serializer) |]
        | multipleFieldsInCustomCaseType ->
            [| for fi in multipleFieldsInCustomCaseType ->
                match inputJObject.[fi.Name] with
                | null when
                    // Afford converters an opportunity to handle the missing field in the best way I can figure out to signal that
                    // The specific need being covered (see tests) is to ensure that, even with MissingMemberHandling=Ignore,
                    // the TypeSafeEnumConverter should reject missing values
                    // not having this case would go direct to `null` without passing go
                    typeHasJsonConverterAttribute fi.PropertyType
                    || serializer.MissingMemberHandling = MissingMemberHandling.Error ->
                        // NB caller can opt out of erroring by setting NullValueHandling = NullValueHandling.Ignore)
                        // which renders the following equivalent to the next case
                        JToken.Parse("null").ToObject(fi.PropertyType, serializer)
                | null -> null
                | itemValue -> itemValue.ToObject(fi.PropertyType, serializer) |]

/// Serializes a discriminated union case with a single field that is a
/// record by flattening the record fields to the same level as the discriminator
type UnionConverter<'T> private (discriminator : string, ?catchAllCase) =
    inherit Serialization.JsonConverter<'T>()

    new() = UnionConverter("case")
    new(discriminator: string, catchAllCase: string) = UnionConverter(discriminator, ?catchAllCase = match catchAllCase with null -> None | x -> Some x)

    override __.CanConvert (t : Type) = Union.tryGetUnion t |> Option.isSome

    override __.Write(writer, value, options) =
        let union = Union.tryGetUnion (value.GetType())
        let tag = union.tagReader value
        let case = union.cases.[tag]
        let fieldValues = union.fieldReader.[tag] value
        let fieldInfos = case.GetFields()

        writer.WriteStartObject()

        writer.WritePropertyName(discriminator)
        writer.WriteValue(case.Name)

        match fieldInfos with
        | [| fi |] ->
            match fieldValues.[0] with
            | null when serializer.NullValueHandling = NullValueHandling.Ignore -> ()
            | fv ->
                let token = if fv = null then JToken.Parse "null" else JToken.FromObject(fv, serializer)
                match token.Type with
                | JTokenType.Object ->
                    // flatten the object properties into the same one as the discriminator
                    for prop in token.Children() do
                        prop.WriteTo writer
                | _ ->
                    writer.WritePropertyName(fi.Name)
                    token.WriteTo writer
        | _ ->
            for fieldInfo, fieldValue in Seq.zip fieldInfos fieldValues do
                if fieldValue <> null || serializer.NullValueHandling = NullValueHandling.Include then
                    writer.WritePropertyName(fieldInfo.Name)
                    serializer.Serialize(writer, fieldValue)

        writer.WriteEndObject()

    override __.Read(reader, t : Type, options) =
        let token = JToken.ReadFrom reader
        if token.Type <> JTokenType.Object then raise (FormatException(sprintf "Expected object token, got %O" token.Type))
        let inputJObject = token :?> JObject

        let union = Union.getUnion t
        let targetCaseIndex =
            let inputCaseNameValue = inputJObject.[discriminator] |> string
            let findCaseNamed x = union.cases |> Array.tryFindIndex (fun case -> case.Name = x)
            match findCaseNamed inputCaseNameValue, catchAllCase  with
            | None, None ->
                sprintf "No case defined for '%s', and no catchAllCase nominated for '%s' on type '%s'"
                    inputCaseNameValue typeof<UnionConverter>.Name t.FullName |> invalidOp
            | Some foundIndex, _ -> foundIndex
            | None, Some catchAllCaseName ->
                match findCaseNamed catchAllCaseName with
                | None ->
                    sprintf "No case defined for '%s', nominated catchAllCase: '%s' not found in type '%s'"
                        inputCaseNameValue catchAllCaseName t.FullName |> invalidOp
                | Some foundIndex -> foundIndex

        let targetCaseFields, targetCaseCtor = union.cases.[targetCaseIndex].GetFields(), union.caseConstructor.[targetCaseIndex]
        targetCaseCtor (Union.mapTargetCaseArgs inputJObject serializer targetCaseFields)
