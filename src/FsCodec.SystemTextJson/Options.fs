namespace FsCodec.SystemTextJson

open System
open System.Runtime.InteropServices
open System.Text.Json
open System.Text.Json.Serialization

#nowarn "44" // see IgnoreNullValues below

type Options private () =

    static let def = lazy Options.Create()

    /// <summary>Analogous to <c>JsonSerializerOptions.Default</c> - allows for sharing/caching of the default profile as defined by <c>Options.Create()</c></summary>
    static member Default : JsonSerializerOptions = def.Value

    /// Creates a default set of serializer options used by Json serialization. When used with no args, same as `JsonSerializerOptions()`
    static member CreateDefault
        (   [<Optional; ParamArray>] converters : JsonConverter[],
            /// Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent : bool,
            /// Render idiomatic camelCase for PascalCase items by using `PropertyNamingPolicy = CamelCase`. Defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?camelCase : bool,
            /// Ignore null values in input data, don't render fields with null values; defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls : bool,
            /// Drop escaping of HTML-sensitive characters. defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?unsafeRelaxedJsonEscaping : bool) =

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

    /// Opinionated helper that creates serializer settings that represent good defaults for F# <br/>
    /// - no camel case conversion - assumption is you'll use records with camelCased names <br/>
    /// - renders values with `UnsafeRelaxedJsonEscaping` - i.e. minimal escaping as per `NewtonsoftJson`<br/>
    /// Everything else is as per CreateDefault:- i.e. emit nulls instead of omitting fields, no indenting, no camelCase conversion
    static member Create
        (   /// List of converters to apply. Implicit converters may be prepended and/or be used as a default
            [<Optional; ParamArray>] converters : JsonConverter[],
            /// Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent : bool,
            /// Render idiomatic camelCase for PascalCase items by using `PropertyNamingPolicy = CamelCase`.
            ///  Defaults to false on basis that you'll use record and tuple field names that are camelCase (but thus not `CLSCompliant`).
            [<Optional; DefaultParameterValue(null)>] ?camelCase : bool,
            /// Ignore null values in input data, don't render fields with null values; defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls : bool,
            /// Drop escaping of HTML-sensitive characters. defaults to `true`.
            [<Optional; DefaultParameterValue(null)>] ?unsafeRelaxedJsonEscaping : bool,
            /// <summary>Apply <c>TypeSafeEnumConverter</c> if possible. Defaults to <c>false</c>.</summary>
            [<Optional; DefaultParameterValue(null)>] ?autoTypeSafeEnumToJsonString : bool,
            /// <summary>Apply <c>UnionConverter</c> for all Discriminated Unions, if <c>TypeSafeEnumConverter</c> not possible. Defaults to <c>false</c>.</summary>
            [<Optional; DefaultParameterValue(null)>] ?autoUnionToJsonObject : bool,
            /// <summary>When set to <c>false</c> the codec will throw on <c>null</c> strings. Use <c>string option</c> to allow nulls.
            [<Optional; DefaultParameterValue(null)>] ?allowNullStrings : bool) =

        let defaultConverters: JsonConverter array =
            if allowNullStrings = Some false then [| RejectNullStringConverter() |] else Array.empty
        let converters = if converters = null then defaultConverters else Array.append converters defaultConverters

        Options.CreateDefault(
            converters =
                (   match autoTypeSafeEnumToJsonString = Some true, autoUnionToJsonObject = Some true with
                    | tse, u when tse || u ->
                        Array.append converters [| UnionOrTypeSafeEnumConverterFactory(typeSafeEnum = tse, union = u) |]
                    | _ -> converters),
            ?ignoreNulls = ignoreNulls,
            ?indent = indent,
            ?camelCase = camelCase,
            unsafeRelaxedJsonEscaping = defaultArg unsafeRelaxedJsonEscaping true)
