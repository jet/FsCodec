﻿namespace Newtonsoft.Json.Converters.FSharp

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
    ///     Creates a default serializer settings used by Json serialization
    /// </summary>
    /// <param name="camelCase">Render idiomatic camelCase for PascalCase items by using `CamelCasePropertyNamesContractResolver`. Defaults to true.</param>
    /// <param name="indent">Use multi-line, indented formatting when serializing json; defaults to false.</param>
    /// <param name="ignoreNulls">Ignore null values in input data; defaults to true. NB OOTB, Json.Net defaults to false.</param>
    /// <param name="errorOnMissing">Error on missing values (as opposed to letting them just be default-initialized); defaults to false.</param>
    static member CreateDefault
        (   [<Optional;DefaultParameterValue(null)>]?indent : bool,
            [<Optional;DefaultParameterValue(null)>]?camelCase : bool,
            [<Optional;DefaultParameterValue(null)>]?ignoreNulls : bool,
            [<Optional;DefaultParameterValue(null)>]?errorOnMissing : bool) =
        let indent = defaultArg indent false
        let camelCase = defaultArg camelCase true
        let ignoreNulls = defaultArg ignoreNulls true
        let errorOnMissing = defaultArg errorOnMissing false
        let resolver : IContractResolver =
             if camelCase then CamelCasePropertyNamesContractResolver() :> _
             else DefaultContractResolver() :> _
        JsonSerializerSettings(
            ContractResolver = resolver,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = (if indent then Formatting.Indented else Formatting.None),
            MissingMemberHandling = (if errorOnMissing then MissingMemberHandling.Error else MissingMemberHandling.Ignore),
            NullValueHandling = (if ignoreNulls then NullValueHandling.Ignore else NullValueHandling.Include))