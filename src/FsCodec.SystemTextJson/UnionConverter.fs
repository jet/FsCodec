namespace FsCodec.SystemTextJson

open FSharp.Reflection
open System
open System.Reflection
open System.Text.Json

type IUnionConverterOptions =
    abstract member Discriminator : string with get
    abstract member CatchAllCase : string option with get

/// Use this attribute in combination with a JsonConverter/UnionConverter attribute to specify
/// your own name for a discriminator and/or a catch-all case for a specific discriminated union.
/// If this attribute is set, its values take precedence over the values set on the converter via its constructor.
/// Example: <c>[<JsonConverter(typeof<UnionConverter<T>>); JsonUnionConverterOptions("type")>]</c>
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = false, Inherited = false)>]
type JsonUnionConverterOptionsAttribute(discriminator : string) =
    inherit Attribute()
        member val CatchAllCase : string = null with get, set
    interface IUnionConverterOptions with
        member _.Discriminator = discriminator
        member x.CatchAllCase = Option.ofObj x.CatchAllCase

type UnionConverterOptions =
    {
        discriminator : string
        catchAllCase : string option
    }
    interface IUnionConverterOptions with
        member x.Discriminator = x.discriminator
        member x.CatchAllCase = x.catchAllCase

[<NoComparison; NoEquality>]
type private Union =
    {
        cases : UnionCaseInfo[]
        tagReader : obj -> int
        fieldReader : (obj -> obj[])[]
        caseConstructor : (obj[] -> obj)[]
        options : IUnionConverterOptions option
    }

module private Union =

    let isUnion : Type -> bool = memoize (fun t -> FSharpType.IsUnion(t, true))

    let private createUnion t =
        let cases = FSharpType.GetUnionCases(t, true)
        {
            cases = cases
            tagReader = FSharpValue.PreComputeUnionTagReader(t, true)
            fieldReader = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionReader(c, true))
            caseConstructor = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionConstructor(c, true))
            options =
                t.GetCustomAttributes(typeof<JsonUnionConverterOptionsAttribute>, false)
                |> Array.tryHead // AttributeUsage(AllowMultiple = false)
                |> Option.map (fun a -> a :?> IUnionConverterOptions)
        }
    let getUnion : Type -> Union = memoize createUnion

    /// Allows us to distinguish between Unions that have bodies and hence should UnionConverter
    let hasOnlyNullaryCases (t : Type) =
        let union = getUnion t
        union.cases |> Seq.forall (fun case -> case.GetFields().Length = 0)

    /// Parallels F# behavior wrt how it generates a DU's underlying .NET Type
    let inline isInlinedIntoUnionItem (t : Type) =
        t = typeof<string>
        || (t.IsValueType && t <> typeof<JsonElement>)
        || t.IsArray
        || (t.IsGenericType
           && (typedefof<Option<_>> = t.GetGenericTypeDefinition()
                || t.GetGenericTypeDefinition().IsValueType)) // Nullable<T>

    let private typeHasJsonConverterAttribute_ (t : Type) = t.IsDefined(typeof<Serialization.JsonConverterAttribute>(*, false*))
    let typeHasJsonConverterAttribute : Type -> bool = memoize typeHasJsonConverterAttribute_

    /// Prepare arguments for the Case class ctor based on the kind of case and how F# maps that to a Type
    /// and/or whether we need to defer to System.Text.Json
    let mapTargetCaseArgs (element : JsonElement) (options : JsonSerializerOptions) (props : PropertyInfo[]) : obj [] =
        [| for fi in props ->
            match element.TryGetProperty fi.Name with
            | false, _ when props.Length = 1 && not fi.PropertyType.IsValueType && element.ValueKind = JsonValueKind.Object ->
                JsonSerializer.Deserialize(element, fi.PropertyType, options)
            | false, _ when props.Length = 1 && isInlinedIntoUnionItem fi.PropertyType ->
                JsonSerializer.Deserialize(element, fi.PropertyType, options)
            | false, _ ->
                failwithf "NF %d %s %b" props.Length fi.Name (isInlinedIntoUnionItem fi.PropertyType)
//                JsonSerializer.Deserialize(el, fi.PropertyType, options)
            | true, el when props.Length <> 1 ->
                JsonSerializer.Deserialize(el, fi.PropertyType, options)
            | true, el when props.Length = 1 && typeHasJsonConverterAttribute fi.PropertyType ->
                JsonSerializer.Deserialize(el, fi.PropertyType, options)
            | true, el when props.Length = 1 ->
                JsonSerializer.Deserialize(element, fi.PropertyType, options)
            | true, el when props.Length = 1 && not (isInlinedIntoUnionItem fi.PropertyType) ->
                JsonSerializer.Deserialize(el, fi.PropertyType, options)
            | true, el when props.Length = 1 && isInlinedIntoUnionItem fi.PropertyType ->
//                failwithf "NF2 %d %s %b" props.Length fi.Name (isInlinedIntoUnionItem fi.PropertyType)
//                failwithf "NF2 %d %s %b" props.Length fi.Name fi.PropertyType
//                failwithf "NF2 %d %s %b" props.Length fi.Name (isInlinedIntoUnionItem fi.PropertyType)
                JsonSerializer.Deserialize(el, fi.PropertyType, options)
//            | true, el when props.Length = 1 && isInlinedIntoUnionItem fi.PropertyType ->
//                JsonSerializer.Deserialize(element, fi.PropertyType, options)
//                failwithf "NF2 %d %s %b" props.Length fi.Name (isInlinedIntoUnionItem fi.PropertyType)
            | true, el ->
                failwithf "NF2 %d %s %b" props.Length fi.Name (isInlinedIntoUnionItem fi.PropertyType) |]
