namespace FsCodec

/// Provides Codecs that render to a UTF-8 array suitable for storage in EventStore or CosmosDb based on explicit functions you supply
/// i.e., with using conventions / Type Shapes / Reflection or specific Json processing libraries - see FsCodec.*.Codec for batteries-included Coding/Decoding
type Codec =

    /// Generate a codec suitable for use with <c>Equinox.EventStore</c>, <c>Equinox.Cosmos</c> or <c>Propulsion</c> libraries
    /// using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    // Leaving this private until someone actually asks for this (IME, while many systems have some code touching the metadata, it tends to fall into disuse)
    static member private Create<'Union>
        (   /// Maps a 'Union to an Event Type Name with UTF-8 arrays representing the `Data` and `Metadata`.
            encode : 'Union -> string * byte[] * byte[],
            /// Attempts to map from an Event Type Name and UTF-8 arrays representing the `Data` and `Metadata` to a 'Union case, or None if not mappable.
            tryDecode : string * byte[] * byte[] -> 'Union option
                                        )
        : IUnionEncoder<'Union,byte[]> =
        { new IUnionEncoder<'Union, byte[]> with
            member __.Encode e =
                let eventType, payload, metadata = encode e
                Core.EventData.Create(eventType,payload,metadata,?timestamp=None) :> _
            member __.TryDecode ee =
                tryDecode (ee.EventType, ee.Data, ee.Meta) }

    /// Generate a codec suitable for use with <c>Equinox.EventStore</c>, <c>Equinox.Cosmos</c> or <c>Propulsion</c> libraries,
    /// using the supplied pair of <c>encode</c> and <c>tryDecode</code> functions.
    static member Create<'Union>
        (   /// Maps a 'Union to an Event Type Name and a UTF-8 array representing the `Data`.
            encode : 'Union -> string * byte[],
            /// Attempts to map an Event Type Name and a UTF-8 `Data` array to a 'Union case, or None if not mappable.
            tryDecode : string * byte[] -> 'Union option
                                )
        : IUnionEncoder<'Union,byte[]> =
        let encode' value = let c, d = encode value in c, d, null
        let tryDecode' (et,d,_md) = tryDecode (et, d)
        Codec.Create(encode', tryDecode')