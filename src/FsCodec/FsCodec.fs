namespace FsCodec

/// Common form for either a Domain Event or an Unfolded Event
type IEvent<'Format> =
    /// The Event Type, used to drive deserialization
    abstract member EventType : string
    /// Event body, as UTF-8 encoded json ready to be injected into the Store
    abstract member Data : 'Format
    /// Optional metadata (null, or same as Data, not written if missing)
    abstract member Meta : 'Format
    /// The Event's Creation Time (as defined by the writer, i.e. in a mirror, this is intended to reflect the original time)
    /// <remarks>
    /// - For EventStore, this value is not honored when writing; the server applies an authoritative timestamp when accepting the write.
    /// - For Cosmos, the value is not exposed where the event `IsUnfold`.
    /// </remarks>
    abstract member Timestamp : System.DateTimeOffset

/// Defines a contract interpreter for a Discriminated Union representing a set of events borne by a stream
type IUnionEncoder<'Union, 'Format> =
    /// Encodes a union instance into a decoded representation
    abstract Encode      : value:'Union -> IEvent<'Format>
    /// Decodes a formatted representation into a union instance. Does not throw exception on format mismatches
    abstract TryDecode   : encoded:IEvent<'Format> -> 'Union option

namespace FsCodec.Core

open System

/// Represents a Domain Event or Unfold, together with it's Index in the event sequence
// Included here to enable extraction of this ancillary information (by downcasting IEvent in one's IUnionEncoder.TryDecode implementation)
// in the corner cases where this coupling is absolutely definitely better than all other approaches
type IIndexedEvent<'Format> =
    inherit FsCodec.IEvent<'Format>
    /// The index into the event sequence of this event
    abstract member Index : int64
    /// Indicates this is not a Domain Event, but actually an Unfolded Event based on the state inferred from the events up to `Index`
    abstract member IsUnfold: bool

/// An Event about to be written, see IEvent for further information
type EventData<'Format> =
    { eventType : string; data : 'Format; meta : 'Format; timestamp: DateTimeOffset }
    interface FsCodec.IEvent<'Format> with
        member __.EventType = __.eventType
        member __.Data = __.data
        member __.Meta = __.meta
        member __.Timestamp = __.timestamp

type EventData =
    static member Create(eventType, data, ?meta, ?timestamp) =
        {   eventType = eventType
            data = data
            meta = defaultArg meta null
            timestamp = match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow }