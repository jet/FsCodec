namespace FsCodec

/// Common form for either a Domain Event or an Unfolded Event, without any context regarding its place in the timeline of events
type IEventData<'Format> =
    /// The Event Type, used to drive deserialization
    abstract member EventType : string
    /// Event body, as UTF-8 encoded JSON, protobuf etc, ready to be injected into the Store
    abstract member Data : 'Format
    /// Optional metadata (null, or same as Data, not written if missing)
    abstract member Meta : 'Format
    /// Application-generated identifier used to drive idempotent writes based on deterministic Ids and/or Request Id
    abstract member EventId : System.Guid
    /// The Correlation Id associated with the flow that generated this event. Can be `null`
    abstract member CorrelationId : string
    /// The Causation Id associated with the flow that generated this event. Can be `null`
    abstract member CausationId : string
    /// The Event's Creation Time (as defined by the writer, i.e. in a mirror, this is intended to reflect the original time)
    /// <remarks>- For EventStore, this value is not honored when writing; the server applies an authoritative timestamp when accepting the write.</remarks>
    abstract member Timestamp : System.DateTimeOffset

/// <summary>Represents a Domain Event or Unfold, together with it's 0-based <c>Index</c> in the event sequence</summary>
type ITimelineEvent<'Format> =
    inherit IEventData<'Format>
    /// The 0-based index into the event sequence of this Event
    abstract member Index : int64
    /// Application-supplied context related to the origin of this event
    abstract member Context : obj
    /// <summary>Indicates this is not a true Domain Event, but actually an Unfolded Event based on the State inferred from the Events up to and including that at <c>Index</c></summary>
    abstract member IsUnfold : bool

/// <summary>Defines an Event Contract interpreter that Encodes and/or Decodes payloads representing the known/relevant set of <c>'Event</c>s borne by a stream Category</summary>
type IEventCodec<'Event, 'Format, 'Context> =
    /// <summary>Encodes a <c>'Event</c> instance into a <c>'Format</c> representation</summary>
    abstract Encode : context: 'Context option * value: 'Event -> IEventData<'Format>
    /// <summary>Decodes a formatted representation into a <c>'Event</c> instance. Returns <c>None</c> on undefined <c>EventType</c>s</summary>
    abstract TryDecode : encoded: ITimelineEvent<'Format> -> 'Event option

namespace FsCodec.Core

open FsCodec
open System

/// An Event about to be written, see <c>IEventData<c> for further information
[<NoComparison; NoEquality>]
type EventData<'Format> private (eventType, data, meta, eventId, correlationId, causationId, timestamp) =
    static member Create(eventType, data, ?meta, ?eventId, ?correlationId, ?causationId, ?timestamp) : IEventData<'Format> =
        let meta, correlationId, causationId = defaultArg meta Unchecked.defaultof<_>, defaultArg correlationId null, defaultArg causationId null
        let eventId = match eventId with Some id -> id | None -> Guid.NewGuid()
        EventData(eventType, data, meta, eventId, correlationId, causationId, match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow) :> _

    interface FsCodec.IEventData<'Format> with
        member _.EventType = eventType
        member _.Data = data
        member _.Meta = meta
        member _.EventId = eventId
        member _.CorrelationId = correlationId
        member _.CausationId = causationId
        member _.Timestamp = timestamp

/// An Event or Unfold that's been read from a Store and hence has a defined <c>Index</c> on the Event Timeline
[<NoComparison; NoEquality>]
type TimelineEvent<'Format> private (index, isUnfold, eventType, data, meta, eventId, correlationId, causationId, timestamp, context) =
    static member Create(index, eventType, data, ?meta, ?eventId, ?correlationId, ?causationId, ?timestamp, ?isUnfold, ?context) : ITimelineEvent<'Format> =
        let isUnfold, context = defaultArg isUnfold false, defaultArg context null
        let meta, eventId = defaultArg meta Unchecked.defaultof<_>, match eventId with Some x -> x | None -> Guid.Empty
        let timestamp = match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow
        let correlationId, causationId = defaultArg correlationId null, defaultArg causationId null
        TimelineEvent(index, isUnfold, eventType, data, meta, eventId, correlationId, causationId, timestamp, context) :> _

    interface ITimelineEvent<'Format> with
        member _.Index = index
        member _.IsUnfold = isUnfold
        member _.Context = context
        member _.EventType = eventType
        member _.Data = data
        member _.Meta = meta
        member _.EventId = eventId
        member _.CorrelationId = correlationId
        member _.CausationId = causationId
        member _.Timestamp = timestamp
