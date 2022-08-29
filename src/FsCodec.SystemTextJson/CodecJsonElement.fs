namespace FsCodec.SystemTextJson.Core

open System.Text.Json

/// System.Text.Json implementation of TypeShape.UnionContractEncoder's IEncoder that encodes to a JsonElement
type JsonElementEncoder(options : JsonSerializerOptions) =
    interface TypeShape.UnionContract.IEncoder<JsonElement> with
        member _.Empty = Unchecked.defaultof<JsonElement>

        member _.Encode(value : 'T) =
            JsonSerializer.SerializeToElement(value, options)

        member _.Decode<'T>(json : JsonElement) =
            JsonSerializer.Deserialize<'T>(json, options)

namespace FsCodec.SystemTextJson

open System
open System.Runtime.InteropServices
open System.Text.Json

/// <summary>Provides Codecs that render to a JsonElement suitable for storage in Event Stores that use <c>System.Text.Json</c> internally such as Equinox.CosmosStore v4 and later.
/// Requires that Contract types adhere to the conventions implied by using <c>TypeShape.UnionContract.UnionContractEncoder</c><br/>
/// If you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead.<br/>
/// See <a href="https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs"></a> for example usage.</summary>
type CodecJsonElement private () =

    static let DefaultEncoder = lazy (Core.JsonElementEncoder Options.Default :> TypeShape.UnionContract.IEncoder<JsonElement>)

    static let mkEncoder : JsonSerializerOptions option -> TypeShape.UnionContract.IEncoder<JsonElement> = function
        | None -> DefaultEncoder.Value
        | Some opts -> Core.JsonElementEncoder opts :> _

    /// <summary>Generate an <code>IEventCodec</code> that handles <c>JsonElement</c> Event Bodies using the supplied <c>System.Text.Json</c> <c>options</c>.
    /// Uses <c>up</c>, <c>down</c> functions to handle upconversion/downconversion and eventId/correlationId/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : struct (FsCodec.ITimelineEvent<JsonElement> * 'Contract) -> 'Event,
            // <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c><br/>
            // The function is also expected to derive an optional <c>meta</c> object that will be serialized with the same <c>encoder</c>,
            // and <c>eventId</c>, <c>correlationId</c>, <c>causationId</c> and an Event Creation<c>timestamp</c></summary>.
            down : struct ('Context voption * 'Event) -> struct ('Contract * 'Meta voption * Guid * string * string * DateTimeOffset voption),
            // <summary>Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c></summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =
        FsCodec.Core.Codec.Create(mkEncoder options, up, down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <c>IEventCodec</c> that handles <c>JsonElement</c> Event Bodies using the supplied <c>System.Text.Json</c> <c>options</c>.
    /// Uses <c>up</c>, <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and eventId/correlationId/causationId/timestamp mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : struct (FsCodec.ITimelineEvent<JsonElement> * 'Contract) -> 'Event,
            // <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also expected to derive
            //   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c>.</summary>
            down : 'Event -> struct ('Contract * 'Meta voption * DateTimeOffset voption),
            // <summary>Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>eventId</c> c) the <c>correlationId</c> and d) the <c>causationId</c></summary>
            mapCausation : struct ('Context voption * 'Meta voption) -> struct ('Meta voption * Guid * string * string),
            // <summary>Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c>.</summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =
        FsCodec.Core.Codec.Create(mkEncoder options, up, down, mapCausation, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> that handles <c>JsonElement</c> Event Bodies using the supplied <c>System.Text.Json</c> <c>options</c>.
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion/timestamping without eventId/correlation/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies</summary>.
    static member Create<'Event, 'Contract, 'Meta when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up : struct (FsCodec.ITimelineEvent<JsonElement> * 'Contract) -> 'Event,
            // <summary>Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also expected to derive
            //   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c>.</summary>
            down : 'Event -> struct ('Contract * 'Meta voption * DateTimeOffset voption),
            // <summary>Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c>.</summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, JsonElement, obj> =
        FsCodec.Core.Codec.Create(mkEncoder options, up, down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> that handles <c>JsonElement</c> Event Bodies using the supplied <c>System.Text.Json</c> <c>options</c>.
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   // <summary>Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Default</c>.</summary>
            [<Optional; DefaultParameterValue(null)>] ?options,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Union, JsonElement, obj> =
        FsCodec.Core.Codec.Create(mkEncoder options, ?rejectNullaryCases = rejectNullaryCases)
