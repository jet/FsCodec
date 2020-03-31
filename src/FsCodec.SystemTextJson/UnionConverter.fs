namespace FsCodec.SystemTextJson.Converters

open FsCodec.SystemTextJson
open FsCodec.SystemTextJson.Core
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
    let mapTargetCaseArgs (element : JsonElement) options : PropertyInfo[] -> obj [] = function
        | [| singleCaseArg |] when propTypeRequiresConstruction singleCaseArg.PropertyType ->
            [| JsonSerializer.DeserializeElement (element, singleCaseArg.PropertyType, options) |]
        | multipleFieldsInCustomCaseType ->
            [| for fi in multipleFieldsInCustomCaseType ->
                match element.GetProperty fi.Name with
                // TOTHINK: I'm not sure this applies with STJ

                //| null when
                //    // Afford converters an opportunity to handle the missing field in the best way I can figure out to signal that
                //    // The specific need being covered (see tests) is to ensure that, even with MissingMemberHandling=Ignore,
                //    // the TypeSafeEnumConverter should reject missing values
                //    // not having this case would go direct to `null` without passing go
                //    typeHasJsonConverterAttribute fi.PropertyType
                //    || serializer.MissingMemberHandling = MissingMemberHandling.Error ->
                //        // NB caller can opt out of erroring by setting NullValueHandling = NullValueHandling.Ignore)
                //        // which renders the following equivalent to the next case
                //        JToken.Parse("null").ToObject(fi.PropertyType, serializer)
                | el when el.ValueKind = JsonValueKind.Null -> null
                | el -> JsonSerializer.DeserializeElement (el, fi.PropertyType, options) |]

/// Serializes a discriminated union case with a single field that is a
/// record by flattening the record fields to the same level as the discriminator
type UnionConverter<'T> private (discriminator : string, ?catchAllCase) =
    inherit Serialization.JsonConverter<'T>()

    new() = UnionConverter("case")
    new(discriminator: string, catchAllCase: string) = UnionConverter(discriminator, ?catchAllCase = match catchAllCase with null -> None | x -> Some x)

    override __.CanConvert (t : Type) = Union.tryGetUnion t |> Option.isSome

    override __.Write(writer, value, options) =
        let value = box value
        let union = Union.tryGetUnion (value.GetType()) |> Option.get // TOASK: Do we wanna keep the try?
        let tag = union.tagReader value
        let case = union.cases.[tag]
        let fieldValues = union.fieldReader.[tag] value
        let fieldInfos = case.GetFields()

        writer.WriteStartObject()

        writer.WritePropertyName(discriminator)
        writer.WriteStringValue(case.Name)

        match fieldInfos with
        | [| fi |] ->
            match fieldValues.[0] with
            | null when options.IgnoreNullValues -> ()
            | fv ->
                let element = JsonSerializer.SerializeToElement(fv, options)
                match element.ValueKind with
                | JsonValueKind.Object ->
                    // flatten the object properties into the same one as the discriminator
                    for prop in element.EnumerateObject() do
                        prop.WriteTo writer
                | _ ->
                    writer.WritePropertyName(fi.Name)
                    element.WriteTo writer
        | _ ->
            for fieldInfo, fieldValue in Seq.zip fieldInfos fieldValues do
                if fieldValue <> null || not options.IgnoreNullValues then
                    writer.WritePropertyName(fieldInfo.Name)
                    JsonSerializer.Serialize(writer, fieldValue, options)

        writer.WriteEndObject()

    override __.Read(reader, t : Type, options) =
        reader.ValidateTokenType(JsonTokenType.StartObject)
        use document = JsonDocument.ParseValue &reader
        let element = document.RootElement

        let union = Union.tryGetUnion t |> Option.get // TOASK: Do we wanna keep the try?
        let targetCaseIndex =
            let inputCaseNameValue = element.GetProperty discriminator |> string
            let findCaseNamed x = union.cases |> Array.tryFindIndex (fun case -> case.Name = x)
            match findCaseNamed inputCaseNameValue, catchAllCase  with
            | None, None ->
                sprintf "No case defined for '%s', and no catchAllCase nominated for '%s' on type '%s'"
                    inputCaseNameValue typeof<UnionConverter<_>>.Name t.FullName |> invalidOp
            | Some foundIndex, _ -> foundIndex
            | None, Some catchAllCaseName ->
                match findCaseNamed catchAllCaseName with
                | None ->
                    sprintf "No case defined for '%s', nominated catchAllCase: '%s' not found in type '%s'"
                        inputCaseNameValue catchAllCaseName t.FullName |> invalidOp
                | Some foundIndex -> foundIndex

        let targetCaseFields, targetCaseCtor = union.cases.[targetCaseIndex].GetFields(), union.caseConstructor.[targetCaseIndex]
        targetCaseCtor (Union.mapTargetCaseArgs element options targetCaseFields) :?> 'T
