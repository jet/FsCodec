namespace FsCodec.SystemTextJson

open System
open System.Runtime.InteropServices
open System.Text.Json
open System.Text.Json.Serialization

type Options private () =

    static let defaultConverters : JsonConverter[] = [| |]

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
        if ignoreNulls then options.IgnoreNullValues <- true
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
            [<Optional; DefaultParameterValue(null)>] ?unsafeRelaxedJsonEscaping : bool) =

        Options.CreateDefault(
            converters = (match converters with null | [||] -> defaultConverters | xs -> Array.append defaultConverters xs),
            ?ignoreNulls = ignoreNulls,
            ?indent = indent,
            ?camelCase = camelCase,
            unsafeRelaxedJsonEscaping = defaultArg unsafeRelaxedJsonEscaping true)
