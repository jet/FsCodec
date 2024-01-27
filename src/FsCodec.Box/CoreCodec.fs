namespace FsCodec.Core

open System
open System.Runtime.InteropServices

/// <summary>Low-level Codec Generator that encodes to a Generic Event <c>'Body</c> Type.
/// Requires that Contract types adhere to the conventions implied by using <c>TypeShape.UnionContract.UnionContractEncoder</c><br/>
/// If you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead.<br/>
/// See non-<c>Core</c> namespace for application level encoders.</summary>
[<AbstractClass; Sealed>]
type Codec private () =

    /// <summary>Generate an <c>IEventCodec</c> using the supplied <c>encoder</c>.<br/>
    /// Uses <c>up</c>, <c>down</c> functions to handle upconversion/downconversion and eventId/correlationId/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c><br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Body, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   encoder,
            // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up: Func<FsCodec.ITimelineEvent<'Body>, 'Contract, 'Event>,
            // <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c><br/>
            // The function is also expected to derive an optional <c>meta</c> object that will be serialized with the same <c>encoder</c>,
            // and <c>eventId</c>, <c>correlationId</c>, <c>causationId</c> and an Event Creation<c>timestamp</c></summary>.
            down: Func<'Context, 'Event, struct ('Contract * 'Meta voption * Guid * string * string * DateTimeOffset)>,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, 'Body, 'Context> =

        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Contract, 'Body>(
                encoder,
                // Round-tripping cases like null and/or empty strings etc involves edge cases that stores,
                // FsCodec.NewtonsoftJson.Codec, Interop.fs and InteropTests.fs do not cover, so we disable this
                requireRecordFields = true,
                allowNullaryCases = not (defaultArg rejectNullaryCases false))

        { new FsCodec.IEventCodec<'Event, 'Body, 'Context> with

            member _.Encode(context, event) =
                let struct (c, meta: 'Meta voption, eventId, correlationId, causationId, timestamp) = down.Invoke(context, event)
                let enc = dataCodec.Encode c
                let meta' = match meta with ValueSome x -> encoder.Encode<'Meta> x | ValueNone -> Unchecked.defaultof<'Body>
                EventData(enc.CaseName, enc.Payload, meta', eventId, correlationId, causationId, timestamp)

            member _.Decode encoded =
                match dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data } with
                | None -> ValueNone
                | Some contract -> up.Invoke(encoded, contract) |> ValueSome }

    /// <summary>Generate an <c>IEventCodec</c> using the supplied <c>encoder</c>.<br/>
    /// Uses <c>up</c>, <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and eventId/correlationId/causationId/timestamp mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name;
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Event, 'Contract, 'Meta, 'Body, 'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   encoder,
            // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up: Func<FsCodec.ITimelineEvent<'Body>, 'Contract, 'Event>,
            // <summary>Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also expected to derive
            //   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c>  (Default: DateTimeOffset.UtcNow).</summary>
            down: Func<'Event, struct ('Contract * 'Meta voption * DateTimeOffset voption)>,
            // <summary>Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to produce a) the final metadata b) the <c>eventId</c> c) the <c>correlationId</c> and d) the <c>causationId</c></summary>
            mapCausation: Func<'Context, 'Meta voption, struct ('Meta voption * Guid * string * string)>,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, 'Body, 'Context> =

        let down context union =
            let struct (c, m, t) = down.Invoke union
            let struct (m', eventId, correlationId, causationId) = mapCausation.Invoke(context, m)
            struct (c, m', eventId, correlationId, causationId, match t with ValueSome t -> t | ValueNone -> DateTimeOffset.UtcNow)
        Codec.Create(encoder, up = up, down = down, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> using the supplied <c>encoder</c>.<br/>
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion/timestamping without eventId/correlation/causationId mapping
    ///   and/or surfacing metadata to the programming model by including it in the emitted <c>'Event</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies</summary>.
    static member Create<'Event, 'Contract, 'Meta, 'Body when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   encoder,
            // <summary>Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the representation (typically a Discriminated Union) that is to be presented to the programming model.</summary>
            up: Func<FsCodec.ITimelineEvent<'Body>, 'Contract, 'Event>,
            // <summary>Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also expected to derive
            //   a <c>meta</c> object that will be serialized with the same options (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c> (Default: DateTimeOffset.UtcNow).</summary>
            down: Func<'Event, struct ('Contract * 'Meta voption * DateTimeOffset voption)>,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Event, 'Body, unit> =

        let mapCausation () (m: 'Meta voption) = struct (m, Guid.NewGuid(), null, null)
        Codec.Create(encoder, up = up, down = down, mapCausation = mapCausation, ?rejectNullaryCases = rejectNullaryCases)

    /// <summary>Generate an <code>IEventCodec</code> using the supplied <c>encoder</c>.<br/>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.</summary>
    static member Create<'Body, 'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   encoder: TypeShape.UnionContract.IEncoder<'Body>,
            // <summary>Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them.</summary>
            [<Optional; DefaultParameterValue(null)>] ?rejectNullaryCases)
        : FsCodec.IEventCodec<'Union, 'Body, unit> =

        let up (_e: FsCodec.ITimelineEvent<'Body>) (u: 'Union): 'Union = u
        let down (event: 'Union) = struct (event, ValueNone (*Meta*), ValueNone (*Timestamp*))
        Codec.Create(encoder, up = up, down = down, ?rejectNullaryCases = rejectNullaryCases)
