namespace FsCodec

/// Common form for either a Domain Event or an Unfolded Event, without any context regarding its place in the timeline of events
type IEventData<'Format> =
    /// The Event Type, used to drive deserialization
    abstract member EventType: string
    /// Event body, as UTF-8 encoded JSON / protobuf etc, ready to be injected into the Store
    abstract member Data: 'Format
    /// Optional metadata (null, or same as Data, not written if missing)
    abstract member Meta: 'Format
    /// Application-generated identifier used to drive idempotent writes based on deterministic Ids and/or Request Id
    abstract member EventId: System.Guid
    /// The Correlation Id associated with the flow that generated this event. Can be `null`
    abstract member CorrelationId: string
    /// The Causation Id associated with the flow that generated this event. Can be `null`
    abstract member CausationId: string
    /// The Event's Creation Time (as defined by the writer, i.e. in a mirror, this is intended to reflect the original time)
    /// <remarks>- For EventStore, this value is not honored when writing; the server applies an authoritative timestamp when accepting the write.</remarks>
    abstract member Timestamp: System.DateTimeOffset

/// <summary>Represents a Domain Event or Unfold, together with it's 0-based <c>Index</c> in the event sequence</summary>
type ITimelineEvent<'Format> =
    inherit IEventData<'Format>
    /// The 0-based index into the event sequence of this Event
    abstract member Index: int64
    /// <summary>Indicates this is not a true Domain Event, but actually an Unfolded Event based on the State inferred from the Events up to and including that at <c>Index</c></summary>
    abstract member IsUnfold: bool
    /// Application-supplied context related to the origin of this event. Can be null.
    abstract member Context: obj
    /// The stored size of the Event
    abstract member Size: int

/// <summary>Defines an Event Contract interpreter that Encodes and/or Decodes payloads representing the known/relevant set of <c>'Event</c>s borne by a stream Category</summary>
type IEventCodec<'Event, 'Format, 'Context> =
    /// <summary>Encodes a <c>'Event</c> instance into a <c>'Format</c> representation</summary>
    abstract Encode: context: 'Context * value: 'Event -> IEventData<'Format>
    /// <summary>Decodes a formatted representation into a <c>'Event</c> instance. Returns <c>None</c> on undefined <c>EventType</c>s</summary>
    abstract Decode: encoded: ITimelineEvent<'Format> -> 'Event voption

namespace FsCodec.Core

open FsCodec
open System
open System.ComponentModel

/// <summary>An Event about to be written, see <c>IEventData</c> for further information.</summary>
[<NoComparison; NoEquality>]
type EventData<'Format>(eventType, data, meta, eventId, correlationId, causationId, timestamp) =

    static member Create(eventType, data, ?meta, ?eventId, ?correlationId, ?causationId, ?timestamp: DateTimeOffset): IEventData<'Format> =
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

    static member MapBodies<'Mapped>(f: Func<IEventData<'Format>, 'Format, 'Mapped>): Func<IEventData<'Format>, IEventData<'Mapped>> =
        Func<_, _>(fun x ->
            { new IEventData<'Mapped> with
                member _.EventType = x.EventType
                member _.Data = f.Invoke(x, x.Data)
                member _.Meta = f.Invoke(x, x.Meta)
                member _.EventId = x.EventId
                member _.CorrelationId = x.CorrelationId
                member _.CausationId = x.CausationId
                member _.Timestamp = x.Timestamp })

    // Original ugly signature
    [<Obsolete "Superseded by MapBodies / EventData.mapBodies; more importantly, the original signature mixed F# and C# types so was messy in all contexts"; EditorBrowsable(EditorBrowsableState.Never)>]
    static member Map<'Mapped>(f: Func<'Format, 'Mapped>) (x: IEventData<'Format>): IEventData<'Mapped> =
        EventData.MapBodies(Func<_, _, _>(fun _x -> f.Invoke)).Invoke(x)

