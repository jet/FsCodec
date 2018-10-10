﻿namespace Newtonsoft.Json.Converters.FSharp

open FSharp.Reflection
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.Reflection

[<NoComparison; NoEquality>]
type private Union =
    {
        cases: UnionCaseInfo[]
        tagReader: obj -> int
        fieldReader: (obj -> obj[])[]
        caseConstructor: (obj[] -> obj)[]
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private Union =
    let isUnion = memoize (fun t -> FSharpType.IsUnion(t, true))
    let getUnionCases = memoize (fun t -> FSharpType.GetUnionCases(t, true))

    let createUnion t =
        let cases = getUnionCases t
        {
            cases = cases
            tagReader = FSharpValue.PreComputeUnionTagReader(t, true)
            fieldReader = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionReader(c, true))
            caseConstructor = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionConstructor(c, true))
        }
    let getUnion = memoize createUnion

    /// Paralells F# behavior wrt how it generates a DU's underlying .NET Type
    let inline isInlinedIntoUnionItem (t : Type) =
        t = typeof<string>
        || t.IsValueType
        || t.IsArray
        || (t.IsGenericType
           && (typedefof<Option<_>> = t.GetGenericTypeDefinition()
                || t.GetGenericTypeDefinition().IsValueType)) // Nullable<T>

    let typeHasJsonConverterAttribute = memoize (fun (t : Type) -> t.IsDefined(typeof<JsonConverterAttribute>))

    let propTypeRequiresConstruction (propertyType : Type) =
        not (isInlinedIntoUnionItem propertyType)
        && not (typeHasJsonConverterAttribute propertyType)

    /// Prepare arguments for the Case class ctor based on the kind of case and how F# maps that to a Type
    /// and/or whether we need to let json.net step in to convert argument types
    let mapTargetCaseArgs (inputJObject : JObject) jsonSerializer : PropertyInfo[] -> obj [] = function
        | [| singleCaseArg |] when propTypeRequiresConstruction singleCaseArg.PropertyType ->
            [| inputJObject.ToObject(singleCaseArg.PropertyType, jsonSerializer) |]
        | multipleFieldsInCustomCaseType ->
            [| for fi in multipleFieldsInCustomCaseType ->
                match inputJObject.[fi.Name] with
                | null when typeHasJsonConverterAttribute fi.PropertyType ->
                        JToken.Parse("null").ToObject(fi.PropertyType, jsonSerializer)
                | null -> null
                | itemValue -> itemValue.ToObject(fi.PropertyType, jsonSerializer) |]

/// Serializes a discriminated union case with a single field that is a
/// record by flattening the record fields to the same level as the discriminator
type UnionConverter private (discriminator : string, ?catchAllCase) =
    inherit JsonConverter()

    new(discriminator: string, catchAllCase: string) = UnionConverter(discriminator, ?catchAllCase = match catchAllCase with null -> None | x -> Some x)
    new() = UnionConverter("case")

    override __.CanConvert (t: Type) = Union.isUnion t

    override __.WriteJson(writer: JsonWriter, value: obj, jsonSerializer: JsonSerializer) =
        let union = Union.getUnion (value.GetType())
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
            | null -> ()
            | fv ->
                let token = JToken.FromObject(fv, jsonSerializer)
                match token.Type with
                | JTokenType.Object ->
                    // flatten the object properties into the same one as the discriminator
                    for prop in token.Children() do
                        if prop <> null then
                            prop.WriteTo writer
                | _ ->
                    writer.WritePropertyName(fi.Name)
                    token.WriteTo writer
        | _ ->
            for fieldInfo, fieldValue in Seq.zip fieldInfos fieldValues do
                if fieldValue <> null then
                    writer.WritePropertyName(fieldInfo.Name)
                    jsonSerializer.Serialize(writer, fieldValue)

        writer.WriteEndObject()

    override __.ReadJson(reader: JsonReader, t: Type, _: obj, jsonSerializer: JsonSerializer) =
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
        targetCaseCtor (Union.mapTargetCaseArgs inputJObject jsonSerializer targetCaseFields)