namespace FsCodec.SystemTextJson

open System.Text.Json

[<AutoOpen>]
module private Prelude =
    let memoize (f: 'T -> 'S): 'T -> 'S =
        let cache = new System.Collections.Concurrent.ConcurrentDictionary<'T, 'S>()
        fun t -> cache.GetOrAdd(t, f)

[<AbstractClass>]
type JsonPickler<'T>() =
    inherit Serialization.JsonConverter<'T>()

    static let isMatchingType =
        let rec isMatching = function
            | [] -> false
            | t :: _ when t = typeof<'T> -> true
            | t :: tl ->
                let tail =
                    [ match t.BaseType with null -> () | bt -> yield bt
                      yield! t.GetInterfaces()
                      yield! tl ]
                isMatching tail
        memoize (fun t -> isMatching [t])

    abstract Read: reader: byref<Utf8JsonReader> * options: JsonSerializerOptions -> 'T

    override _.CanConvert t = isMatchingType t

    override x.Read(reader, _typeToConvert, options) =
        x.Read(&reader, options)

/// Json Converter that serializes based on an isomorphic type
[<AbstractClass>]
type JsonIsomorphism<'T, 'U>(?targetPickler: JsonPickler<'U>) =
    inherit JsonPickler<'T>()

    abstract Pickle: 'T -> 'U
    abstract UnPickle: 'U -> 'T

    override x.Write(writer, source: 'T, options) =
        let target = x.Pickle source
        match targetPickler with
        | None -> JsonSerializer.Serialize(writer, target, options)
        | Some p -> p.Write(writer, target, options)
    override x.Read(reader, options): 'T =
        let target =
            match targetPickler with
            | None -> JsonSerializer.Deserialize<'U>(&reader, options)
            | Some p -> p.Read(&reader, options)
        x.UnPickle target
