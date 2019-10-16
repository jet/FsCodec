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

// Provides Codecs that render to a UTF-8 array suitable for storage in Event Stores based using <c>Newtonsoft.Json</c> and the conventions implied by using
/// <c>TypeShape.UnionContract.UnionContractEncoder</c> - if you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead
/// See <a href=""https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs"></a> for example usage.
type Codec private () =

    static let defaultSettings = lazy Settings.Create()

    /// Generate a <code>IUnionEncoder</code> Codec, using the supplied <c>Newtonsoft.Json<c/> <c>settings</c>.
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Union</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union,'Meta,'Contract,'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.
            up : FsCodec.ITimelineEvent<byte[]> * 'Contract -> 'Union,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            down : 'Context option -> 'Union -> 'Contract * 'Meta option * string * string * DateTimeOffset option,
            /// Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Settings.Create()</c>
            ?settings,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union,byte[],'Context> =
        let settings = match settings with Some x -> x | None -> defaultSettings.Value
        let bytesEncoder : TypeShape.UnionContract.IEncoder<_> = new Core.BytesEncoder(settings) :> _
        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Contract,byte[]>(
                bytesEncoder,
                requireRecordFields=true, // See JsonConverterTests - round-tripping UTF-8 correctly with Json.net is painful so for now we lock up the dragons
                allowNullaryCases=not (defaultArg rejectNullaryCases false))
        { new FsCodec.IUnionEncoder<'Union,byte[],'Context> with
            member __.Encode(context,value) =
                let (evt, meta : 'Meta option, correlationId, causationId, timestamp : DateTimeOffset option) = down context value
                let enc = dataCodec.Encode evt
                let metaUtf8 = meta |> Option.map bytesEncoder.Encode<'Meta>
                FsCodec.Core.EventData.Create(enc.CaseName, enc.Payload, defaultArg metaUtf8 null, correlationId, causationId, ?timestamp=timestamp) :> _
            member __.TryDecode encoded =
                let evt = dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data }
                match evt with None -> None | Some e -> (encoded,e) |> up |> Some }

    /// Generate a <code>IUnionEncoder</code> Codec using the supplied <c>Newtonsoft.Json</c> <c>settings</c>.
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   // Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Settings.Create()</c>
            [<Optional;DefaultParameterValue(null)>]
            ?settings,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union, byte[], obj> =
        Codec.Create(up=snd, down=(fun _context evt -> evt, None, null, null, None), ?settings=settings, ?rejectNullaryCases=rejectNullaryCases)