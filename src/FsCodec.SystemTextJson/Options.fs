namespace FsCodec.SystemTextJson

open System
open System.Runtime.InteropServices
open System.Text.Json
open System.Text.Json.Serialization

#nowarn "44" // see IgnoreNullValues below

[<AbstractClass; Sealed>]
type Options private () =

    /// <summary>Analogous to <c>JsonSerializerOptions.Default</c> - allows for sharing/caching of the default profile as defined by <c>Options.Create()</c></summary>
    static member val Default: JsonSerializerOptions = Options.Create()

    /// <summary>Creates a default set of serializer options used by Json serialization. When used with no args, same as <c>JsonSerializerOptions()</c></summary>
    static member CreateDefault
        (   [<Optional; ParamArray>] converters: JsonConverter[],
            // Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent: bool,
            // Render idiomatic camelCase for PascalCase items by using `PropertyNamingPolicy`/`DictionaryKeyPolicy = CamelCase`. Defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?camelCase: bool,
            // Ignore null values in input data, don't render fields with null values; defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls: bool,
            // Drop escaping of HTML-sensitive characters. defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?unsafeRelaxedJsonEscaping: bool) =
        let indent = defaultArg indent false
        let camelCase = defaultArg camelCase false
        let ignoreNulls = defaultArg ignoreNulls false
        let unsafeRelaxedJsonEscaping = defaultArg unsafeRelaxedJsonEscaping false

        let options = JsonSerializerOptions()
        if converters <> null then converters |> Array.iter options.Converters.Add
        if indent then options.WriteIndented <- true
        if camelCase then options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase; options.DictionaryKeyPolicy <- JsonNamingPolicy.CamelCase
        if ignoreNulls then options.IgnoreNullValues <- true // options.DefaultIgnoreCondition <- JsonIgnoreCondition.Always is outlawed so nowarn required
        if unsafeRelaxedJsonEscaping then options.Encoder <- System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        options

    /// <summary>Opinionated helper that creates serializer settings that represent good defaults for F# <br/>
    /// - no camel case conversion - assumption is you'll use records with camelCased names (which is the <c>System.Text.Json</c> default) <br/>
    /// - renders values with <c>UnsafeRelaxedJsonEscaping</c> - i.e. minimal escaping as per <c>Newtonsoft.Json</c> <br/>
    /// Everything else is as per <c>CreateDefault</c>, i.e. emit nulls instead of omitting fields, no indenting</summary>
    static member Create
        (   // List of converters to apply. Implicit converters may be prepended and/or be used as a default
            [<Optional; ParamArray>] converters: JsonConverter[],
            // Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent: bool,
            // Render idiomatic camelCase for PascalCase items by using `PropertyNamingPolicy`/`DictionaryKeyPolicy = CamelCase`.
            //  As with <c>NewtonsoftJson.Options</c>, defaults to false on basis that you'll use record and tuple field names that are already camelCase.
            //  NOTE this is also the <c>System.Text.Json</c> default (but <c>Newtonsoft.Json</c> does conversion by default out of the box)
            [<Optional; DefaultParameterValue(null)>] ?camelCase: bool,
            // Ignore null values in input data, don't render fields with null values; defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls: bool,
            // Drop escaping of HTML-sensitive characters. Defaults to `true` (NOTE this can represent a security concern).
            [<Optional; DefaultParameterValue(null)>] ?unsafeRelaxedJsonEscaping: bool,
            // Apply <c>TypeSafeEnumConverter</c> if possible. Defaults to <c>false</c>.
            [<Optional; DefaultParameterValue(null)>] ?autoTypeSafeEnumToJsonString: bool,
            // Apply <c>UnionConverter</c> for all Discriminated Unions, if <c>TypeSafeEnumConverter</c> not possible. Defaults to <c>false</c>.
            [<Optional; DefaultParameterValue(null)>] ?autoUnionToJsonObject: bool,
            // Apply <c>RejectNullStringConverter</c> in order to have serialization throw on <c>null</c> strings.
            // Use <c>string option</c> to represent strings that can potentially be <c>null</c>.
            [<Optional; DefaultParameterValue(null)>] ?rejectNullStrings: bool,
            // Apply <c>RejectNullConverter</c> in order to have serialization throw on <c>null</c> on <c>null</c> or missing <c>list</c> or <c>Set</c> values.
            // Wrap the type in <c>option</c> to represent values that can potentially be <c>null</c> or missing
            [<Optional; DefaultParameterValue(null)>] ?rejectNull: bool) =
        let autoTypeSafeEnumToJsonString = defaultArg autoTypeSafeEnumToJsonString false
        let autoUnionToJsonObject = defaultArg autoUnionToJsonObject false
        let rejectNullStrings = defaultArg rejectNullStrings false

        Options.CreateDefault(
            converters = [|
                if rejectNullStrings then RejectNullStringConverter()
                if defaultArg rejectNull false then RejectNullConverterFactory()
                if autoTypeSafeEnumToJsonString || autoUnionToJsonObject then
                    UnionOrTypeSafeEnumConverterFactory(typeSafeEnum = autoTypeSafeEnumToJsonString, union = autoUnionToJsonObject)
                if converters <> null then yield! converters |],
            ?ignoreNulls = ignoreNulls,
            ?indent = indent,
            ?camelCase = camelCase,
            unsafeRelaxedJsonEscaping = defaultArg unsafeRelaxedJsonEscaping true)
