namespace FsCodec.SystemTextJson

open FSharp.Reflection
open System
open System.Text.Json

[<Interface>]
type IUnionConverterOptions =
    abstract member Discriminator: string with get
    abstract member CatchAllCase: string option with get

/// <summary>Use this attribute in combination with a JsonConverter / UnionConverter attribute to specify
/// your own name for a discriminator and/or a catch-all case for a specific discriminated union.</summary>
/// <example><c>[JsonConverter typeof &lt; UnionConverter &lt; T &gt; &gt;); JsonUnionConverterOptions("type") &gt;]</c></example>
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = false, Inherited = false)>]
type JsonUnionConverterOptionsAttribute(discriminator : string) =
    inherit Attribute()
        member val CatchAllCase: string = null with get, set
    interface IUnionConverterOptions with
        member _.Discriminator = discriminator
        member x.CatchAllCase = Option.ofObj x.CatchAllCase

type private UnionConverterOptions =
    {   discriminator: string
        catchAllCase: string option }
    interface IUnionConverterOptions with
        member x.Discriminator = x.discriminator
        member x.CatchAllCase = x.catchAllCase

[<NoComparison; NoEquality>]
type private UnionInfo =
    {   fieldReader: (obj -> obj[])[]
        options: IUnionConverterOptions option }
module private UnionInfo =
    let get: Type -> UnionInfo = memoize (fun t ->
        let i = FsCodec.Union.Info.get t
        {   fieldReader = i.cases |> Array.map (fun c -> FSharpValue.PreComputeUnionReader(c, true))
            options =
                t.GetCustomAttributes(typeof<JsonUnionConverterOptionsAttribute>, false)
                |> Array.tryHead // could be tryExactlyOne as AttributeUsage(AllowMultiple = false)
                |> Option.map (fun a -> a :?> IUnionConverterOptions) })
    let internal hasConverterAttribute = memoize (fun (t: Type) -> t.IsDefined(typeof<Serialization.JsonConverterAttribute>, true))

type UnionConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    static let defaultConverterOptions = { discriminator = "case"; catchAllCase = None } :> IUnionConverterOptions

    let getOptions union = defaultArg union.options defaultConverterOptions

    override _.CanConvert(t : Type) = t = typeof<'T> && FsCodec.Union.isUnion t

    override _.Write(writer, value, options) =
        let value = box value
        let ui = FsCodec.Union.Info.get typeof<'T>
        let tag = ui.tagReader value
        let case = ui.cases[tag]

        writer.WriteStartObject()
        let u = UnionInfo.get typeof<'T>
        let unionOptions = getOptions u
        writer.WritePropertyName(unionOptions.Discriminator)
        writer.WriteStringValue(case.Name)
        let fieldValues = u.fieldReader[tag] value
        let fieldInfos = case.GetFields()
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
        let u = UnionInfo.get typeof<'T>
        let ui = FsCodec.Union.Info.get typeof<'T>
        let unionOptions = getOptions u
        let element = document.RootElement

        let targetCaseTag =
            let inputCaseNameValue = element.GetProperty unionOptions.Discriminator |> string
            let findCaseNamed x = ui.cases |> Array.tryFindIndex (fun case -> case.Name = x)
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

        let targetCaseFields, targetCaseCtor = ui.cases[targetCaseTag].GetFields(), ui.caseConstructor[targetCaseTag]
        let ctorArgs =
            [| for fieldInfo in targetCaseFields ->
                let t = fieldInfo.PropertyType
                let targetEl =
                    if targetCaseFields.Length = 1 && (t = typeof<JsonElement> || FSharpType.IsRecord(t, true)) then element
                    else let _found, el = element.TryGetProperty fieldInfo.Name in el
                JsonSerializer.Deserialize(targetEl, t, options) |]
        targetCaseCtor ctorArgs :?> 'T
