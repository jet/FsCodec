namespace Gardelloyd.NewtonsoftJson

open Newtonsoft.Json
open System
open System.IO
open System.Runtime.InteropServices

/// Reuse interim buffers when coding/encoding
// https://stackoverflow.com/questions/55812343/newtonsoft-json-net-jsontextreader-garbage-collector-intensive
module private CharBuffersPool =
    let private inner = System.Buffers.ArrayPool<char>.Shared
    let instance =
        { new IArrayPool<char> with
            member __.Rent minLen = inner.Rent minLen
            member __.Return x = inner.Return x }

// http://www.philosophicalgeek.com/2015/02/06/announcing-microsoft-io-recycablememorystream/
module private Utf8BytesEncoder =
    let private streamManager = Microsoft.IO.RecyclableMemoryStreamManager()
    let rentStream () = streamManager.GetStream("bytesEncoder")
    let wrapAsStream json =
        // This is the most efficient way of approaching this without using Spans etc.
        // RecyclableMemoryStreamManager does not have any wins to provide us
        new MemoryStream(json,writable=false)
    let makeJsonReader(ms : MemoryStream) =
        new JsonTextReader(new StreamReader(ms), ArrayPool = CharBuffersPool.instance)
    let private utf8NoBom = new System.Text.UTF8Encoding(false, true)
    let makeJsonWriter ms =
        // We need to `leaveOpen` in order to allow .Dispose of the `.rentStream`'d to return it
        let sw = new StreamWriter(ms, utf8NoBom, 1024, leaveOpen=true) // same middle args as StreamWriter default ctor 
        new JsonTextWriter(sw, ArrayPool = CharBuffersPool.instance)

module Core =
    /// Newtonsoft.Json implementation of TypeShape.UnionContractEncoder's IEncoder that encodes direct to a UTF-8 Buffer
    type BytesEncoder(settings : JsonSerializerSettings) =
        let serializer = JsonSerializer.Create(settings)
        interface TypeShape.UnionContract.IEncoder<byte[]> with
            member __.Empty = Unchecked.defaultof<_>
            member __.Encode (value : 'T) =
                use ms = Utf8BytesEncoder.rentStream ()
                (   use jsonWriter = Utf8BytesEncoder.makeJsonWriter ms
                    serializer.Serialize(jsonWriter, value, typeof<'T>))
                // TOCONSIDER as noted in the comments on RecyclableMemoryStream.ToArray, ideally we'd be continuing the rental and passing out a Span
                ms.ToArray()
            member __.Decode(json : byte[]) =
                use ms = Utf8BytesEncoder.wrapAsStream json
                use jsonReader = Utf8BytesEncoder.makeJsonReader ms
                serializer.Deserialize<'T>(jsonReader)

// Provides Codecs that render to a UTF-8 array suitable for storage in EventStore or CosmosDb based on explicit functions you supply using `Newtonsoft.Json` and
/// `TypeShape.UnionContract.UnionContractEncoder` - if you need full control and/or have have your own codecs, see `Gardelloyd.Codec.Create` instead
type Codec private () =

    static let defaultSettings = lazy Settings.Create()

    /// Generate a codec suitable for use with <c>Equinox.EventStore</c>, <c>Equinox.Cosmos</c> or <c>Propulsion</c> libraries,
    ///   using the supplied `Newtonsoft.Json` <c>settings</c>.
    /// The Event Type Names are inferred based on either explicit `DataMember(Name=` Attributes,
    ///   or (if unspecified) the Discriminated Union Case Name
    /// The Union must be tagged with `interface TypeShape.UnionContract.IUnionContract` to signify this scheme applies.
    /// See https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs for example usage.
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   /// Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as `Settings.Create()`<
            ?settings,
            /// Generate a metadata object to serialize from a given 'value
            [<Optional;DefaultParameterValue(null)>]?mapMeta : 'Union -> obj,
            /// Provides a function that can be used to determine the creation time for a given event 'value
            [<Optional;DefaultParameterValue(null)>]?genTimestamp : 'Union -> DateTimeOffset,
            /// Fail encoder generation if union contains nullary cases. Defaults to <c>true</c>.<
            [<Optional;DefaultParameterValue(null)>]?allowNullaryCases)
        : Gardelloyd.IUnionEncoder<'Union,byte[]> =
        let settings = match settings with Some x -> x | None -> defaultSettings.Value
        let bytesEncoder : TypeShape.UnionContract.IEncoder<_> = new Core.BytesEncoder(settings) :> _
        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Union,byte[]>(
                bytesEncoder,
                requireRecordFields=true, // See JsonConverterTests - roundtripping UTF-8 correctly with Json.net is painful so for now we lock up the dragons
                ?allowNullaryCases=allowNullaryCases)
        let (metaf, timestampf) =
            match mapMeta,genTimestamp with
            | Some mf, Some gts -> (fun value -> bytesEncoder.Encode (mf value)), (fun value -> Some (gts value))
            | Some mf, None -> (fun value -> bytesEncoder.Encode (mf value)), (fun _ -> None)
            | None, Some gts -> (fun _ -> null), (fun value -> Some (gts value))
            | None, None -> (fun _ -> null), (fun _ -> None)
        { new Gardelloyd.IUnionEncoder<'Union,byte[]> with
            member __.Encode value =
                let enc = dataCodec.Encode value
                let (meta, timestamp) = metaf value, timestampf value
                Gardelloyd.Core.EventData.Create(enc.CaseName, enc.Payload, meta, ?timestamp=timestamp) :> _
            member __.TryDecode encoded =
                dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data } }