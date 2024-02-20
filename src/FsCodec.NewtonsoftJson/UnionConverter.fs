namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.Reflection

[<NoComparison; NoEquality>]
module private UnionInfo =

    /// Parallels F# behavior wrt how it generates a DU's underlying .NET Type
    let inline isInlinedIntoUnionItem (t: Type) =
        t = typeof<string>
        || t.IsValueType
        || t.IsArray
        || (t.IsGenericType && let g = t.GetGenericTypeDefinition() in typedefof<option<_>> = g || g.IsValueType) // Nullable<T>, ValueOption<T>

    let hasConverterCache = System.Collections.Concurrent.ConcurrentDictionary<Type, bool>()
    let typeHasConverterAttribute (t: Type) = hasConverterCache.GetOrAdd(t, fun t -> t.IsDefined(typeof<JsonConverterAttribute>, ``inherit`` = false))
    let isUnionCache = System.Collections.Concurrent.ConcurrentDictionary<Type, bool>()
    let typeIsUnionWithConverterAttribute t = isUnionCache.GetOrAdd(t, fun t -> FsCodec.Union.isUnion t && typeHasConverterAttribute t)

    let propTypeRequiresConstruction (propertyType: Type) =
        not (isInlinedIntoUnionItem propertyType)
        && not (typeHasConverterAttribute propertyType)

    /// Prepare arguments for the Case class ctor based on the kind of case and how F# maps that to a Type
    /// and/or whether we need to let json.net step in to convert argument types
    let mapTargetCaseArgs (inputJObject: JObject) serializer: PropertyInfo[] -> obj [] = function
        | [| singleCaseArg |] when propTypeRequiresConstruction singleCaseArg.PropertyType ->
            [| inputJObject.ToObject(singleCaseArg.PropertyType, serializer) |]
        | multipleFieldsInCustomCaseType ->
            [| for fi in multipleFieldsInCustomCaseType ->
                match inputJObject[fi.Name] with
                | null when
                    // Afford converters an opportunity to handle the missing field in the best way I can figure out to signal that
                    // The specific need being covered (see tests) is to ensure that, even with MissingMemberHandling=Ignore,
                    // the TypeSafeEnumConverter should reject missing values
                    // not having this case would go direct to `null` without passing go
                    typeHasConverterAttribute fi.PropertyType
                    || serializer.MissingMemberHandling = MissingMemberHandling.Error ->
                        // NB caller can opt out of erroring by setting NullValueHandling = NullValueHandling.Ignore)
                        // which renders the following equivalent to the next case
                        JToken.Parse("null").ToObject(fi.PropertyType, serializer)
                | null -> null
                | itemValue -> itemValue.ToObject(fi.PropertyType, serializer) |]

/// Serializes a discriminated union case with a single field that is a
/// record by flattening the record fields to the same level as the discriminator
type UnionConverter private (discriminator: string, ?catchAllCase) =
    inherit JsonConverter()

    new() = UnionConverter("case", ?catchAllCase = None)
    new(discriminator: string) = UnionConverter(discriminator, ?catchAllCase = None)
    new(discriminator: string, catchAllCase: string) = UnionConverter(discriminator, ?catchAllCase = match catchAllCase with null -> None | x -> Some x)

    override _.CanConvert(t: Type) = FsCodec.Union.isUnion t

    override _.WriteJson(writer: JsonWriter, value: obj, serializer: JsonSerializer) =
        writer.WriteStartObject()

        writer.WritePropertyName(discriminator)
        let case = (FsCodec.Union.Info.get (value.GetType())).getCase value
        writer.WriteValue(case.name)

        let fieldValues = case.deconstruct value
        match case.fields with
        | [| fi |] when not (UnionInfo.typeIsUnionWithConverterAttribute fi.PropertyType) ->
            match fieldValues[0] with
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
            for fieldInfo, fieldValue in Seq.zip case.fields fieldValues do
                if fieldValue <> null || serializer.NullValueHandling = NullValueHandling.Include then
                    writer.WritePropertyName(fieldInfo.Name)
                    serializer.Serialize(writer, fieldValue)

        writer.WriteEndObject()

    override _.ReadJson(reader: JsonReader, t: Type, _: obj, serializer: JsonSerializer) =
        let token = JToken.ReadFrom reader
        if token.Type <> JTokenType.Object then raise (FormatException(sprintf "Expected object token, got %O" token.Type))
        let inputJObject = token :?> JObject

        let targetCase =
            let findCaseNamed x = FsCodec.Union.Info.tryFindCaseWithName (FsCodec.Union.Info.get t) ((=) x)
            let inputCaseNameValue = inputJObject[discriminator] |> string
            match findCaseNamed inputCaseNameValue, catchAllCase with
            | None, None ->
                sprintf "No case defined for '%s', and no catchAllCase nominated for '%s' on type '%s'"
                    inputCaseNameValue typeof<UnionConverter>.Name t.FullName |> invalidOp
            | Some c, _ -> c
            | None, Some catchAllCaseName ->
                match findCaseNamed catchAllCaseName with
                | None ->
                    sprintf "No case defined for '%s', nominated catchAllCase: '%s' not found in type '%s'"
                        inputCaseNameValue catchAllCaseName t.FullName |> invalidOp
                | Some c -> c
        targetCase.construct(UnionInfo.mapTargetCaseArgs inputJObject serializer targetCase.fields)