/// F#-specific wrappers; for C#, use EventData.MapBodies directly
// These helper modules may move up to the FsCodec namespace in V4, along with breaking changes moving IsUnfold and Context from ITimelineEvent to IEventData
// If you have helpers that should be in the box alongside these, raise an Issue please
module EventData =

    let mapBodies_<'Format, 'Mapped> (f: IEventData<'Format> -> 'Format -> 'Mapped) =
        EventData.MapBodies(Func<IEventData<'Format>, 'Format, 'Mapped> f).Invoke
    let mapBodies<'Format, 'Mapped> (f: 'Format -> 'Mapped) =
        EventData.MapBodies(Func<IEventData<'Format>, 'Format, 'Mapped>(fun _ -> f)).Invoke

/// <summary>An Event or Unfold that's been read from a Store and hence has a defined <c>Index</c> on the Event Timeline.</summary>
[<NoComparison; NoEquality>]
type TimelineEvent<'Format>(index, eventType, data, meta, eventId, correlationId, causationId, timestamp, isUnfold, context, size) =

    static member Create(index, eventType, data, ?meta, ?eventId, ?correlationId, ?causationId, ?timestamp, ?isUnfold, ?context, ?size): ITimelineEvent<'Format> =
        let isUnfold = defaultArg isUnfold false
        let meta =     match meta      with Some x -> x   | None -> Unchecked.defaultof<_>
        let eventId =  match eventId   with Some x -> x   | None -> Guid.Empty
        let ts =       match timestamp with Some ts -> ts | None -> DateTimeOffset.UtcNow
        let size =     defaultArg size 0
        TimelineEvent(index, eventType, data, meta, eventId, Option.toObj correlationId, Option.toObj causationId, ts, isUnfold, Option.toObj context, size) :> _

    static member Create(index, inner: IEventData<'Format>, ?isUnfold, ?context, ?size): ITimelineEvent<'Format> =
        let isUnfold = defaultArg isUnfold false
        let size =     defaultArg size 0
        TimelineEvent(index, inner.EventType, inner.Data, inner.Meta, inner.EventId, inner.CorrelationId, inner.CausationId, inner.Timestamp, isUnfold, Option.toObj context, size) :> _

    override _.ToString() = sprintf "%s %s @%i" (if isUnfold then "Unfold" else "Event") eventType index

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

    static member MapBodies<'Mapped>(f: Func<ITimelineEvent<'Format>, 'Format, 'Mapped>): Func<ITimelineEvent<'Format>, ITimelineEvent<'Mapped>> =
        Func<_, _>(fun x ->
            { new ITimelineEvent<'Mapped> with
                member _.Index = x.Index
                member _.IsUnfold = x.IsUnfold
                member _.Context = x.Context
                member _.Size = x.Size
                member _.EventType = x.EventType
                member _.Data = f.Invoke(x, x.Data)
                member _.Meta = f.Invoke(x, x.Meta)
                member _.EventId = x.EventId
                member _.CorrelationId = x.CorrelationId
                member _.CausationId = x.CausationId
                member _.Timestamp = x.Timestamp })
    // Original ugly signature
    [<Obsolete "Superseded by MapBodies / TimeLineEvent.mapBodies; more importantly, the original signature mixed F# and C# types so was messy in all contexts"; EditorBrowsable(EditorBrowsableState.Never)>]
    static member Map<'Mapped>(f: Func<'Format, 'Mapped>) (x: ITimelineEvent<'Format>): ITimelineEvent<'Mapped> =
        TimelineEvent.MapBodies(Func<_, _, _>(fun _x -> f.Invoke)).Invoke(x)

/// F#-specific wrappers; for C#, use TimelineEvent.MapBodies directly
module TimelineEvent =

    let mapBodies_<'Format, 'Mapped> (f: ITimelineEvent<'Format> -> 'Format -> 'Mapped) =
        TimelineEvent.MapBodies(Func<ITimelineEvent<'Format>, 'Format, 'Mapped> f).Invoke
    let mapBodies<'Format, 'Mapped> (f: 'Format -> 'Mapped) =
        TimelineEvent.MapBodies(Func<ITimelineEvent<'Format>, 'Format, 'Mapped>(fun _ -> f)).Invoke

[<AbstractClass; Sealed>]
type EventCodec<'Event, 'Format, 'Context> private () =

    static member MapBodies<'TargetFormat>(
            native: IEventCodec<'Event, 'Format, 'Context>,
            up: Func<IEventData<'Format>, 'Format, 'TargetFormat>,
            down: Func<'TargetFormat, 'Format>)
        : IEventCodec<'Event, 'TargetFormat, 'Context> =

        let upConvert = EventData.MapBodies up
        let downConvert = TimelineEvent.MapBodies(fun _ x -> down.Invoke x)

        { new IEventCodec<'Event, 'TargetFormat, 'Context> with

            member _.Encode(context, event) =
                let encoded = native.Encode(context, event)
                upConvert.Invoke encoded

            member _.Decode target =
                let encoded = downConvert.Invoke target
                native.Decode encoded }

    // NOTE To be be replaced by MapBodies/EventCodec.mapBodies for symmetry with TimelineEvent and EventData
    // TO BE be Obsoleted and whenever FsCodec.Box is next released
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    static member Map<'TargetFormat>(native: IEventCodec<'Event, 'Format, 'Context>, up: Func<'Format, 'TargetFormat>, down: Func<'TargetFormat, 'Format>)
        : IEventCodec<'Event, 'TargetFormat, 'Context> =
        EventCodec.MapBodies(native, Func<_, _, _>(fun _x -> up.Invoke), down)

/// F#-specific wrappers; for C#, use EventCodec.MapBodies directly
module EventCodec =

    let mapBodies_ (up: IEventData<'Format> -> 'Format -> 'TargetFormat) (down: 'TargetFormat -> 'Format) x =
        EventCodec<'Event, 'Format, 'Context>.MapBodies<'TargetFormat>(x, up, down)
    let mapBodies (up: 'Format -> 'TargetFormat) (down: 'TargetFormat -> 'Format) x =
        EventCodec<'Event, 'Format, 'Context>.MapBodies<'TargetFormat>(x, Func<_, _, _>(fun _ -> up), down)
