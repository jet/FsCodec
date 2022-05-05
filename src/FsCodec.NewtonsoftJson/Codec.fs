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
    let wrapAsStream (utf8json : ReadOnlyMemory<byte>) =
        // This is the most efficient way of approaching this without using Spans etc.
        // RecyclableMemoryStreamManager does not have any wins to provide us
        new MemoryStream(utf8json.ToArray(), writable = false)
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
        interface TypeShape.UnionContract.IEncoder<ReadOnlyMemory<byte>> with
            member _.Empty = ReadOnlyMemory.Empty

            member _.Encode (value : 'T) =
                use ms = Utf8BytesEncoder.rentStream ()
                (   use jsonWriter = Utf8BytesEncoder.makeJsonWriter ms
                    serializer.Serialize(jsonWriter, value, typeof<'T>))
                // TOCONSIDER as noted in the comments on RecyclableMemoryStream.ToArray, ideally we'd be continuing the rental and passing out a Span
                ms.ToArray() |> ReadOnlyMemory

            member _.Decode(utf8json : ReadOnlyMemory<byte>) =
                use ms = Utf8BytesEncoder.wrapAsStream utf8json
                use jsonReader = Utf8BytesEncoder.makeJsonReader ms
                serializer.Deserialize<'T>(jsonReader)

/// <summary>Provides Codecs that render to a <c>ReadOnlyMemory&lt;byte&gt;</c>, suitable for storage in Event Stores that handle Event Data and Metadata as opaque blobs.
/// Requires that Contract types adhere to the conventions implied by using <c>TypeShape.UnionContract.UnionContractEncoder</c><br/>
/// If you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead.<br/>
/// See <a href="https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs"></a> for example usage.</summary>
type Codec private () =

    static let DefaultEncoder : Lazy<TypeShape.UnionContract.IEncoder<ReadOnlyMemory<byte>>> = lazy (Core.BytesEncoder Options.Default :> _)

    static let mkEncoder : JsonSerializerSettings option -> TypeShape.UnionContract.IEncoder<ReadOnlyMemory<byte>> = function
        | None -> DefaultEncoder.Value
        | Some opts -> Core.BytesEncoder opts :> _

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies using the supplied <c>Newtonsoft.Json.JsonSerializerSettings</c> <c>options</c>.<br/>
    /// Uses <c>up</c>, <c>down</c> functions to handle upconversion/downconversion and eventId/correlationId/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<ReadOnlyMemory<byte>> * 'Contract -> 'Event,
            /// <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c><br/>
            /// The function is also expected to derive an optional <c>meta</c> object that will be serialized with the same <c>encoder</c>,
            /// and <c>eventId</c>, <c>correlationId</c>, <c>causationId</c> and an Event Creation<c>timestamp</c></summary>.
            down : 'Context option * 'Event -> 'Contract * 'Meta option * Guid * string * string * DateTimeOffset option,
            /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Box.Core.Codec.Create(mkEncoder options, up, down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies using the supplied <c>Newtonsoft.Json.JsonSerializerSettings</c> <c>options</c>.<br/>
    /// Uses <c>up</c>, <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and eventId/correlationId/causationId/timestamp mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<ReadOnlyMemory<byte>> * 'Contract -> 'Event,
            /// <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.</summary>
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// <summary>Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>eventId</c> c) the <c>correlationId</c> and d) the <c>causationId</c></summary>
            mapCausation : 'Context option * 'Meta option -> 'Meta option * Guid * string * string,
            /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, 'Context> =
        FsCodec.Box.Core.Codec.Create(mkEncoder options, up, down, mapCausation, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies using the supplied <c>Newtonsoft.Json.JsonSerializerSettings</c> <c>options</c>.<br/>
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion/timestamping without eventId/correlation/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies</summary>.
    static member Create<'Event, 'Contract, 'Meta when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<ReadOnlyMemory<byte>> * 'Contract -> 'Event,
            /// <summary>Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.</summary>
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, ReadOnlyMemory<byte>, obj> =
        FsCodec.Box.Core.Codec.Create(mkEncoder options, up, down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>ReadOnlyMemory&lt;byte&gt;</c> Event Bodies using the supplied <c>Newtonsoft.Json.JsonSerializerSettings</c> <c>options</c>.<br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Union, ReadOnlyMemory<byte>, obj> =
        FsCodec.Box.Core.Codec.Create(mkEncoder options, ?rejectNullaryCases = rejectNullaryCases)
