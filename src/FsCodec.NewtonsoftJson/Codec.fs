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
            member _.Rent minLen = inner.Rent minLen
            member _.Return x = inner.Return x }

// http://www.philosophicalgeek.com/2015/02/06/announcing-microsoft-io-recycablememorystream/
module private Utf8BytesEncoder =
    let private streamManager = Microsoft.IO.RecyclableMemoryStreamManager()
    let rentStream () = streamManager.GetStream("bytesEncoder")
    let wrapAsStream json =
        // This is the most efficient way of approaching this without using Spans etc.
        // RecyclableMemoryStreamManager does not have any wins to provide us
        new MemoryStream(json, writable = false)
    let makeJsonReader(ms : MemoryStream) =
        new JsonTextReader(new StreamReader(ms), ArrayPool = CharBuffersPool.instance)
    let private utf8NoBom = System.Text.UTF8Encoding(false, true)
    let makeJsonWriter ms =
        // We need to `leaveOpen` in order to allow .Dispose of the `.rentStream`'d to return it
        let sw = new StreamWriter(ms, utf8NoBom, 1024, leaveOpen = true) // same middle args as StreamWriter default ctor
        new JsonTextWriter(sw, ArrayPool = CharBuffersPool.instance)

module Core =
    /// Newtonsoft.Json implementation of TypeShape.UnionContractEncoder's IEncoder that encodes direct to a UTF-8 Buffer
    type BytesEncoder(settings : JsonSerializerSettings) =
        let serializer = JsonSerializer.Create(settings)
        interface TypeShape.UnionContract.IEncoder<byte[]> with
            member _.Empty = Unchecked.defaultof<_>

            member _.Encode (value : 'T) =
                use ms = Utf8BytesEncoder.rentStream ()
                (   use jsonWriter = Utf8BytesEncoder.makeJsonWriter ms
                    serializer.Serialize(jsonWriter, value, typeof<'T>))
                // TOCONSIDER as noted in the comments on RecyclableMemoryStream.ToArray, ideally we'd be continuing the rental and passing out a Span
                ms.ToArray()

            member _.Decode(json : byte[]) =
                use ms = Utf8BytesEncoder.wrapAsStream json
                use jsonReader = Utf8BytesEncoder.makeJsonReader ms
                serializer.Deserialize<'T>(jsonReader)

/// <summary>Provides Codecs that render to a UTF-8 array suitable for storage in Event Stores based using <c>Newtonsoft.Json</c> and the conventions implied by using
/// <c>TypeShape.UnionContract.UnionContractEncoder</c> - if you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead
/// See <a href="https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs" /> for example usage.</summary>
type Codec private () =

    /// <summary>Generate an <code>IEventCodec</code> using the supplied <c>Newtonsoft.Json</c> <c>settings</c>.<br/>
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<byte[]> * 'Contract -> 'Event,
            /// <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.</summary>
            down : 'Context option * 'Event -> 'Contract * 'Meta option * Guid * string * string * DateTimeOffset option,
            /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?settings,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, byte[], 'Context> =

        let settings = match settings with Some x -> x | None -> Options.Default
        let bytesEncoder : TypeShape.UnionContract.IEncoder<_> = Core.BytesEncoder(settings) :> _
        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Contract, byte[]>(
                bytesEncoder,
                // For now, we hard wire in disabling of non-record bodies as:
                // a) it's extra yaks to shave
                // b) it's questionable whether allowing one to define event contracts that preclude adding extra fields is a useful idea in the first instance
                // See VerbatimUtf8EncoderTests.fs and InteropTests.fs - there are edge cases when `d` fields have null / zero-length / missing values
                requireRecordFields = true,
                allowNullaryCases = not (defaultArg rejectNullaryCases false))

        { new FsCodec.IEventCodec<'Event, byte[], 'Context> with
            member _.Encode(context, event) =
                let (c, meta : 'Meta option, eventId, correlationId, causationId, timestamp : DateTimeOffset option) = down (context, event)
                let enc = dataCodec.Encode c
                let metaUtf8 = match meta with Some x -> bytesEncoder.Encode<'Meta> x | None -> null
                FsCodec.Core.EventData.Create(enc.CaseName, enc.Payload, metaUtf8, eventId, correlationId, causationId, ?timestamp = timestamp)

            member _.TryDecode encoded =
                match dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data } with
                | None -> None
                | Some contract -> up (encoded, contract) |> Some }

    /// <summary>Generate an <code>IEventCodec</code> using the supplied <c>Newtonsoft.Json</c> <c>settings</c>.
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<byte[]> * 'Contract -> 'Event,
            /// <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.</summary>
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// <summary>Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId</summary>
            mapCausation : 'Context option * 'Meta option -> 'Meta option * Guid * string * string,
            /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?settings,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, byte[], 'Context> =

        let down (context, union) =
            let c, m, t = down union
            let m', eventId, correlationId, causationId = mapCausation (context, m)
            c, m', eventId, correlationId, causationId, t
        Codec.Create(up = up, down = down, ?settings = settings, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> using the supplied <c>Newtonsoft.Json</c> <c>settings</c>.
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<byte[]> * 'Contract -> 'Event,
            /// <summary>Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.</summary>
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?settings,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, byte[], obj> =

        let mapCausation (_context : obj, m : 'Meta option) = m, Guid.NewGuid(), null, null
        Codec.Create(up = up, down = down, mapCausation = mapCausation, ?settings = settings, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> using the supplied <c>Newtonsoft.Json</c> <c>settings</c>.
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?settings,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Union, byte[], obj> =

        let up : FsCodec.ITimelineEvent<_> * 'Union -> 'Union = snd
        let down (event : 'Union) = event, None, None
        Codec.Create(up = up, down = down, ?settings = settings, ?rejectNullaryCases = rejectNullaryCases)
