/// Fork of FsCodec.NewtonsoftJson.Codec intended to provide equivalent calls and functionality, without actually serializing/deserializing as JSON
/// This is a useful facility for in-memory stores such as Equinox's MemoryStore as it enables you to
/// - efficiently test behaviors from an event sourced decision processing perspective (e.g. with Property Based Tests)
/// - without paying a serialization cost and/or having to deal with sanitization of generated data in order to make it roundtrippable through same
namespace FsCodec.Box

open System
open System.Runtime.InteropServices

/// <summary>Provides Codecs that encode and/or extract Event bodies from a stream bearing a set of events defined in terms of a Discriminated Union,
///   using the conventions implied by using <c>TypeShape.UnionContract.UnionContractEncoder</c><br/>
/// If you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead.<br/>
/// See <a href="https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs" /> for example usage.</summary>
type Codec private () =

    /// <summary>Generate a <code>IEventEncoder</code> Codec that roundtrips events by holding the boxed form of the Event body.<br/>
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or, if unspecified, the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<obj> * 'Contract -> 'Event,
            /// <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionContract</c> <c>'Contract</c><br/>
            /// The function is also expected to derive a <c>meta</c> object that will be held alongside the data (if it's not <c>None</c>)
            ///   together with its <c>eventId</c>, <c>correlationId</c>, <c>causationId</c> and an event creation <c>timestamp</c> (defaults to <c>UtcNow</c>).</summary>
            down : 'Context option * 'Event -> 'Contract * 'Meta option * Guid * string * string * DateTimeOffset option,
            /// <summary>Enables one to fail encoder generation if 'Contract contains nullary cases. Defaults to <c>false</c>, i.e. permitting them</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, obj, 'Context> =

        let boxEncoder : TypeShape.UnionContract.IEncoder<obj> = TypeShape.UnionContract.BoxEncoder() :> _
        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Contract, obj>(
                boxEncoder,
                requireRecordFields = true,
                allowNullaryCases = not (defaultArg rejectNullaryCases false))

        { new FsCodec.IEventCodec<'Event, obj, 'Context> with
            member _.Encode(context, event) =
                let (c, meta : 'Meta option, eventId, correlationId, causationId, timestamp : DateTimeOffset option) = down (context, event)
                let enc = dataCodec.Encode c
                let meta = meta |> Option.map boxEncoder.Encode<'Meta>
                FsCodec.Core.EventData.Create(enc.CaseName, enc.Payload, defaultArg meta null, eventId, correlationId, causationId, ?timestamp = timestamp)

            member _.TryDecode encoded =
                let cOption = dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data }
                match cOption with None -> None | Some contract -> let event = up (encoded, contract) in Some event }

    /// <summary>Generate an <c>IEventCodec</c> that roundtrips events by holding the boxed form of the Event body.
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<obj> * 'Contract -> 'Event,
            /// <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c><br/>
            /// The function is also expected to derive:<br>
            /// - a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)<br/>
            /// - and an Event Creation <c>timestamp</c>.<summary>
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// <summary>Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId</summary>
            mapCausation : 'Context option * 'Meta option -> 'Meta option * Guid * string * string,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, obj, 'Context> =

        let down (context, event) =
            let c, m, t = down event
            let m', eventId, correlationId, causationId = mapCausation (context, m)
            c, m', eventId, correlationId, causationId, t
        Codec.Create(up = up, down = down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> that roundtrips events by holding the boxed form of the Event body.<br/>
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : FsCodec.ITimelineEvent<obj> * 'Contract -> 'Event,
            /// <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive:<br/>
            /// - a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)<br/>
            /// - and an Event Creation <c>timestamp</c>.</summary>
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, obj, obj> =

        let mapCausation (_context : obj, m : 'Meta option) = m, Guid.NewGuid(), null, null
        Codec.Create(up = up, down = down, mapCausation = mapCausation, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> that roundtrips events by holding the boxed form of the Event body.<br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Union, obj, obj> =

        let up : FsCodec.ITimelineEvent<_> * 'Union -> 'Union = snd
        let down (event : 'Union) = event, None, None
        Codec.Create(up = up, down = down, ?rejectNullaryCases = rejectNullaryCases)
