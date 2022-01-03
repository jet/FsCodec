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
    let getUnionCases = memoize (fun t -> FSharpType.GetUnionCases(t, true))

    let private createUnion t =
        let cases = getUnionCases t
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

    /// Parallels F# behavior wrt how it generates a DU's underlying .NET Type
    let inline isInlinedIntoUnionItem (t : Type) =
        t = typeof<string>
        || (t.IsValueType && t <> typeof<JsonElement>)
        || t.IsArray
        || (t.IsGenericType
           && (typedefof<Option<_>> = t.GetGenericTypeDefinition()
                || t.GetGenericTypeDefinition().IsValueType)) // Nullable<T>

    let typeHasJsonConverterAttribute = memoize (fun (t : Type) -> t.IsDefined(typeof<Serialization.JsonConverterAttribute>(*, false*)))
    let typeIsUnionWithConverterAttribute = memoize (fun (t : Type) -> isUnion t && typeHasJsonConverterAttribute t)

    let propTypeRequiresConstruction (propertyType : Type) =
        not (isInlinedIntoUnionItem propertyType)
        && not (typeHasJsonConverterAttribute propertyType)

    /// Prepare arguments for the Case class ctor based on the kind of case and how F# maps that to a Type
    /// and/or whether we need to defer to System.Text.Json
    let mapTargetCaseArgs (element : JsonElement) (options : JsonSerializerOptions) (props : PropertyInfo[]) : obj [] =
        match props with
        | [| singleCaseArg |] when propTypeRequiresConstruction singleCaseArg.PropertyType ->
            [| JsonSerializer.Deserialize(element, singleCaseArg.PropertyType, options) |]
        | multipleFieldsInCustomCaseType ->
            [| for fi in multipleFieldsInCustomCaseType ->
                match element.TryGetProperty fi.Name with
                | false, _ when fi.PropertyType.IsValueType -> Activator.CreateInstance fi.PropertyType
                | false, _ -> null
                | true, el when el.ValueKind = JsonValueKind.Null -> null
                | true, el -> JsonSerializer.Deserialize(el, fi.PropertyType, options) |]

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

        match fieldInfos with
        | [| fi |] when not (Union.typeIsUnionWithConverterAttribute fi.PropertyType) ->
            match fieldValues.[0] with
            | null when options.DefaultIgnoreCondition = Serialization.JsonIgnoreCondition.Always -> ()
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
                if fieldValue <> null || options.DefaultIgnoreCondition <> Serialization.JsonIgnoreCondition.Always  then
                    writer.WritePropertyName(fieldInfo.Name)
                    let element = JsonSerializer.SerializeToElement(fieldValue, fieldInfo.PropertyType, options)
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
