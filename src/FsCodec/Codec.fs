namespace FsCodec

open System

/// Provides Codecs that render to store-supported form (e.g., a UTF-8 byte array) suitable for storage in Event Stores, based on explicit functions you supply
/// Does not involve conventions / Type Shapes / Reflection or specific Json processing libraries - see FsCodec.*.Codec for batteries-included Coding/Decoding
type Codec =

    /// <summary>Generate an <code>IEventCodec</code> suitable using the supplied pair of <c>encode</c> and <c>tryDecode</c> functions.</summary>
    // Leaving this helper private until we have a real use case which will e.g. enable us to decide whether to align the signature with the up/down functions
    //   employed in the convention-based Codec
    // (IME, while many systems have some code touching the metadata, it's not something one typically wants to encourage)
    static member private Create<'Event, 'Format, 'Context>
        (   // <summary>Maps an 'Event to: an Event Type Name, a pair of <c>'Format</c>'s representing the <c>Data</c> and <c>Meta</c> together with the
            // <c>eventId</c>, <c>correlationId</c>, <c>causationId</c> and <c>timestamp</c>.</summary>
            encode : struct ('Context * 'Event) -> struct (string * 'Format * 'Format * Guid * string * string * DateTimeOffset),
            // <summary>Attempts to map from an Event's stored data to <c>Some 'Event</c>, or <c>None</c> if not mappable.</summary>
            tryDecode : ITimelineEvent<'Format> -> 'Event voption)
        : IEventCodec<'Event, 'Format, 'Context> =

        { new IEventCodec<'Event, 'Format, 'Context> with
            member _.Encode(context, event) =
                let struct (eventType, data, metadata, eventId, correlationId, causationId, timestamp) = encode struct (context, event)
                Core.EventData(eventType, data, metadata, eventId, correlationId, causationId, timestamp)

            member _.TryDecode encoded =
                tryDecode encoded }

    /// <summary>Generate an <c>IEventCodec</c> suitable using the supplied <c>encode</c> and <c>tryDecode</c> functions to map to/from the stored form.
    /// <c>mapCausation</c> provides metadata generation and correlation/causationId mapping based on the <c>context</c> passed to the encoder</summary>
    static member Create<'Event, 'Format, 'Context>
        (   // Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            // The function is also expected to derive
            //   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            //   and an Event Creation <c>timestamp</c>.
            encode : 'Event -> struct (string * 'Format * DateTimeOffset voption),
            // Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            // to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.
            tryDecode : ITimelineEvent<'Format> -> 'Event voption,
            // Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId
            mapCausation : struct ('Context * 'Event) -> struct ('Format * Guid * string * string))
        : IEventCodec<'Event, 'Format, 'Context> =

        let encode struct (context, event) =
            let struct (et, d, t) = encode event
            let ts = match t with ValueSome x -> x | ValueNone -> DateTimeOffset.UtcNow
            let struct (m, eventId, correlationId, causationId) = mapCausation (context, event)
            struct (et, d, m, eventId, correlationId, causationId, ts)
        Codec.Create(encode, tryDecode)

    /// Generate an <code>IEventCodec</code> using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    static member Create<'Event, 'Format>
        (   // Maps a <c>'Event</c> to an Event Type Name and a UTF-8 array representing the <c>Data</c>.
            encode : 'Event -> struct (string * 'Format),
            // Attempts to map an Event Type Name and a UTF-8 array <c>Data</c> to <c>Some 'Event</c> case, or <c>None</c> if not mappable.
            tryDecode : struct (string * 'Format) -> 'Event voption)
        : IEventCodec<'Event, 'Format, unit> =

        let encode' struct (_context, event) =
            let struct (eventType, data : 'Format) = encode event
            struct (eventType, data, Unchecked.defaultof<'Format> (* metadata *), Guid.NewGuid() (* eventId *), null (* correlationId *), null (* causationId *), DateTimeOffset.UtcNow (* timestamp *))
        let tryDecode' (encoded : ITimelineEvent<'Format>) = tryDecode (encoded.EventType, encoded.Data)
        Codec.Create(encode', tryDecode')
