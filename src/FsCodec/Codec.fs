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
            /// Attempts to map from an Event's Data to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : ITimelineEvent<byte[]> -> 'Union option)
        : IUnionEncoder<'Union, byte[], 'Context> =
        { new IUnionEncoder<'Union, byte[], 'Context> with
            member __.Encode(context, event) =
                let eventType, data, metadata, correlationId, causationId, timestamp = encode context event
                Core.EventData.Create(eventType, data, metadata, correlationId, causationId, ?timestamp=timestamp) :> _
            member __.TryDecode ie =
                tryDecode ie }

    /// Generate a <code>IUnionEncoder</code> Codec suitable using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    /// <c>mapCausation</c> provides correlation/causationId mapping
    static member Create<'Union,'Context>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.
            tryDecode : FsCodec.ITimelineEvent<byte[]> -> 'Union option,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            encode : 'Union -> string * byte[] * DateTimeOffset option,
            /// Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId
            mapCausation : 'Context option -> 'Union -> byte[] * string * string,
            /// Configuration to be used by the underlying <c>Newtonsoft.Json</c> Serializer when encoding/decoding. Defaults to same as <c>Settings.Create()</c>
            ?settings,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            ?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union,byte[],'Context> =
        let encode context e =
            let et, d, t = encode e
            let m, correlationId, causationId = mapCausation context e
            et, d, m, correlationId, causationId, t
        Codec.Create(encode,tryDecode)

    /// Generate a <code>IUnionEncoder</code> Codec using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    static member Create<'Union>
        (   /// Maps a <c>'Union</c> to an Event Type Name and a UTF-8 array representing the <c>Data</c>.
            encode : 'Union -> string * byte[],
            /// Attempts to map an Event Type Name and a UTF-8 <c>Data</c> array to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : string * byte[] -> 'Union option)
        : IUnionEncoder<'Union, byte[], obj> =
        let encode' _context value = let et, d = encode value in et, d, null, null, null, None
        let tryDecode' (e : FsCodec.ITimelineEvent<_>) = tryDecode (e.EventType, e.Data)
        Codec.Create(encode', tryDecode')