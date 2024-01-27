namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System

[<AutoOpen>]
module private Prelude =
    let memoize (f: 'T -> 'S): 'T -> 'S =
        let cache = new System.Collections.Concurrent.ConcurrentDictionary<'T, 'S>()
        fun t -> cache.GetOrAdd(t, f)

[<AbstractClass>]
type JsonPickler<'T>() =
    inherit JsonConverter()

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

    abstract Write: writer: JsonWriter * serializer: JsonSerializer * source: 'T  -> unit
    abstract Read: reader: JsonReader * serializer: JsonSerializer -> 'T

    override _.CanConvert t = isMatchingType t
    override _.CanRead = true
    override _.CanWrite = true

    override x.WriteJson(writer: JsonWriter, value: obj, serializer: JsonSerializer) =
        x.Write(writer, serializer, value :?> 'T)
    override x.ReadJson(reader: JsonReader, _objectType, _existingValue: obj, serializer: JsonSerializer) =
        x.Read(reader, serializer) :> obj

/// Json Converter that serializes based on an isomorphic type
[<AbstractClass>]
type JsonIsomorphism<'T, 'U>(?targetPickler: JsonPickler<'U>) =
    inherit JsonPickler<'T>()

    abstract Pickle: 'T -> 'U
    abstract UnPickle: 'U -> 'T

    override x.Write(writer: JsonWriter, serializer: JsonSerializer, source: 'T) =
        let target = x.Pickle source
        match targetPickler with
        | None -> serializer.Serialize(writer, target, typeof<'U>)
        | Some p -> p.Write(writer, serializer, target)
    override x.Read(reader: JsonReader, serializer: JsonSerializer) =
        let target =
            match targetPickler with
            | None -> serializer.Deserialize<'U>(reader)
            | Some p -> p.Read(reader, serializer)
        x.UnPickle target
