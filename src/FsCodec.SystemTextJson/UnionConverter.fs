namespace FsCodec.SystemTextJson

open FSharp.Reflection
open System
open System.Text.Json

/// <summary>Use this attribute in combination with a JsonConverter / UnionConverter attribute to specify
/// your own name for a discriminator and/or a catch-all case for a specific discriminated union.</summary>
/// <example><c>[&lt;JsonConverter(typeof&lt;UnionConverter&lt;T&gt;&gt;); JsonUnionConverterOptions("type")&gt;]</c></example>
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct, AllowMultiple = false, Inherited = false)>]
type JsonUnionConverterOptionsAttribute(discriminator: string) =
    inherit Attribute()
    member val internal DiscriminatorPropName = discriminator
    member val CatchAllCase: string = null with get, set

module private UnionConverterOptions =
    let private defaultOptions = JsonUnionConverterOptionsAttribute("case", CatchAllCase = null)
    let get (t: Type) =
        match t.GetCustomAttributes(typeof<JsonUnionConverterOptionsAttribute>, false) with
        | [||] -> defaultOptions
        | xs -> Array.exactlyOne xs :?> _ // AttributeUsage(AllowMultiple = false)

type UnionConverter<'T>() =
    inherit Serialization.JsonConverter<'T>()

    let converterOptions = UnionConverterOptions.get typeof<'T>
    let info = FsCodec.Union.Info.get typeof<'T>

    override _.CanConvert(t: Type) = t = typeof<'T> && FsCodec.Union.isUnion t

    override _.Write(writer: Utf8JsonWriter, value, options: JsonSerializerOptions) =
        let value = box value
        writer.WriteStartObject()
        writer.WritePropertyName(converterOptions.DiscriminatorPropName)
        let case = info.getCase value
        writer.WriteStringValue(case.name)
        let fieldValues = case.deconstruct value
        for fieldInfo, fieldValue in Seq.zip case.fields fieldValues do
            if fieldValue <> null || options.DefaultIgnoreCondition <> Serialization.JsonIgnoreCondition.Always then
                let element = JsonSerializer.SerializeToElement(fieldValue, fieldInfo.PropertyType, options)
                if case.fields.Length = 1 && FSharpType.IsRecord(fieldInfo.PropertyType, true) then
                    // flatten the record properties into the same JSON object as the discriminator
                    for prop in element.EnumerateObject() do
                        prop.WriteTo writer
                else
                    writer.WritePropertyName(fieldInfo.Name)
                    element.WriteTo writer
        writer.WriteEndObject()

    override _.Read(reader, t: Type, options) =
        if reader.TokenType <> JsonTokenType.StartObject then
            sprintf "Unexpected token when reading Union: %O" reader.TokenType |> JsonException |> raise
        use document = JsonDocument.ParseValue &reader
        let element = document.RootElement

        let case =
            let inputCaseNameValue = element.GetProperty converterOptions.DiscriminatorPropName |> string
            let findCaseNamed x = FsCodec.Union.Info.tryFindCaseWithName info ((=) x)
            match findCaseNamed inputCaseNameValue, converterOptions.CatchAllCase  with
            | None, null ->
                sprintf "No case defined for '%s', and no catchAllCase nominated for '%s' on type '%s'"
                    inputCaseNameValue typeof<UnionConverter<_>>.Name t.FullName |> invalidOp
            | Some c, _ -> c
            | None, catchAllCaseName ->
                match findCaseNamed catchAllCaseName with
                | None ->
                    sprintf "No case defined for '%s', nominated catchAllCase: '%s' not found in type '%s'"
                        inputCaseNameValue catchAllCaseName t.FullName |> invalidOp
                | Some c -> c
        let ctorArgs =
            [| for fieldInfo in case.fields ->
                let ft = fieldInfo.PropertyType
                let targetEl =
                    if case.fields.Length = 1 && (ft = typeof<JsonElement> || FSharpType.IsRecord(ft, true)) then element
                    else let _found, el = element.TryGetProperty fieldInfo.Name in el
                JsonSerializer.Deserialize(targetEl, ft, options) |]
        case.construct ctorArgs :?> 'T
