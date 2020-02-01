namespace FsCodec

open System

/// Provides Codecs that render to store-supported form (e.g., a UTF-8 byte array) suitable for storage in Event Stores, based on explicit functions you supply
/// Does not involve conventions / Type Shapes / Reflection or specific Json processing libraries - see FsCodec.*.Codec for batteries-included Coding/Decoding
type Codec =

    /// Generate an <code>IEventCodec</code> suitable using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    // Leaving this helper private until we have a real use case which will e.g. enable us to decide whether to align the signature with the up/down functions
    //   employed in the convention-based Codec
    // (IME, while many systems have some code touching the metadata, it's not something one typically wants to encourage)
    static member private Create<'Event, 'Format, 'Context>
        (   /// Maps an 'Event to: an Event Type Name, a pair of <>'Format</c>'s representing the <c>Data</c> and <c>Meta</c> together with the <c>correlationId</c>, <c>causationId</c> and <c>timestamp</c>.
            encode : 'Context option * 'Event -> string * 'Format * 'Format * string * string * System.DateTimeOffset option,
            /// Attempts to map from an Event's stored data to <c>Some 'Event</c>, or <c>None</c> if not mappable.
            tryDecode : ITimelineEvent<'Format> -> 'Event option)
        : IEventCodec<'Event, 'Format, 'Context> =
        { new IEventCodec<'Event, 'Format, 'Context> with
            member __.Encode(context, event) =
                let eventType, data, metadata, correlationId, causationId, timestamp = encode (context, event)
                Core.EventData.Create(eventType, data, metadata, correlationId, causationId, ?timestamp = timestamp)
            member __.TryDecode encoded =
                tryDecode encoded }

    /// Generate an <code>IEventCodec</code> suitable using the supplied <c>encode</c> and <c>tryDecode</code> functions to map to/from the stored form.
    /// <c>mapCausation</c> provides metadata generation and correlation/causationId mapping based on the <c>context</c> passed to the encoder
    static member Create<'Context, 'Event, 'Format>
        (   /// Maps a fresh <c>'Event</c> resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            encode : 'Event -> string * 'Format * DateTimeOffset option,
            /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the <c>'Event</c> representation (typically a Discriminated Union) that is to be presented to the programming model.
            tryDecode : FsCodec.ITimelineEvent<'Format> -> 'Event option,
            /// Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId
            mapCausation : 'Context option * 'Event -> 'Format * string * string)
        : FsCodec.IEventCodec<'Event, 'Format, 'Context> =
        let encode (context, event) =
            let et, d, t = encode event
            let m, correlationId, causationId = mapCausation (context, event)
            et, d, m, correlationId, causationId, t
        Codec.Create(encode, tryDecode)

    /// Generate an <code>IEventCodec</code> using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    static member Create<'Event, 'Format when 'Format : null>
        (   /// Maps a <c>'Event</c> to an Event Type Name and a UTF-8 array representing the <c>Data</c>.
            encode : 'Event -> string * 'Format,
            /// Attempts to map an Event Type Name and a UTF-8 array <c>Data</c> to <c>Some 'Event</c> case, or <c>None</c> if not mappable.
            tryDecode : string * 'Format -> 'Event option)
        : IEventCodec<'Event, 'Format, obj> =
        let encode' (_context : obj, event) = let (et, d : 'Format) = encode event in et, d, null, null, null, None
        let tryDecode' (encoded : FsCodec.ITimelineEvent<'Format>) = tryDecode (encoded.EventType, encoded.Data)
        Codec.Create(encode', tryDecode')
