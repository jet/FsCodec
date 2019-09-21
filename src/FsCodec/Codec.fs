namespace FsCodec

/// Provides Codecs that render to a UTF-8 array suitable for storage in Event Stores, based on explicit functions you supply
/// Does not involve conventions / Type Shapes / Reflection or specific Json processing libraries - see FsCodec.*.Codec for batteries-included Coding/Decoding
type Codec =

    /// Generate a <code>IUnionEncoder</code> Codec suitable using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    // Leaving this helper private until we have a real use case which will e.g. enable us to decide whether to align the signature with the up/down functions
    //   employed in the convention-based Codec
    // (IME, while many systems have some code touching the metadata, it's not something one typically wants to encourage)
    static member private Create<'Union>
        (   /// Maps a 'Union to an Event Type Name with UTF-8 arrays representing the <c>Data</c> and <c>Meta</c>.
            encode : 'Union -> string * byte[] * byte[],
            /// Attempts to map from an Event Type Name and UTF-8 arrays representing the <c>Data</c> and <c>Meta</c>
            ///   to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : string * byte[] * byte[] -> 'Union option)
        : IUnionEncoder<'Union,byte[]> =
        { new IUnionEncoder<'Union, byte[]> with
            member __.Encode e =
                let eventType, payload, metadata = encode e
                Core.EventData.Create(eventType, payload, metadata, ?timestamp=None) :> _
            member __.TryDecode ie =
                tryDecode (ie.EventType, ie.Data, ie.Meta) }

    /// Generate a <code>IUnionEncoder</code> Codec using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    static member Create<'Union>
        (   /// Maps a <c>'Union</c> to an Event Type Name and a UTF-8 array representing the <c>Data</c>.
            encode : 'Union -> string * byte[],
            /// Attempts to map an Event Type Name and a UTF-8 <c>Data</c> array to a <c>'Union</c> case, or <c>None</c> if not mappable.
            tryDecode : string * byte[] -> 'Union option)
        : IUnionEncoder<'Union,byte[]> =
        let encode' value = let c, d = encode value in c, d, null
        let tryDecode' (et,d,_md) = tryDecode (et, d)
        Codec.Create(encode', tryDecode')