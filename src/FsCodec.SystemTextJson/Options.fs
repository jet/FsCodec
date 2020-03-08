namespace FsCodec.SystemTextJson

open System
open System.Runtime.InteropServices
open System.Text.Json
open System.Text.Json.Serialization

type Options private () =

    static let defaultConverters : JsonConverter[] =
        [|  Converters.JsonOptionConverter()
            Converters.JsonRecordConverter() |]

    /// Creates a default set of serializer options used by Json serialization. When used with no args, same as `JsonSerializerOptions()`
    static member CreateDefault
        (   [<Optional; ParamArray>] converters : JsonConverter[],
            /// Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent : bool,
            /// Render idiomatic camelCase for PascalCase items by using `PropertyNamingPolicy = CamelCase`. Defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?camelCase : bool,
            /// Ignore null values in input data; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls : bool) =

        let indent = defaultArg indent false
        let camelCase = defaultArg camelCase false
        let ignoreNulls = defaultArg ignoreNulls false
        let options = JsonSerializerOptions()
        if converters <> null then converters |> Array.iter options.Converters.Add
        if indent then options.WriteIndented <- true
        if camelCase then options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase; options.DictionaryKeyPolicy <- JsonNamingPolicy.CamelCase
        if ignoreNulls then options.IgnoreNullValues <- true
        options

    /// Opinionated helper that creates serializer settings that provide good defaults for F#
    /// - Always prepends `[JsonOptionConverter(); JsonRecordConverter()]` to any converters supplied
    /// - no camel case conversion - assumption is you'll use records with camelCased names
    /// Everything else is as per CreateDefault:- i.e. emit nulls instead of omitting fields, no indenting, no camelCase conversion
    static member Create
        (   /// List of converters to apply. Implicit [JsonOptionConverter(); JsonRecordConverter()] will be prepended and/or be used as a default
            [<Optional; ParamArray>] converters : JsonConverter[],
            /// Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent : bool,
            /// Render idiomatic camelCase for PascalCase items by using `PropertyNamingPolicy = CamelCase`.
            ///  Defaults to false on basis that you'll use record and tuple field names that are camelCase (but thus not `CLSCompliant`).
            [<Optional; DefaultParameterValue(null)>] ?camelCase : bool,
            /// Ignore null values in input data; defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls : bool) =

        Options.CreateDefault(
            converters = (match converters with null | [||] -> defaultConverters | xs -> Array.append defaultConverters xs),
            ?ignoreNulls = ignoreNulls,
            ?indent = indent,
            ?camelCase = camelCase)
