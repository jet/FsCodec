namespace FsCodec.NewtonsoftJson

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
/// `TypeShape.UnionContract.UnionContractEncoder` - if you need full control and/or have have your own codecs, see `FsCodec.Codec.Create` instead
/// See <a href=""https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs"></a> for example usage.
type Codec private () =

    static let defaultSettings = lazy Settings.Create()

    /// Generate a codec suitable for use with <c>Equinox.EventStore</c>, <c>Equinox.Cosmos</c> or <c>Propulsion</c> libraries,
    ///   using the supplied `Newtonsoft.Json` <c>settings</c>
    /// Uses <c>up</c> and <c>down</c> functions to faciliate upconversion/downconversion
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Union</c>
    /// The Event Type Names are inferred based on either explicit `DataMember(Name=` Attributes,
    ///   or (if unspecified) the Discriminated Union Case Name on the <c>'Contract</c> type.
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union,'Contract when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the `TypeShape UnionConverter` 'Contract case the event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discrimninated Union) that is to be represented to the programming model
            up : FsCodec.IEvent<byte[]> * 'Contract -> 'Union,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the `TypeShape UnionConverter` 'Contract
            /// The function is also expected to derive a `metadata` (which may be `null`) and an Event Creation TimeStamp
            down : 'Union -> 'Contract * byte[] * DateTimeOffset,
            /// Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as `Settings.Create()`<
            ?settings,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union,byte[]> =
        let settings = match settings with Some x -> x | None -> defaultSettings.Value
        let bytesEncoder : TypeShape.UnionContract.IEncoder<_> = new Core.BytesEncoder(settings) :> _
        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Contract,byte[]>(
                bytesEncoder,
                requireRecordFields=true, // See JsonConverterTests - roundtripping UTF-8 correctly with Json.net is painful so for now we lock up the dragons
                allowNullaryCases=not (defaultArg rejectNullaryCases false))
        { new FsCodec.IUnionEncoder<'Union,byte[]> with
            member __.Encode value =
                let (evt, meta, timestamp) = down value
                let enc = dataCodec.Encode evt
                FsCodec.Core.EventData.Create(enc.CaseName, enc.Payload, meta, timestamp) :> _
            member __.TryDecode encoded =
                let evt = dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data }
                match evt with None -> None | Some e -> (encoded,e) |> up |> Some }

    /// Generate a codec suitable for use with <c>Equinox.EventStore</c>, <c>Equinox.Cosmos</c> or <c>Propulsion</c> libraries,
    ///   using the supplied `Newtonsoft.Json` <c>settings</c>.
    /// The Event Type Names are inferred based on either explicit `DataMember(Name=` Attributes,
    ///   or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c? must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   // Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as `Settings.Create()`<
            ?settings,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union,byte[]> =
        Codec.Create(up=snd, down=(fun evt -> evt,null,DateTimeOffset.UtcNow), ?settings=settings, ?rejectNullaryCases=rejectNullaryCases)