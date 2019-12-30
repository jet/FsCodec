namespace FsCodec

open System

/// Provides Codecs that render to store-supported form (e.g., a UTF-8 byte array) suitable for storage in Event Stores, based on explicit functions you supply
/// Does not involve conventions / Type Shapes / Reflection or specific Json processing libraries - see FsCodec.*.Codec for batteries-included Coding/Decoding
type Codec =

    /// Generate a <code>IUnionEncoder</code> Codec suitable using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    // Leaving this helper private until we have a real use case which will e.g. enable us to decide whether to align the signature with the up/down functions
    //   employed in the convention-based Codec
    // (IME, while many systems have some code touching the metadata, it's not something one typically wants to encourage)
    static member private Create<'Union,'Format,'Context>
        (   /// Maps a 'Union to an Event Type Name, a pair of <>'Format</c>'s representing the  <c>Data</c> and <c>Meta</c> together with the <c>correlationId</c>, <c>causationId</c> and <c>timestamp</c>.
            encode : 'Context option * 'Union -> string * 'Format * 'Format * string * string * System.DateTimeOffset option,
            /// Attempts to map from an Event's stored data to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : ITimelineEvent<'Format> -> 'Union option)
        : IEventCodec<'Union, 'Format, 'Context> =
        { new IEventCodec<'Union, 'Format, 'Context> with
            member __.Encode(context, union) =
                let eventType, data, metadata, correlationId, causationId, timestamp = encode (context,union)
                Core.EventData.Create(eventType, data, metadata, correlationId, causationId, ?timestamp=timestamp) :> _
            member __.TryDecode encoded =
                tryDecode encoded }

    /// Generate a <code>IUnionEncoder</code> Codec suitable using the supplied <c>encode</c> and <c>tryDecode</code> functions to map to/from the stored form.
    /// <c>mapCausation</c> provides metadata generation and correlation/causationId mapping based on the <c>context</c> passed to the encoder
    static member Create<'Context,'Union,'Format>
        (   /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            encode : 'Union -> string * 'Format * DateTimeOffset option,
            /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.
            tryDecode : FsCodec.ITimelineEvent<'Format> -> 'Union option,
            /// Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId
            mapCausation : 'Context option * 'Union -> 'Format * string * string)
        : FsCodec.IEventCodec<'Union,'Format,'Context> =
        let encode (context,union) =
            let et, d, t = encode union
            let m, correlationId, causationId = mapCausation (context, union)
            et, d, m, correlationId, causationId, t
        Codec.Create(encode,tryDecode)

    /// Generate a <code>IUnionEncoder</code> Codec using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    static member Create<'Union,'Format when 'Format : null>
        (   /// Maps a <c>'Union</c> to an Event Type Name and a UTF-8 array representing the <c>Data</c>.
            encode : 'Union -> string * 'Format,
            /// Attempts to map an Event Type Name and a UTF-8 <c>Data</c> array to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : string * 'Format -> 'Union option)
        : IEventCodec<'Union, 'Format, obj> =
        let encode' (_context : obj,union) = let (et, d : 'Format) = encode union in et, d, null, null, null, None
        let tryDecode' (encoded : FsCodec.ITimelineEvent<'Format>) = tryDecode (encoded.EventType,encoded.Data)
        Codec.Create(encode', tryDecode')