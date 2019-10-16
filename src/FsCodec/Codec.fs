namespace FsCodec
open System

/// Provides Codecs that render to a UTF-8 array suitable for storage in Event Stores, based on explicit functions you supply
/// Does not involve conventions / Type Shapes / Reflection or specific Json processing libraries - see FsCodec.*.Codec for batteries-included Coding/Decoding
type Codec =

    /// Generate a <code>IUnionEncoder</code> Codec suitable using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    // Leaving this helper private until we have a real use case which will e.g. enable us to decide whether to align the signature with the up/down functions
    //   employed in the convention-based Codec
    // (IME, while many systems have some code touching the metadata, it's not something one typically wants to encourage)
    static member private Create<'Union,'Context>
        (   /// Maps a 'Union to an Event Type Name with UTF-8 arrays representing the <c>Data</c> and <c>Meta</c> together with the correlationId, causationId and timestamp.
            encode : 'Context option -> 'Union -> string * byte[] * byte[] * string * string * System.DateTimeOffset option,
            /// Attempts to map from an Event Type Name and UTF-8 arrays representing the <c>Data</c> and <c>Meta</c>
            ///   to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : string * byte[] -> byte[] * string * string * DateTimeOffset -> 'Union option)
        : IUnionEncoder<'Union, byte[], 'Context> =
        { new IUnionEncoder<'Union, byte[], 'Context> with
            member __.Encode(context, event) =
                let eventType, data, metadata, correlationId, causationId, timestamp = encode context event
                Core.EventData.Create(eventType, data, metadata, correlationId, causationId, ?timestamp=timestamp) :> _
            member __.TryDecode ie =
                tryDecode (ie.EventType, ie.Data) (ie.Meta, ie.CorrelationId, ie.CausationId, ie.Timestamp) }

    /// Generate a <code>IUnionEncoder</code> Codec using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    static member Create<'Union>
        (   /// Maps a <c>'Union</c> to an Event Type Name and a UTF-8 array representing the <c>Data</c>.
            encode : 'Union -> string * byte[],
            /// Attempts to map an Event Type Name and a UTF-8 <c>Data</c> array to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : string * byte[] -> 'Union option)
        : IUnionEncoder<'Union, byte[], obj> =
        let encode' _context value = let et, d = encode value in et, d, null, null, null, None
        let tryDecode' (et,d) _ = tryDecode (et, d)
        Codec.Create(encode', tryDecode')