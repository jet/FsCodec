namespace FsCodec

/// Common form for either a Domain Event or an Unfolded Event, without any context regarding its place in the timeline of events
type IEventData<'Format> =
    /// The Event Type, used to drive deserialization
    abstract member EventType : string
    /// Event body, as UTF-8 encoded JSON, protobuf etc, ready to be injected into the Store
    abstract member Data : 'Format
    /// Optional metadata (null, or same as Data, not written if missing)
    abstract member Meta : 'Format
    /// The Event's Creation Time (as defined by the writer, i.e. in a mirror, this is intended to reflect the original time)
    /// <remarks>- For EventStore, this value is not honored when writing; the server applies an authoritative timestamp when accepting the write.</remarks>
    abstract member Timestamp : System.DateTimeOffset
    /// The Correlation Id associated with the flow that generated this event. Can be `null`
    abstract member CorrelationId : string
    /// The Causation Id associated with the flow that generated this event. Can be `null`
    abstract member CausationId : string

/// Represents a Domain Event or Unfold, together with it's 0-based <c>Index</c> in the event sequence
type ITimelineEvent<'Format> =
    inherit IEventData<'Format>
    /// The 0-based index into the event sequence of this Event
    abstract member Index : int64
    /// Application-supplied context related to the origin of this event
    abstract member Context : obj
    /// Indicates this is not a true Domain Event, but actually an Unfolded Event based on the State inferred from the Events up to and including that at <c>Index</c>
    abstract member IsUnfold : bool

/// Defines an Event Contract interpreter that Encodes and/or Decodes payloads representing the known/relevant set of <c>'Event</c>s borne by a stream Category
type IEventCodec<'Event, 'Format, 'Context> =
    /// Encodes a <c>'Event</c> instance into a <c>'Format</c> representation
    abstract Encode : context: 'Context option * value: 'Event -> IEventData<'Format>
    /// Decodes a formatted representation into a <c>'Event<c> instance. Returns <c>None</c> on undefined <c>EventType</c>s
    abstract TryDecode : encoded: ITimelineEvent<'Format> -> 'Event option

namespace FsCodec.Core

open FsCodec
open System

/// An Event about to be written, see <c>IEventData<c> for further information
[<NoComparison; NoEquality>]
type EventData<'Format> private (eventType, data, meta, correlationId, causationId, timestamp) =
    static member Create(eventType, data, ?meta, ?correlationId, ?causationId, ?timestamp) : IEventData<'Format> =
        let meta, correlationId, causationId = defaultArg meta Unchecked.defaultof<_>, defaultArg correlationId null, defaultArg causationId null
        EventData(eventType, data, meta, correlationId, causationId, match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow) :> _

    interface FsCodec.IEventData<'Format> with
        member __.EventType = eventType
        member __.Data = data
        member __.Meta = meta
        member __.Timestamp = timestamp
        member __.CorrelationId = correlationId
        member __.CausationId = causationId

/// An Event or Unfold that's been read from a Store and hence has a defined <c>Index</c> on the Event Timeline
[<NoComparison; NoEquality>]
type TimelineEvent<'Format> private (index, isUnfold, eventType, data, meta, correlationId, causationId, timestamp, context) =
    static member Create(index, eventType, data, ?meta, ?correlationId, ?causationId, ?timestamp, ?isUnfold, ?context) : ITimelineEvent<'Format> =
        let meta, timestamp = defaultArg meta Unchecked.defaultof<_>, match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow
        let isUnfold, context = defaultArg isUnfold false, defaultArg context null
        let correlationId, causationId = defaultArg correlationId null, defaultArg causationId null
        TimelineEvent(index, isUnfold, eventType, data, meta, correlationId, causationId, timestamp, context) :> _

    interface FsCodec.ITimelineEvent<'Format> with
        member __.Index = index
        member __.IsUnfold = isUnfold
        member __.Context = context
        member __.EventType = eventType
        member __.Data = data
        member __.Meta = meta
        member __.Timestamp = timestamp
        member __.CorrelationId = correlationId
        member __.CausationId = causationId
