namespace Newtonsoft.Json.Converters.FSharp

open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System
open System.Runtime.InteropServices

[<AutoOpen>]
module private Prelude =
    /// Provides a thread-safe memoization wrapper for supplied function
    let memoize : ('T -> 'S) -> 'T -> 'S =
        fun f ->
            let cache = new System.Collections.Concurrent.ConcurrentDictionary<'T, 'S>()
            fun t -> cache.GetOrAdd(t, f)

[<AbstractClass>]
type JsonPickler<'T>() =
    inherit JsonConverter()

    static let isMatchingType =
        let rec isMatching (ts : Type list) =
            match ts with
            | [] -> false
            | t :: _ when t = typeof<'T> -> true
            | t :: tl ->
                let tail =
                    [ match t.BaseType with null -> () | bt -> yield bt
                      yield! t.GetInterfaces()
                      yield! tl ]

                isMatching tail

        memoize (fun t -> isMatching [t])

    abstract Write : writer:JsonWriter * serializer:JsonSerializer * source:'T  -> unit
    abstract Read : reader:JsonReader * serializer:JsonSerializer -> 'T

    override __.CanConvert t = isMatchingType t

    override __.CanRead = true
    override __.CanWrite = true

    override __.WriteJson(writer, source : obj, serialize : JsonSerializer) =
        __.Write(writer, serialize, source :?> 'T)

    override __.ReadJson(reader : JsonReader, _, _, serializer : JsonSerializer) =
        __.Read(reader, serializer) :> obj

/// Json Converter that serializes based on an isomorphic type
[<AbstractClass>]
type JsonIsomorphism<'T, 'U>(?targetPickler : JsonPickler<'U>) =
    inherit JsonPickler<'T>()

    abstract Pickle   : 'T -> 'U
    abstract UnPickle : 'U -> 'T

    override __.Write(writer:JsonWriter, serializer:JsonSerializer, source:'T) =
        let target = __.Pickle source
        match targetPickler with
        | None -> serializer.Serialize(writer, target, typeof<'U>)
        | Some p -> p.Write(writer, serializer, target)

    override __.Read(reader:JsonReader, serializer:JsonSerializer) =
        let target =
            match targetPickler with
            | None -> serializer.Deserialize<'U>(reader)
            | Some p -> p.Read(reader, serializer)

        __.UnPickle target

type Settings private () =
    /// <summary>
    ///     Creates a default serializer settings used by Json serialization. When used with no args, same as JsonSerializerSettings.CreateDefault()
    /// </summary>
    /// <param name="camelCase">Render idiomatic camelCase for PascalCase items by using `CamelCasePropertyNamesContractResolver`. Defaults to false.</param>
    /// <param name="indent">Use multi-line, indented formatting when serializing json; defaults to false.</param>
    /// <param name="ignoreNulls">Ignore null values in input data; defaults to true. NB OOTB, Json.Net defaults to false.</param>
    /// <param name="errorOnMissing">Error on missing values (as opposed to letting them just be default-initialized); defaults to false.</param>
    // TODO in v2, Create should be renamed to CreateDefault as it now aligns with Newtonsoft.Json defaults (as opposed the current method with that name)
    static member Create
        (   [<Optional;ParamArray>]converters : JsonConverter[],
            [<Optional;DefaultParameterValue(null)>]?indent : bool,
            [<Optional;DefaultParameterValue(null)>]?camelCase : bool,
            [<Optional;DefaultParameterValue(null)>]?ignoreNulls : bool,
            [<Optional;DefaultParameterValue(null)>]?errorOnMissing : bool) =
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

    [<Obsolete("Provides backcompat with ill-considered defaults; for new code, CreateCorrect is recommended. For existing code, use Create(ignoreNulls=true, camelCase=true)")>]
    // TODO in v2, Create should be renamed to CreateDefault as it aligns with Newtonsoft.Json defaults, as provided by that method
    static member CreateDefault
        (   [<Optional;DefaultParameterValue(null)>]?indent : bool,
            [<Optional;DefaultParameterValue(null)>]?camelCase : bool,
            [<Optional;DefaultParameterValue(null)>]?ignoreNulls : bool,
            [<Optional;DefaultParameterValue(null)>]?errorOnMissing : bool) =
        let camelCase = defaultArg camelCase true
        let ignoreNulls = defaultArg ignoreNulls true
        Settings.Create(?indent=indent, camelCase=camelCase, ignoreNulls=ignoreNulls, ?errorOnMissing=errorOnMissing)

    /// <summary>
    ///     Optionated helper that creates a set of serializer settings that fails fast, providing less surprises when working in F#.
    ///     Recommended only for greenfield areas of a system that have not leaned on NullValueHandling.Ignore
    /// </summary>
    /// <param name="camelCase">
    ///     Render idiomatic camelCase for PascalCase items by using `CamelCasePropertyNamesContractResolver`.
    ///     Defaults to false for this profile on basis that you'll use record and tuple field names that are camelCase (and hence not `CLSCompliant`).</param>
    /// <param name="indent">Use multi-line, indented formatting when serializing json; defaults to false.</param>
    /// <param name="ignoreNulls">Ignore null values in input data; defaults to true. NB OOTB, Json.Net defaults to false.</param>
    /// <param name="errorOnMissing">Error on missing values (as opposed to letting them just be default-initialized); defaults to false.</param>
    static member CreateCorrect
        (   [<Optional;ParamArray>]converters : JsonConverter[],
            [<Optional;DefaultParameterValue(null)>]?indent : bool,
            [<Optional;DefaultParameterValue(null)>]?camelCase : bool,
            [<Optional;DefaultParameterValue(null)>]?ignoreNulls : bool,
            [<Optional;DefaultParameterValue(null)>]?errorOnMissing : bool) =
        Settings.Create(
            converters=converters,
            // the key impact of this is that Nullables/options start to render as absent (same for strings etc)
            ignoreNulls=defaultArg ignoreNulls true,
            ?errorOnMissing=errorOnMissing,
            ?indent=indent,
            ?camelCase=camelCase)