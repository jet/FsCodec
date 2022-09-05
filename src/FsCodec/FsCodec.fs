namespace FsCodec

/// Common form for either a Domain Event or an Unfolded Event, without any context regarding its place in the timeline of events
type IEventData<'Format> =
    /// The Event Type, used to drive deserialization
    abstract member EventType : string
    /// Event body, as UTF-8 encoded JSON / protobuf etc, ready to be injected into the Store
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
    /// <summary>Indicates this is not a true Domain Event, but actually an Unfolded Event based on the State inferred from the Events up to and including that at <c>Index</c></summary>
    abstract member IsUnfold : bool
    /// Application-supplied context related to the origin of this event. Can be null.
    abstract member Context : obj
    /// The stored size of the Event
    abstract member Size : int

/// <summary>Defines an Event Contract interpreter that Encodes and/or Decodes payloads representing the known/relevant set of <c>'Event</c>s borne by a stream Category</summary>
type IEventCodec<'Event, 'Format, 'Context> =
    /// <summary>Encodes a <c>'Event</c> instance into a <c>'Format</c> representation</summary>
    abstract Encode : context: 'Context * value : 'Event -> IEventData<'Format>
    /// <summary>Decodes a formatted representation into a <c>'Event</c> instance. Returns <c>None</c> on undefined <c>EventType</c>s</summary>
    abstract TryDecode : encoded: ITimelineEvent<'Format> -> 'Event voption

namespace FsCodec.Core

open FsCodec
open System

/// An Event about to be written, see <c>IEventData<c> for further information
[<NoComparison; NoEquality>]
type EventData<'Format>(eventType, data, meta, eventId, correlationId, causationId, timestamp) =

    static member Create(eventType, data, ?meta, ?eventId, ?correlationId, ?causationId, ?timestamp : DateTimeOffset) : IEventData<'Format> =
        let meta =    match meta      with Some x -> x   | None -> Unchecked.defaultof<'Format>
        let eventId = match eventId   with Some x -> x   | None -> Guid.NewGuid()
        let ts =      match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow
        EventData(eventType, data, meta, eventId, Option.toObj correlationId, Option.toObj causationId, ts) :> _

    interface IEventData<'Format> with
        member _.EventType = eventType
        member _.Data = data
        member _.Meta = meta
        member _.EventId = eventId
        member _.CorrelationId = correlationId
        member _.CausationId = causationId
        member _.Timestamp = timestamp

    static member Map<'Mapped>(f : 'Format -> 'Mapped)
        (x : IEventData<'Format>) : IEventData<'Mapped> =
            { new IEventData<'Mapped> with
                member _.EventType = x.EventType
                member _.Data = f x.Data
                member _.Meta = f x.Meta
                member _.EventId = x.EventId
                member _.CorrelationId = x.CorrelationId
                member _.CausationId = x.CausationId
                member _.Timestamp = x.Timestamp }

/// An Event or Unfold that's been read from a Store and hence has a defined <c>Index</c> on the Event Timeline
[<NoComparison; NoEquality>]
type TimelineEvent<'Format>(index, eventType, data, meta, eventId, correlationId, causationId, timestamp, isUnfold, context, size) =

    static member Create(index, eventType, data, ?meta, ?eventId, ?correlationId, ?causationId, ?timestamp, ?isUnfold, ?context, ?size) : ITimelineEvent<'Format> =
        let isUnfold = defaultArg isUnfold false
        let meta =    match meta      with Some x -> x   | None -> Unchecked.defaultof<_>
        let eventId = match eventId   with Some x -> x   | None -> Guid.Empty
        let ts =      match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow
        let size =    defaultArg size 0
        TimelineEvent(index, eventType, data, meta, eventId, Option.toObj correlationId, Option.toObj causationId, ts, isUnfold, Option.toObj context, size) :> _

    interface ITimelineEvent<'Format> with
        member _.Index = index
        member _.IsUnfold = isUnfold
        member _.Context = context
        member _.Size = size
        member _.EventType = eventType
        member _.Data = data
        member _.Meta = meta
        member _.EventId = eventId
        member _.CorrelationId = correlationId
        member _.CausationId = causationId
        member _.Timestamp = timestamp

    static member Map<'Mapped>(f : 'Format -> 'Mapped)
        (x : ITimelineEvent<'Format>) : ITimelineEvent<'Mapped> =
            { new ITimelineEvent<'Mapped> with
                member _.Index = x.Index
                member _.IsUnfold = x.IsUnfold
                member _.Context = x.Context
                member _.Size = x.Size
                member _.EventType = x.EventType
                member _.Data = f x.Data
                member _.Meta = f x.Meta
                member _.EventId = x.EventId
                member _.CorrelationId = x.CorrelationId
                member _.CausationId = x.CausationId
                member _.Timestamp = x.Timestamp }

[<NoComparison; NoEquality>]
type EventCodec<'Event, 'Format, 'Context> private () =

    static member Map<'TargetFormat>(native : IEventCodec<'Event, 'Format, 'Context>, up : 'Format -> 'TargetFormat, down : 'TargetFormat -> 'Format)
        : IEventCodec<'Event, 'TargetFormat, 'Context> =

        let upConvert = EventData.Map up
        let downConvert = TimelineEvent.Map down

        { new IEventCodec<'Event, 'TargetFormat, 'Context> with
            member _.Encode(context, event) =
                let encoded = native.Encode(context, event)
                upConvert encoded

            member _.TryDecode target =
                let encoded = downConvert target
                native.TryDecode encoded }
