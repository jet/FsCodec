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

    let jnull = JsonSerializer.SerializeToElement null
    let converterOptions = UnionConverterOptions.get typeof<'T>
    let info = FsCodec.Union.Info.get typeof<'T>
    let findCase (t: Type) name =
        let inline findCaseNamed x = FsCodec.Union.Info.tryFindCaseWithName info ((=) x)
        match findCaseNamed name, converterOptions.CatchAllCase with
        | None, null ->
            sprintf "No case defined for '%s', and no catchAllCase nominated for '%s' on type '%s'"
                name typeof<UnionConverter<'T>>.Name t.FullName |> invalidOp
        | Some c, _ -> c
        | None, catchAllCaseName ->
            match findCaseNamed catchAllCaseName with
            | None ->
                sprintf "No case defined for '%s', nominated catchAllCase: '%s' not found in type '%s'"
                    name catchAllCaseName t.FullName |> invalidOp
            | Some c -> c

    override _.CanConvert t = t = typeof<'T> && FsCodec.Union.isUnion t

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
        let inline isSingle ({ fields = f } : FsCodec.Union.CaseInfo) =
            let shouldBindDirect pt = pt = typeof<JsonElement> || FSharpType.IsRecord(pt, true)
            if f.Length = 1 && shouldBindDirect f[0].PropertyType then ValueSome f[0].PropertyType else ValueNone
        let rejectMissingRecords propertyType name =
            if FSharpType.IsRecord(propertyType, true) then
                raise (JsonException <| sprintf "No property found for %s" name)
        let inline construct (case: FsCodec.Union.CaseInfo) args = case.construct args :?> 'T
        let inline des name propertyType (el: JsonElement) =
            if el.ValueKind = JsonValueKind.Null then rejectMissingRecords propertyType name
            JsonSerializer.Deserialize(el, propertyType, options)
        if reader.TokenType = JsonTokenType.String then // For upconversion from a TypeSafeEnum
            let case = reader.GetString() |> findCase t
            match isSingle case with
            | ValueSome pt -> [| System.Runtime.Serialization.FormatterServices.GetUninitializedObject pt |]
            | ValueNone -> [| for f in case.fields -> (*des f.Name f.PropertyType j*)null (*OR: jnull*) |]
            |> construct case
        elif reader.TokenType = JsonTokenType.StartObject then
            use doc = JsonDocument.ParseValue &reader
            let el = doc.RootElement
            let case = el.GetProperty converterOptions.DiscriminatorPropName |> string |> findCase t
            let propOrDefault (name: string) = let _found, propertyElement = el.TryGetProperty name in propertyElement
            // we deserialize direct from the full element if it's a record or the JsonElement catchall
            match isSingle case with
            | ValueSome pt -> [| des "Item" pt el |] |> construct case
            | ValueNone -> [| for x in case.fields -> propOrDefault x.Name |> des x.Name x.PropertyType |] |> construct case
        else raise (JsonException <| sprintf "Unexpected token when reading Union: %O" reader.TokenType)
