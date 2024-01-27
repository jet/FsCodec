namespace FsCodec

open System

/// Provides Codecs that render to store-supported form (e.g., a UTF-8 byte array) suitable for storage in Event Stores, based on explicit functions you supply
/// Does not involve conventions / Type Shapes / Reflection or specific Json processing libraries - see FsCodec.*.Codec for batteries-included Coding/Decoding
[<AbstractClass; Sealed>]
type Codec =

    /// <summary>Generate an <code>IEventCodec</code> suitable using the supplied pair of <c>encode</c> and <c>decode</c> functions.</summary>
    // Leaving this helper private until we have a real use case which will e.g. enable us to decide whether to align the signature with the up/down functions
    //   employed in the convention-based Codec
    // (IME, while many systems have some code touching the metadata, it's not something one typically wants to encourage)
    static member private Create<'Event, 'Format, 'Context>
        (   // <summary>Maps an 'Event to: an Event Type Name, a pair of <c>'Format</c>'s representing the <c>Data</c> and <c>Meta</c> together with the
            // <c>eventId</c>, <c>correlationId</c>, <c>causationId</c> and <c>timestamp</c>.</summary>
            encode: Func<'Context, 'Event, struct (string * 'Format * 'Format * Guid * string * string * DateTimeOffset)>,
            // <summary>Attempts to map from an Event's stored data to <c>Some 'Event</c>, or <c>None</c> if not mappable.</summary>
            decode: Func<ITimelineEvent<'Format>, 'Event voption>)
        : IEventCodec<'Event, 'Format, 'Context> =

        { new IEventCodec<'Event, 'Format, 'Context> with
            member _.Encode(context, event) =
                let struct (eventType, data, metadata, eventId, correlationId, causationId, timestamp) = encode.Invoke(context, event)
                Core.EventData(eventType, data, metadata, eventId, correlationId, causationId, timestamp)

            member _.Decode encoded =
                decode.Invoke encoded }

    /// <summary>Generate an <c>IEventCodec</c> suitable using the supplied <c>encode</c> and <c>decode</c> functions to map to/from the stored form.
    /// <c>mapCausation</c> provides metadata generation and correlation/causationId mapping based on the <c>context</c> passed to the encoder</summary>
    static member Create<'Event, 'Format, 'Context>
        (   // Maps a fresh <c>'Event</c> resulting from the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also responsible for deriving:
            //   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c> (Default: DateTimeOffset.UtcNow).
            encode: Func<'Event, struct (string * 'Format * DateTimeOffset voption)>,
            // Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.
            decode: Func<ITimelineEvent<'Format>, 'Event voption>,
            // Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId
            mapCausation: Func<'Context, 'Event, struct ('Format * Guid * string * string)>)
        : IEventCodec<'Event, 'Format, 'Context> =

        let encode context event =
            let struct (et, d, t) = encode.Invoke event
            let ts = match t with ValueSome x -> x | ValueNone -> DateTimeOffset.UtcNow
            let struct (m, eventId, correlationId, causationId) = mapCausation.Invoke(context, event)
            struct (et, d, m, eventId, correlationId, causationId, ts)
        Codec.Create(encode, decode)

    /// <summary>Generate an <c>IEventCodec</c> using the supplied pair of <c>encode</c> and <c>decode</c> functions.</summary>
    static member Create<'Event, 'Format>
        (   // Maps a <c>'Event</c> to an Event Type Name and an encoded body (to be used as the <c>Data</c>).
            encode: Func<'Event, struct (string * 'Format)>,
            // Attempts to map an Event Type Name and an encoded <c>Data</c> to <c>Some 'Event</c> case, or <c>None</c> if not mappable.
            decode: Func<string, 'Format, 'Event voption>)
        : IEventCodec<'Event, 'Format, unit> =

        let encode' _context event =
            let struct (eventType, data: 'Format) = encode.Invoke event
            struct (eventType, data, Unchecked.defaultof<'Format> (* metadata *),
                    Guid.NewGuid() (* eventId *), null (* correlationId *), null (* causationId *), DateTimeOffset.UtcNow (* timestamp *))
        let decode' (encoded: ITimelineEvent<'Format>) = decode.Invoke(encoded.EventType, encoded.Data)
        Codec.Create(encode', decode')
