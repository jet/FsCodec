// Mirror of FsCodec.NewtonsoftJson/SystemTextJson.Codec intended to provide equivalent calls and functionality, without actually serializing/deserializing as JSON
// This is a useful facility for in-memory stores such as Equinox's MemoryStore as it enables you to
// - efficiently test behaviors from an event sourced decision processing perspective (e.g. with Property Based Tests)
// - without paying a serialization cost and/or having to deal with sanitization of generated data in order to make it roundtrippable through same
namespace FsCodec.Box

open System
open System.Runtime.InteropServices

/// <summary>Provides Codecs that render to boxed object, ideal for usage in a Memory Store.
/// Requires that Contract types adhere to the conventions implied by using <c>TypeShape.UnionContract.UnionContractEncoder</c><br/>
/// If you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead.<br/>
/// See <a href="https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs"></a> for example usage.</summary>
[<AbstractClass; Sealed>]
type Codec private () =

    static let defEncoder: TypeShape.UnionContract.IEncoder<obj> = TypeShape.UnionContract.BoxEncoder() :> _

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>obj</c> (boxed .NET <c>Object</c>) Event Bodies.<br/>
    /// Uses <c>up</c>, <c>down</c> functions to handle upconversion/downconversion and eventId/correlationId/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up: Func<FsCodec.ITimelineEvent<obj>, 'Contract, 'Event>,
            // <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c><br/>
            // The function is also expected to derive an optional <c>meta</c> object that will be serialized with the same <c>encoder</c>,
            // and <c>eventId</c>, <c>correlationId</c>, <c>causationId</c> and an Event Creation<c>timestamp</c></summary>.
            down: Func<'Context, 'Event, struct ('Contract * 'Meta voption * Guid * string * string * DateTimeOffset)>,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, obj, 'Context> =
        FsCodec.Core.Codec.Create(defEncoder, up, down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>obj</c> (boxed .NET <c>Object</c>) Event Bodies.<br/>
    /// Uses <c>up</c>, <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and eventId/correlationId/causationId/timestamp mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up: Func<FsCodec.ITimelineEvent<obj>, 'Contract, 'Event>,
            // <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also expected to derive
            //   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c> (Default: DateTimeOffset.UtcNow).</summary>
            down: Func<'Event, struct ('Contract * 'Meta voption * DateTimeOffset voption)>,
            // <summary>Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>eventId</c> c) the <c>correlationId</c> and d) the <c>causationId</c></summary>
            mapCausation: Func<'Context, 'Meta voption, struct ('Meta voption * Guid * string * string)>,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, obj, 'Context> =
        FsCodec.Core.Codec.Create(defEncoder, up, down, mapCausation, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>obj</c> (boxed .NET <c>Object</c>) Event Bodies.<br/>
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion/timestamping without eventId/correlation/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies</summary>.
    static member Create<'Event, 'Contract, 'Meta when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up: Func<FsCodec.ITimelineEvent<obj>, 'Contract, 'Event>,
            // <summary>Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also expected to derive
            //   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c> (Default: DateTimeOffset.UtcNow).</summary>
            down: Func<'Event, struct ('Contract * 'Meta voption * DateTimeOffset voption)>,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, obj, unit> =
        FsCodec.Core.Codec.Create(defEncoder, up, down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>obj</c> (boxed .NET <c>Object</c>) Event Bodies.<br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Union, obj, unit> =
        FsCodec.Core.Codec.Create(defEncoder, ?rejectNullaryCases = rejectNullaryCases)
