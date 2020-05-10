namespace FsCodec.SystemTextJson

open System
open System.Text.Json

[<AutoOpen>]
module private Prelude =
    /// Provides a thread-safe memoization wrapper for supplied function
    let memoize : ('T -> 'S) -> 'T -> 'S =
        fun f ->
            let cache = System.Collections.Concurrent.ConcurrentDictionary<'T, 'S>()
            fun t -> cache.GetOrAdd(t, f)

[<AbstractClass>]
type JsonPickler<'T>() =
    inherit Serialization.JsonConverter<'T>()

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

    abstract Read : reader: byref<Utf8JsonReader> * options: JsonSerializerOptions -> 'T

    override __.CanConvert t = isMatchingType t

    override __.Read(reader, _ : Type, opts) =
        __.Read(&reader, opts)

/// Json Converter that serializes based on an isomorphic type
[<AbstractClass>]
type JsonIsomorphism<'T, 'U>(?targetPickler : JsonPickler<'U>) =
    inherit JsonPickler<'T>()

    abstract Pickle   : 'T -> 'U
    abstract UnPickle : 'U -> 'T

    override __.Write(writer, source : 'T, options) =
        let target = __.Pickle source
        match targetPickler with
        | None -> JsonSerializer.Serialize(writer, target, options)
        | Some p -> p.Write(writer, target, options)

    override __.Read(reader, options) =
        let target =
            match targetPickler with
            | None -> JsonSerializer.Deserialize<'U>(&reader,options)
            | Some p -> p.Read(&reader, options)

        __.UnPickle target
