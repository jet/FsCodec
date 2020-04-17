namespace FsCodec.SystemTextJson.Core

open System.Text.Json

/// System.Text.Json implementation of TypeShape.UnionContractEncoder's IEncoder that encodes to a `JsonElement`
type JsonElementEncoder(options : JsonSerializerOptions) =
    interface TypeShape.UnionContract.IEncoder<JsonElement> with
        member __.Empty = Unchecked.defaultof<_>

        member __.Encode(value : 'T) =
            JsonSerializer.SerializeToElement(value, options)

        member __.Decode(json : JsonElement) =
            JsonSerializer.DeserializeElement(json, options)

namespace FsCodec.SystemTextJson

open System
open System.Runtime.InteropServices
open System.Text.Json

/// Provides Codecs that render to a JsonElement suitable for storage in Event Stores based using <c>System.Text.Json</c> and the conventions implied by using
/// <c>TypeShape.UnionContract.UnionContractEncoder</c> - if you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead
/// See <a href=""https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs"></a> for example usage.
type Codec private () =

    static let defaultOptions = lazy Options.Create()

    /// Generate an <code>IEventCodec</code> using the supplied <c>System.Text.Json<c/> <c>options</c>.
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.
            up : FsCodec.ITimelineEvent<JsonElement> * 'Contract -> 'Event,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            down : 'Context option * 'Event -> 'Contract * 'Meta option * Guid * string * string * DateTimeOffset option,
            /// Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Create()</c>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =

        let options = match options with Some x -> x | None -> defaultOptions.Value
        let elementEncoder : TypeShape.UnionContract.IEncoder<_> = Core.JsonElementEncoder(options) :> _
        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Contract, JsonElement>(
                elementEncoder,
                // Round-tripping cases like null and/or empty strings etc involves edge cases that various stores
                // and/or Interop.fs do not cover, so we disable this
                requireRecordFields = true,
                allowNullaryCases = not (defaultArg rejectNullaryCases false))

        { new FsCodec.IEventCodec<'Event, JsonElement, 'Context> with
            member __.Encode(context, event) =
                let (c, meta : 'Meta option, eventId, correlationId, causationId, timestamp : DateTimeOffset option) = down (context, event)
                let enc = dataCodec.Encode c
                let meta' = match meta with Some x -> elementEncoder.Encode<'Meta> x | None -> Unchecked.defaultof<_>
                FsCodec.Core.EventData.Create(enc.CaseName, enc.Payload, meta', eventId, correlationId, causationId, ?timestamp = timestamp)

            member __.TryDecode encoded =
                match dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data } with
                | None -> None
                | Some contract -> up (encoded, contract) |> Some }

    /// Generate an <code>IEventCodec</code> using the supplied <c>System.Text.Json<c/> <c>options</c>.
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Event, 'Contract, 'Meta, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.
            up : FsCodec.ITimelineEvent<JsonElement> * 'Contract -> 'Event,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId
            mapCausation : 'Context option * 'Meta option -> 'Meta option * Guid * string * string,
            /// Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Create()</c>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, JsonElement, 'Context> =

        let down (context, union) =
            let c, m, t = down union
            let m', eventId, correlationId, causationId = mapCausation (context, m)
            c, m', eventId, correlationId, causationId, t
        Codec.Create(up = up, down = down, ?options = options, ?rejectNullaryCases = rejectNullaryCases)

    /// Generate an <code>IEventCodec</code> using the supplied <c>System.Text.Json<c/> <c>options</c>.
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Event, 'Contract, 'Meta when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.
            up : FsCodec.ITimelineEvent<JsonElement> * 'Contract -> 'Event,
            /// Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            down : 'Event -> 'Contract * 'Meta option * DateTimeOffset option,
            /// Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Create()</c>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, JsonElement, obj> =

        let mapCausation (_context : obj, m : 'Meta option) = m, Guid.NewGuid(), null, null
        Codec.Create(up = up, down = down, mapCausation = mapCausation, ?options = options, ?rejectNullaryCases = rejectNullaryCases)

    /// Generate an <code>IEventCodec</code> using the supplied <c>System.Text.Json</c> <c>options</c>.
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   // Configuration to be used by the underlying <c>System.Text.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Options.Create()</c>
            [<Optional; DefaultParameterValue(null)>] ?options,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Union, JsonElement, obj> =

        let up : FsCodec.ITimelineEvent<_> * 'Union -> 'Union = snd
        let down (event : 'Union) = event, None, None
        Codec.Create(up = up, down = down, ?options = options, ?rejectNullaryCases = rejectNullaryCases)
