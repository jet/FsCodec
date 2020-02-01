namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System
open System.Runtime.InteropServices

type Settings private () =

    static let defaultConverters : JsonConverter[] = [| OptionConverter() |]

    /// Creates a default set of serializer settings used by Json serialization. When used with no args, same as JsonSerializerSettings.CreateDefault()
    static member CreateDefault
        (   [<Optional; ParamArray>]converters : JsonConverter[],
            /// Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>]?indent : bool,
            /// Render idiomatic camelCase for PascalCase items by using `CamelCasePropertyNamesContractResolver`. Defaults to false.
            [<Optional; DefaultParameterValue(null)>]?camelCase : bool,
            /// Ignore null values in input data; defaults to false.
            [<Optional; DefaultParameterValue(null)>]?ignoreNulls : bool,
            /// Error on missing values (as opposed to letting them just be default-initialized); defaults to false.
            [<Optional; DefaultParameterValue(null)>]?errorOnMissing : bool) =
        let indent = defaultArg indent false
        let camelCase = defaultArg camelCase false
        let ignoreNulls = defaultArg ignoreNulls false
        let errorOnMissing = defaultArg errorOnMissing false
        let resolver : IContractResolver =
             if camelCase then CamelCasePropertyNamesContractResolver() :> _
             else DefaultContractResolver() :> _
        JsonSerializerSettings(
            ContractResolver = resolver,
            Converters = converters,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc, // Override default of RoundtripKind
            DateFormatHandling = DateFormatHandling.IsoDateFormat, // Pin Json.Net claimed default
            Formatting = (if indent then Formatting.Indented else Formatting.None),
            MissingMemberHandling = (if errorOnMissing then MissingMemberHandling.Error else MissingMemberHandling.Ignore),
            NullValueHandling = (if ignoreNulls then NullValueHandling.Ignore else NullValueHandling.Include))

    /// Optionated helper that creates serializer settings that provide good defaults for F#
    /// - no camel case conversion - assumption is you'll use records with camelCased names
    /// - Always prepends an OptionConverter() to any converters supplied
    /// - everything else is as per CreateDefault:- i.e. emit nulls instead of omitting fields etc
    static member Create
        (   /// List of converters to apply. An implicit OptionConverter() will be prepended and/or be used as a default
            [<Optional; ParamArray>]converters : JsonConverter[],
            /// Use multi-line, indented formatting when serializing JSON; defaults to false.
            [<Optional; DefaultParameterValue(null)>]?indent : bool,
            /// Render idiomatic camelCase for PascalCase items by using `CamelCasePropertyNamesContractResolver`.
            ///  Defaults to false on basis that you'll use record and tuple field names that are camelCase (and hence not `CLSCompliant`).
            [<Optional; DefaultParameterValue(null)>]?camelCase : bool,
            /// Ignore null values in input data; defaults to `false`.
            [<Optional; DefaultParameterValue(null)>]?ignoreNulls : bool,
            /// Error on missing values (as opposed to letting them just be default-initialized); defaults to false
            [<Optional; DefaultParameterValue(null)>]?errorOnMissing : bool) =
        Settings.CreateDefault(
            converters = (match converters with null | [||] -> defaultConverters | xs -> Array.append defaultConverters xs),
            ?ignoreNulls = ignoreNulls,
            ?errorOnMissing = errorOnMissing,
            ?indent = indent,
            ?camelCase = camelCase)