//                let el = if props.Length = 1 && isInlinedIntoUnionItem fi.PropertyType then el else element
//                JsonSerializer.Deserialize(element, fi.PropertyType, options) |]

type UnionConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    static let defaultConverterOptions = { discriminator = "case"; catchAllCase = None } :> IUnionConverterOptions

    let getOptions union = defaultArg union.options defaultConverterOptions

    override _.CanConvert(t : Type) = t = typeof<'T> && Union.isUnion t

    override _.Write(writer, value, options) =
        let value = box value
        let union = Union.getUnion typeof<'T>
        let unionOptions = getOptions union
        let tag = union.tagReader value
        let case = union.cases.[tag]
        let fieldValues = union.fieldReader.[tag] value
        let fieldInfos = case.GetFields()

        writer.WriteStartObject()
        writer.WritePropertyName(unionOptions.Discriminator)
        writer.WriteStringValue(case.Name)
        for fieldInfo, fieldValue in Seq.zip fieldInfos fieldValues do
            if fieldValue <> null || options.DefaultIgnoreCondition <> Serialization.JsonIgnoreCondition.Always then
                let element = JsonSerializer.SerializeToElement(fieldValue, fieldInfo.PropertyType, options)
                if fieldInfos.Length = 1 && element.ValueKind = JsonValueKind.Object && Union.isInlinedIntoUnionItem fieldInfo.PropertyType then
                    // flatten the object properties into the same one as the discriminator
                    for prop in element.EnumerateObject() do
                        prop.WriteTo writer
                else
                    writer.WritePropertyName(fieldInfo.Name)
                    element.WriteTo writer
        writer.WriteEndObject()

    override _.Read(reader, t : Type, options) =
        if reader.TokenType <> JsonTokenType.StartObject then
            sprintf "Unexpected token when reading Union: %O" reader.TokenType |> JsonException |> raise
        use document = JsonDocument.ParseValue &reader
        let union = Union.getUnion typeof<'T>
        let unionOptions = getOptions union
        let element = document.RootElement

        let targetCaseIndex =
            let inputCaseNameValue = element.GetProperty unionOptions.Discriminator |> string
            let findCaseNamed x = union.cases |> Array.tryFindIndex (fun case -> case.Name = x)
            match findCaseNamed inputCaseNameValue, unionOptions.CatchAllCase  with
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
