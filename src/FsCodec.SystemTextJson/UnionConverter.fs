namespace FsCodec.SystemTextJson

open FSharp.Reflection
open System
open System.Text.Json

[<Interface>]
type IUnionConverterOptions =
    abstract member Discriminator : string with get
    abstract member CatchAllCase : string option with get

/// <summary>Use this attribute in combination with a JsonConverter / UnionConverter attribute to specify
/// your own name for a discriminator and/or a catch-all case for a specific discriminated union.</summary>
/// <example><c>[JsonConverter typeof &lt; UnionConverter &lt; T &gt; &gt;); JsonUnionConverterOptions("type") &gt;]</c></example>
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = false, Inherited = false)>]
type JsonUnionConverterOptionsAttribute(discriminator : string) =
    inherit Attribute()
        member val CatchAllCase : string = null with get, set
    interface IUnionConverterOptions with
        member _.Discriminator = discriminator
        member x.CatchAllCase = Option.ofObj x.CatchAllCase

type private UnionConverterOptions =
    {   discriminator : string
        catchAllCase : string option }
    interface IUnionConverterOptions with
        member x.Discriminator = x.discriminator
        member x.CatchAllCase = x.catchAllCase

[<NoComparison; NoEquality>]
type private Union =
    {   cases : UnionCaseInfo[]
        tagReader : obj -> int
        fieldReader : (obj -> obj[])[]
        caseConstructor : (obj[] -> obj)[]
        options : IUnionConverterOptions option }

module private Union =

    let isUnion : Type -> bool = memoize (fun t -> FSharpType.IsUnion(t, true))
    // TOCONSIDER: could memoize this within the Info
    let unionHasJsonConverterAttribute = memoize (fun (t : Type) -> t.IsDefined(typeof<System.Text.Json.Serialization.JsonConverterAttribute>, true))

    let private createInfo t =
        let cases = FSharpType.GetUnionCases(t, true)
        {   cases = cases
            tagReader = FSharpValue.PreComputeUnionTagReader(t, true)
            fieldReader = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionReader(c, true))
            caseConstructor = cases |> Array.map (fun c -> FSharpValue.PreComputeUnionConstructor(c, true))
            options =
                t.GetCustomAttributes(typeof<JsonUnionConverterOptionsAttribute>, false)
                |> Array.tryHead // could be tryExactlyOne as AttributeUsage(AllowMultiple = false)
                |> Option.map (fun a -> a :?> IUnionConverterOptions) }
    let getInfo : Type -> Union = memoize createInfo

    /// Allows us to distinguish Unions that do not have bodies and hence should use a TypeSafeEnumConverter
    let hasOnlyNullaryCases (t : Type) =
        let union = getInfo t
        union.cases |> Seq.forall (fun case -> case.GetFields().Length = 0)

type UnionConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    static let defaultConverterOptions = { discriminator = "case"; catchAllCase = None } :> IUnionConverterOptions

    let getOptions union = defaultArg union.options defaultConverterOptions

    override _.CanConvert(t : Type) = t = typeof<'T> && Union.isUnion t

    override _.Write(writer, value, options) =
        let value = box value
        let union = Union.getInfo typeof<'T>
        let unionOptions = getOptions union
        let tag = union.tagReader value
        let case = union.cases[tag]
        let fieldValues = union.fieldReader[tag] value
        let fieldInfos = case.GetFields()

        writer.WriteStartObject()
        writer.WritePropertyName(unionOptions.Discriminator)
        writer.WriteStringValue(case.Name)
        for fieldInfo, fieldValue in Seq.zip fieldInfos fieldValues do
            if fieldValue <> null || options.DefaultIgnoreCondition <> Serialization.JsonIgnoreCondition.Always then
                let element = JsonSerializer.SerializeToElement(fieldValue, fieldInfo.PropertyType, options)
                if fieldInfos.Length = 1 && FSharpType.IsRecord(fieldInfo.PropertyType, true) then
                    // flatten the record properties into the same JSON object as the discriminator
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
        let union = Union.getInfo typeof<'T>
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

        let targetCaseFields, targetCaseCtor = union.cases[targetCaseIndex].GetFields(), union.caseConstructor[targetCaseIndex]
        let ctorArgs =
            [| for fieldInfo in targetCaseFields ->
                let t = fieldInfo.PropertyType
                let targetEl =
                    if targetCaseFields.Length = 1 && (t = typeof<JsonElement> || FSharpType.IsRecord(t, true)) then element
                    else let _found, el = element.TryGetProperty fieldInfo.Name in el
                JsonSerializer.Deserialize(targetEl, t, options) |]
        targetCaseCtor ctorArgs :?> 'T
