namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System
open System.Runtime.InteropServices

[<AbstractClass; Sealed>]
type Options private () =

    /// <summary>Analogous to <c>System.Text.Json</c>'s <c>JsonSerializerOptions.Default</c> - allows for sharing/caching of the default profile as defined by <c>Options.Create()</c></summary>
    static member val Default : JsonSerializerSettings = Options.Create()

    /// Creates a default set of serializer settings used by Json serialization. When used with no args, same as JsonSerializerSettings.CreateDefault()
    /// With one difference - it inhibits the JSON.NET out of the box parsing of strings that look like dates (see https://github.com/JamesNK/Newtonsoft.Json/issues/862)
    static member CreateDefault
        (   [<Optional; ParamArray>] converters : JsonConverter[],
            // Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent : bool,
            // Render idiomatic camelCase for PascalCase items by using `CamelCasePropertyNamesContractResolver`. Defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?camelCase : bool,
            // Ignore null values in input data; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls : bool,
            // Error on missing values (as opposed to letting them just be default-initialized); defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?errorOnMissing : bool) =

        let indent = defaultArg indent false
        let camelCase = defaultArg camelCase false
        let ignoreNulls = defaultArg ignoreNulls false
        let errorOnMissing = defaultArg errorOnMissing false

        JsonSerializerSettings(
            ContractResolver = (if camelCase then CamelCasePropertyNamesContractResolver() : IContractResolver else DefaultContractResolver()),
            Converters = converters,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc, // Override default of RoundtripKind
            DateFormatHandling = DateFormatHandling.IsoDateFormat, // Pin Json.Net claimed default
            DateParseHandling = DateParseHandling.None, // Override hare-brained default of DateTime per https://github.com/JamesNK/Newtonsoft.Json/issues/862
            Formatting = (if indent then Formatting.Indented else Formatting.None),
            MissingMemberHandling = (if errorOnMissing then MissingMemberHandling.Error else MissingMemberHandling.Ignore),
            NullValueHandling = (if ignoreNulls then NullValueHandling.Ignore else NullValueHandling.Include))

    /// Opinionated helper that creates serializer settings that provide good defaults for F#
    /// - no camel case conversion - assumption is you'll use records with camelCased names
    /// - Always prepends an OptionConverter() to any converters supplied
    /// - everything else is as per CreateDefault:- i.e. emit nulls instead of omitting fields etc
    static member Create
        (   // List of converters to apply. An implicit OptionConverter() will be prepended and/or be used as a default
            [<Optional; ParamArray>] converters : JsonConverter[],
            // Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>] ?indent : bool,
            // Render idiomatic camelCase for PascalCase items by using `CamelCasePropertyNamesContractResolver`.
            //  Defaults to false on basis that you'll use record and tuple field names that are camelCase (and hence not `CLSCompliant`).
            [<Optional; DefaultParameterValue(null)>] ?camelCase : bool,
            // Ignore null values in input data; defaults to `false`.
            [<Optional; DefaultParameterValue(null)>] ?ignoreNulls : bool,
            // Error on missing values (as opposed to letting them just be default-initialized); defaults to false
            [<Optional; DefaultParameterValue(null)>] ?errorOnMissing : bool) =

        Options.CreateDefault(
            converters = [| OptionConverter()
                            match converters with null -> () | xs -> yield! xs |],
            ?ignoreNulls = ignoreNulls,
            ?errorOnMissing = errorOnMissing,
            ?indent = indent,
            ?camelCase = camelCase)

[<AbstractClass; Sealed>]
type StringEnumConverter private () =

    /// <summary>Creates a <c>StringEnumConverter</c>.
    /// <c>camelCase</c> option defaults to <c>false</c>.
    /// <c>allowIntegerValues</c> defaults to <c>false</c>. NOTE: Newtonsoft.Json default is: <c>true</c>.</summary>
    static member Create(?camelCase, ?allowIntegerValues) =
        let allowIntegers = defaultArg allowIntegerValues false
        if defaultArg camelCase false then Converters.StringEnumConverter(CamelCaseNamingStrategy(), allowIntegerValues = allowIntegers)
        else Converters.StringEnumConverter(AllowIntegerValues = allowIntegers)
