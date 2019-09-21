namespace FsCodec

/// Common form for either a Domain Event or an Unfolded Event, without any context regarding its place in the timeline of events
type IEventData<'Format> =
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

/// Represents a Domain Event or Unfold, together with it's 0-based <c>Index</c> in the event sequence
type ITimelineEvent<'Format> =
    inherit IEventData<'Format>
    /// The 0-based index into the event sequence of this Event
    abstract member Index : int64
    /// Indicates this is not a true Domain Event, but actually an Unfolded Event based on the State inferred from the Events up to and including that at <c>Index</c>
    abstract member IsUnfold : bool

/// Defines a contract interpreter for a Discriminated Union representing a set of events borne by a stream
type IUnionEncoder<'Union, 'Format> =
    /// Encodes a union instance into a decoded representation
    abstract Encode : value: 'Union -> IEventData<'Format>
    /// Decodes a formatted representation into a <c>'Union<c> instance. Does not throw exception on format mismatches
    abstract TryDecode : encoded: ITimelineEvent<'Format> -> 'Union option

namespace FsCodec.Core

open System

/// An Event about to be written, see <c>IEventData<c> for further information
[<NoComparison; NoEquality>]
type EventData<'Format> private (eventType, data, meta, timestamp) =
    static member Create(eventType, data, ?meta, ?timestamp) =
        EventData(eventType, data, defaultArg meta Unchecked.defaultof<_>, match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow)
    interface FsCodec.IEventData<'Format> with
        member __.EventType = eventType
        member __.Data = data
        member __.Meta = meta
        member __.Timestamp = timestamp

/// An Event or Unfold that's been read from a Store and hence has a defined <c>Index</c> on the Event Timeline
[<NoComparison; NoEquality>]
type TimelineEvent<'Format> private (index, isUnfold, eventType, data, meta, timestamp) =
    static member Create(index, eventType, data, ?meta, ?timestamp, ?isUnfold) =
        let isUnfold, meta = defaultArg isUnfold false, defaultArg meta Unchecked.defaultof<_>
        TimelineEvent(index, isUnfold, eventType, data, meta, match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow)
    interface FsCodec.ITimelineEvent<'Format> with
        member __.Index = index
        member __.IsUnfold = isUnfold
        member __.EventType = eventType
        member __.Data = data
        member __.Meta = meta
        member __.Timestamp = timestamp