// Compile the fsproj by either a) right-clicking or b) typing
// dotnet build tests/FsCodec.SystemTextJson.Tests before attempting to send this to FSI with Alt-Enter

#if !USE_LOCAL_BUILD
(* Rider's FSI is not happy without the explicit references :shrug: *)
#I "bin/Debug/net6.0"
#r "FsCodec.dll"
#r "System.Text.Json.dll"
#r "FsCodec.SystemTextJson.dll"
#r "TypeShape.dll"
#r "FSharp.UMX.dll"
#r "Serilog.dll"
#r "Serilog.Sinks.Console.dll"
#else
#r "nuget: FsCodec.SystemTextJson, *-*"
#r "nuget: Serilog.Sinks.Console"
#endif

open FsCodec.SystemTextJson
type JsonConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute
open System

module Contract =

    type Item = { value: string option }
    // No special policies required as we are using standard types
    let private serdes = Serdes.Default
    let serialize (x: Item) = serdes.Serialize x
    let deserialize (json: string) = serdes.Deserialize json

module Contract2 =

    type TypeThatRequiresMyCustomConverter = { mess: int }
    type MyCustomConverter() = inherit JsonPickler<string>() override _.Read(_,_) = "" override _.Write(_,_,_) = ()
    // NOTE: Pascal-cased field that needs to be converted to camelCase, see `camelCase = true`
    type Item = { Value: string option; other: TypeThatRequiresMyCustomConverter }
    // Note we add a custom converter here
    let private options = Options.Create(converters = [| MyCustomConverter() |], camelCase = true)
    let private serdes = Serdes options
    let serialize (x: Item) = serdes.Serialize x
    let deserialize (json: string) = serdes.Deserialize json

let private serdes = Serdes.Default

(* Global vs local Converters

It's recommended to avoid global converters, for at least the following reasons:
- they're less efficient
- they're more easy to get wrong if you have the wrong policy in place
- Explicit is better than implicit *)
type GuidConverter() =
    inherit JsonIsomorphism<Guid, string>()
    override _.Pickle g = g.ToString "N"
    override _.UnPickle g = Guid.Parse g

type WithEmbeddedGuid = { a: string; [<System.Text.Json.Serialization.JsonConverter(typeof<GuidConverter>)>] b: Guid }

serdes.Serialize { a = "testing"; b = Guid.Empty }
// {"a":"testing","b":"00000000000000000000000000000000"}

serdes.Serialize Guid.Empty
// "00000000-0000-0000-0000-000000000000"

let serdesWithGuidConverter = Options.Create(converters = [| GuidConverter() |]) |> Serdes
serdesWithGuidConverter.Serialize Guid.Empty
// 00000000000000000000000000000000

(* TypeSafeEnumConverter basic usage *)

[<JsonConverter(typeof<TypeSafeEnumConverter<Outcome>>)>]
type Outcome = Joy | Pain | Misery

type Message = { name: string option; outcome: Outcome }

let value = { name = Some null; outcome = Joy}
serdes.Serialize value
// {"name":null,"outcome":"Joy"}

serdes.Deserialize<Message> """{"name":null,"outcome":"Joy"}"""
// val it : Message = {name = None; outcome = Joy;}

// By design, we throw when a value is unknown. Often this is the correct design.
// If, and only if, your software can do something useful with catch-all case, see the technique in `OutcomeWithOther`
try serdes.Deserialize<Message> """{"name":null,"outcome":"Discomfort"}""" with e -> printf "%A" e; Unchecked.defaultof<Message>
// System.Collections.Generic.KeyNotFoundException: Could not find case 'Discomfort' for type 'FSI_0012+Outcome'

(* TypeSafeEnumConverter fallback

While, in general, one wants to version contracts such that invalid values simply don't arise,
  in some cases you want to explicitly handle out of range values.
Here we implement a converter as a JsonIsomorphism to achieve such a mapping *)

[<JsonConverter(typeof<OutcomeWithCatchAllConverter>)>]
type OutcomeWithOther = Joy | Pain | Misery | Other
and OutcomeWithCatchAllConverter() =
    inherit JsonIsomorphism<OutcomeWithOther, string>()
    override _.Pickle v =
        TypeSafeEnum.toString v

    override _.UnPickle json =
        json
        |> TypeSafeEnum.tryParse<OutcomeWithOther>
        |> Option.defaultValue Other

type Message2 = { name: string option; outcome: OutcomeWithOther }

let value2 = { name = Some null; outcome = Joy}
serdes.Serialize value2
// {"name":null,"outcome":"Joy"}

serdes.Deserialize<Message2> """{"name":null,"outcome":"Joy"}"""
// val it : Message = {name = None; outcome = Joy;}

serdes.Deserialize<Message2> """{"name":null,"outcome":"Discomfort"}"""
// val it : Message = {name = None; outcome = Other;}

(* Illustrating usage of IEventCodec and its accompanying active patterns *)

module Store =
    
    type Event = FsCodec.ITimelineEvent<EventBody>
    // Many stores use a ReadOnlyMemory<byte> to represent a UTF-8 encoded JSON event body
    // System.Text.Json.JsonElement can be a useful alternative where the store is JSON based
    and EventBody = ReadOnlyMemory<byte>
    and Codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> = FsCodec.IEventCodec<'E, EventBody, unit>
    
    // Opt in to:
    // - mapping Type Safe Enums (F# Unions where the cases have no bodies) to/from Strings
    // - mapping other F# Unions using the UnionConverter with default settings
    // TOCONSIDER avoid using this automatic behavior, and instead let the exception that System.Text.Json
    //            produces trigger adding a JsonConverterAttribute for each type as Documentation 
    let private options = Options.Create(autoTypeSafeEnumToJsonString = true, autoUnionToJsonObject = true)
    let serdes = Serdes options
    
    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        // NOTE: if EventBody is System.Text.Json, use CodecJsonElement instead
        Codec.Create(serdes = serdes)

(* For these tests, we want to use a strongly typed ClientId
   There are other tests that show other ways to manage this, but FSharp.UMX is clean and safe default *)

open FSharp.UMX
type ClientId = string<clientId>
and [<Measure>] clientId
module ClientId =
    let parse (str: string): ClientId = % str
    let toString (value: ClientId): string = % value
    let (|Parse|) = parse

(* Stream id generation/parsing logic. Normally kept private; Reactions module exposes relevant parsers to the wider application *)
module private Stream =
    // By convention, each contract defines a 'category' used as the first part of the stream name (e.g. `"Favorites-ClientA"`)
    let [<Literal>] Category = "Favorites"
    /// Generates a strongly typed StreamId from the supplied Id
    let id: ClientId -> FsCodec.StreamId = FsCodec.StreamId.gen ClientId.toString
    /// Maps from an app level identifier to a stream name as used when storing events in that stream
    /// Not normally necessary - typically you generate StreamIds, and you'll load from something that knows the Category
    let name: ClientId -> FsCodec.StreamName = id >> FsCodec.StreamName.create Category
    /// Inverse of `id`; decodes a StreamId into its constituent parts; throws if the presented StreamId does not adhere to the expected format
    let decodeId: FsCodec.StreamId -> ClientId = FsCodec.StreamId.dec ClientId.parse
    /// Inspects a stream name; if for this Category, decodes the elements into application level ids. Throws if it's malformed.
    let tryDecode: FsCodec.StreamName -> ClientId voption = FsCodec.StreamName.tryFind Category >> ValueOption.map decodeId

module Reaction =
    /// Active Pattern to determine whether a given {category}-{streamId} StreamName represents the stream associated with this Aggregate
    /// Yields a strongly typed id from the streamId if the Category matches
    let [<return: Struct>] (|For|_|) = Stream.tryDecode

module Events =

    type Added = { item: string }
    type Removed = { name: string }
    type Event =
        | Added of Added
        | Removed of Removed
        interface TypeShape.UnionContract.IUnionContract
    let codec = Store.codec<Event>

let utf8 (s: string) = System.Text.Encoding.UTF8.GetBytes(s) |> ReadOnlyMemory
let streamForClient c = Stream.name (ClientId.parse c)
let events = [
    Stream.name (ClientId.parse "ClientA"),                 FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "a" }""")
    streamForClient "ClientB",                              FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "b" }""")
    FsCodec.StreamName.parse "Favorites-ClientA",           FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "b" }""")
    streamForClient "ClientB",                              FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "a" }""")
    streamForClient "ClientB",                              FsCodec.Core.TimelineEvent.Create(2L, "Removed",   utf8 """{ "item": "a" }""")
    FsCodec.StreamName.compose "Favorites" [| "ClientB" |], FsCodec.Core.TimelineEvent.Create(3L, "Exported",  utf8 """{ "count": 2 }""")
    FsCodec.StreamName.parse "Misc-x",                      FsCodec.Core.TimelineEvent.Create(0L, "Dummy",     utf8 """{ "item": "z" }""")
]

// Switch on debug logging to get detailed information about events that don't match (which has no significant perf cost when not switched on)
module Log =
    open Serilog
    let outputTemplate = "{Message} {Properties}{NewLine}"
    let initWithDebugLevel () =
        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(Serilog.Events.LogEventLevel.Debug, outputTemplate=outputTemplate)
                .CreateLogger()

Log.initWithDebugLevel ()    

(* Explicit matching, showing how some ugly things get into the code if you do the streamName matching and event parsing separately *)

// When we obtain events from an event store via streaming notifications, we typically receive them as ReadOnlyMemory<byte> bodies
type Event = FsCodec.ITimelineEvent<EventBody>
and EventBody = ReadOnlyMemory<byte>
and Codec<'E> = FsCodec.IEventCodec<'E, EventBody, unit>

let streamCodec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
    Codec.Create<'E>(serdes = Store.serdes)
        
let dec = streamCodec<Events.Event>
let [<return:Struct>] (|TryDecodeEvent|_|) (codec: Codec<'E>) event = codec.TryDecode event

let runCodecExplicit () =
    for stream, event in events do
        match stream, event with
        | Reaction.For clientId, TryDecodeEvent dec e ->
            printfn $"Client %s{ClientId.toString clientId}, event %A{e}"
        | FsCodec.StreamName.Split struct (cat, sid), e ->
            printfn $"Unhandled Event: Category %s{cat}, Ids %s{FsCodec.StreamId.toString sid}, Index %d{e.Index}, Event: %A{e.EventType}"

runCodecExplicit ()

(*
Client ClientA, event Added {item = "a";}
Client ClientB, event Added {item = "b";}
Client ClientA, event Added {item = "b";}
Client ClientB, event Added {item = "a";}
Client ClientB, event Removed {name = null;}
Codec "<Snipped>" Could not decode "Exported" in "Favorites-ClientB" {event="{ \"count\": 2 }"}
Unhandled Event: Category Favorites, Id ClientB, Index 3, Event: "Exported"
Unhandled Event: Category Misc, Id x, Index 0, Event: "Dummy"
*)

(* Simplified by having a MatchSingle ActivePattern that decodes if it matches *)

module ReactionsBasic =    
   
    let dec = streamCodec<Events.Event>
    
    let (|DecodeSingle|_|): FsCodec.StreamName * Event -> (ClientId * Events.Event) option = function
        | Reaction.For clientId, TryDecodeEvent dec event -> Some (clientId, event)
        | _ -> None

let reactSingle (clientId: ClientId) (event: Events.Event) =
    printfn "Client %s, event %A" (ClientId.toString clientId) event
    
let runCodecMatch () =
    for streamName, event in events do
        match streamName, event with
        | ReactionsBasic.DecodeSingle (clientId, event) ->
            reactSingle clientId event
        | FsCodec.StreamName.Split (cat, sid), e ->
            printfn $"Unhandled Event: Category %s{cat}, Ids {FsCodec.StreamId.toString sid}, Index %d{e.Index}, Event: %s{e.EventType} "

runCodecMatch ()

(* Standard helper module used for parsing Events delivered via Streams, e.g. from Propulsion *)

module Streams =

    (* TODO if using Propulsion, you can `open Propulsion.Sinks` here
       NOTE it can still be useful to have type aliases so app wiring can refer to a terse `Streams.Event` etc *)
    
    // Events coming from streams are carried as a TimelineEvent; the body type is configurable
    type Event = FsCodec.ITimelineEvent<EventBody>
    // Propulsion's Sinks by default use ReadOnlyMemory<byte> as the storage format
    and EventBody = ReadOnlyMemory<byte>
    // the above Events can be decoded by a Codec implementing this interface
    and Codec<'E> = FsCodec.IEventCodec<'E, EventBody, unit>

    /// Generates a Codec for the specified Event Union type
    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        // Borrowing the Store serdes; frequently the events you parse can use less complex options...
        Codec.Create<'E>(serdes = Store.serdes)

    // as we know our event bodies are all UTF8 encoded JSON, we can render the string as a log event property
    // alternately, you can render the EventBody directly and ensure you have appropriate type destructuring configured
    let private render (x: EventBody): string =
        System.Text.Encoding.UTF8.GetString(x.Span)
    /// Uses the supplied codec to decode the supplied event record `x`
    /// (iff at LogEventLevel.Debug, detail fails to `log` citing the `streamName` and body)
    let tryDecode<'E> (log: Serilog.ILogger) (codec: Codec<'E>) (streamName: FsCodec.StreamName) (x: Event) =
        match codec.TryDecode x with
        | ValueNone ->
            if log.IsEnabled Serilog.Events.LogEventLevel.Debug then
                log.ForContext("event", render x.Data, true)
                    .Debug("Codec {type} Could not decode {eventType} in {stream}", codec.GetType().FullName, x.EventType, streamName)
            ValueNone
        | ValueSome x -> ValueSome x
    
    /// Attempts to decode the supplied Event using the supplied Codec
    let [<return: Struct>] (|TryDecode|_|) (codec: Codec<'E>) struct (streamName, event) =
        tryDecode Serilog.Log.Logger codec streamName event
    module Array = let inline chooseV f xs = [| for item in xs do match f item with ValueSome v -> yield v | ValueNone -> () |]
    /// Yields the subset of events that successfully decoded (could be Array.empty)
    let decode<'E> (codec: Codec<'E>) struct (streamName, events: Event[]): 'E[] =
        events |> Array.chooseV (tryDecode<'E> Serilog.Log.Logger codec streamName)
    let (|Decode|) = decode

(* When using Propulsion, Events are typically delivered as an array of contiguous events together with a StreamName
   The Decode Active Pattern decodes such a batch *)

module Reactions =    
   
    /// Active Pattern to determine whether a given {category}-{streamId} StreamName represents the stream associated with this Aggregate
    /// Yields a strongly typed id from the streamId if the Category matches
    let [<return: Struct>] (|For|_|) = Stream.tryDecode

    let dec = Streams.codec<Events.Event>
    
    /// Yields decoded events and relevant strongly typed ids if the Category of the Stream Name matches
    let [<return: Struct>] (|Decode|_|) = function
        | struct (For clientId, _) & Streams.Decode dec events -> ValueSome struct (clientId, events)
        | _ -> ValueNone

let reactStream (clientId: ClientId) (event: Events.Event[]) =
    printfn $"Client %s{ClientId.toString clientId}, events %A{event}"

let handleStream streamName events =
    match struct (streamName, events) with
    | Reactions.Decode (clientId, events) ->
        reactStream clientId events
    | FsCodec.StreamName.Split (cat, sid), _ ->
        for e in events do
        printfn $"Unhandled Event: Category %s{cat}, Id %A{sid}, Index %d{e.Index}, Event: %s{e.EventType} "

let runStreams () =
    for streamName, xs in events |> Seq.groupBy fst do
        let events = xs |> Seq.map snd |> Array.ofSeq
        handleStream streamName events

runStreams ()

(*

Client ClientA, events [|Added { item = "a" }; Added { item = "b" }|]
Codec "<Snipped>" Could not decode "Exported" in "Favorites-ClientB" {event="System.ReadOnlyMemory<Byte>[14]"}
Client ClientB, events [|Added { item = "b" }; Added { item = "a" }; Removed { name = null }|]
Unhandled Event: Category Misc, Id x, Index 0, Event: "Dummy" 

*)

(* Round-tripping contextual information to the application using an upconverter and a downconverter

   Events being round-tripped from a store (e.g. Equinox etc), typically bear most relevant information in the EventBody 
   Where relevant, a decoding process may want to extract some contextual information based on the event envelope as the body is decoded 
*)

module StoreWithMeta =

    type Event<'E> = int64 * Metadata * 'E
    and Metadata = { principal: string }
    and Codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> = FsCodec.IEventCodec<Event<'E>, Store.EventBody, unit>

    // we assume no special requirements for complex data types when deserializing the metadata, so use Default Serdes
    let private serdes = Serdes.Default
    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        // here we surface the metadata from the raw event as part of the application level event based on the stored form
        let up (raw: Store.Event) (contract: 'E): Event<'E> =
            raw.Index, serdes.Deserialize<Metadata> raw.Meta, contract
        // _index: up and down are expected to encode/decode symmetrically - when encoding, the app supplies a dummy, and the store assigns it on appending
        // the metadata is encoded as the normal bodies are
        let down ((_index, meta: Metadata, event: 'E): Event<'E>) =
            struct (event, ValueSome meta, ValueNone)
        Codec.Create<Event<'E>, 'E, Metadata>(up, down, serdes = Store.serdes) 

(*  Adding contextual information to the event metadata as it's encoded via an out of band context

    As illustrated in StoreWthMeta, in some cases the Metadata can be composed (and then round-tripped back) to the application
    logic as a natural part of the system's processing.
    
    Frequently, however, the contextual information is not actually relevant to the application logic.
    
    In such a case, we can pass a _Context_ to the Codec when encoding is taking place.
    
    An example of such a facility is Equinox's `context` argument for `Decider.createWithContext`; whenever an event is
    being encoded to go into the store, the relevant `'Context` is supplied to the Codec, where it is then supplied to a
    `mapCausation` function 
*)

module StoreWithContext =

    type Context = { correlationId: string; causationId: string; principal: string }
    and Codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> = FsCodec.IEventCodec<'E, Store.EventBody, Context voption>
    and Metadata = { principal: string }
    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        let up (_raw: Store.Event) (contract: 'E) = contract

        let down (event: 'E) =
            // Not producing any Metadata based on the application-level event in this instance
            let meta = ValueNone : Metadata voption
            let ts = ValueNone
            struct (event, meta, ts)

        let mapCausation (context: Context voption) (_downConvertedMeta: Metadata voption) =
            let eventId = Guid.NewGuid()
            let metadata, corrId, causeId =
                match context with
                | ValueNone ->
                    // In some parts of this system, we don't have a Context to pass - hence we use `Context voption`
                    // as the context type in this instance. Generally, it's recommended for this mapping function
                    // to throw in order to have each path in the system that wishes to generate events be required
                    // to supply the relevant attribution information. But, here we illustrate how to do it loosey goosey! 
                    ValueNone, null, null
                | ValueSome v ->
                    // We map the correlation/causation identifiers into the designated fields
                    // the remaining information, we save into the Event's Meta field
                    // In this instance, we don't have any metadata arising from the application level events,
                    //   but, if we did, we could merge it into the final rendered `ValueSome` we are passing down
                    let finalMeta = { principal = v.principal }
                    ValueSome finalMeta, v.correlationId, v.causationId
            struct (metadata, eventId, corrId, causeId)
        Codec.Create<'E, 'E, Metadata, Context voption>(up, down, mapCausation, serdes = Store.serdes)
        
(* Decoding contextual information from Streams Metadata

   Events arriving from a source (e.g. Propulsion) can bear contextual information
   Where relevant, a decoding process may want to extract such context alongside mapping the base information.
*)

module StreamsWithMeta =

    type Event<'E> = (struct (int64 * Metadata * 'E))
    and Metadata = { principal: string }
    and Codec<'E> = FsCodec.IEventCodec<Event<'E>, Streams.EventBody, unit>

    // we assume no special requirements for complex data types when deserializing the metadata, so use Default Options
    let private serdes = Serdes Options.Default
        
    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        // here we surface some metadata from the raw event as part of the application level type  
        let up (raw: Streams.Event) (contract: 'E): Event<'E> =
            struct (raw.Index, serdes.Deserialize<Metadata> raw.Meta, contract)
        // We are not using this codec to encode events, so we let the encoding side fail very fast
        let down _ = failwith "N/A"
        Codec.Create<Event<'E>, 'E, Metadata>(up, down, serdes = Store.serdes) 

let eventsWithMeta = seq {
    for sn, e in events ->
        let meta = utf8 """{"principal": "me"}"""
        sn, FsCodec.Core.TimelineEvent.Create(e.Index, e.EventType, e.Data, meta) }

module ReactionsWithMeta =     
    
    let dec = StreamsWithMeta.codec<Events.Event>
    let [<return: Struct>] (|Decode|_|) = function
        | struct (Reactions.For clientId, _) & Streams.Decode dec events -> ValueSome struct (clientId, events)
        | _ -> ValueNone

let reactStreamWithMeta (clientId: ClientId) (events: StreamsWithMeta.Event<Events.Event>[]) =
    for index, meta, event in events do
        printfn $"Client %s{ClientId.toString clientId}, event %i{index} meta %A{meta} event %A{event}"
    
let handleWithMeta streamName events =
    match struct (streamName, events) with
    | ReactionsWithMeta.Decode (clientId, events) ->
        reactStreamWithMeta clientId events
    | FsCodec.StreamName.Split (cat, sid), _ ->
        for e in events do
        printfn $"Unhandled Event: Category %s{cat}, Id %A{sid}, Index %d{e.Index}, Event: %s{e.EventType} "
    
let runStreamsWithMeta () =
    for streamName, xs in eventsWithMeta |> Seq.groupBy fst do
        let events = xs |> Seq.map snd |> Array.ofSeq
        handleWithMeta streamName events

runStreamsWithMeta ()

(*

Client ClientA, event 0 meta { principal = "me" } event Added { item = "a" }
Client ClientA, event 1 meta { principal = "me" } event Added { item = "b" }
Codec "<Snipped>" Could not decode "Exported" in "Favorites-ClientB" {event="System.ReadOnlyMemory<Byte>[14]"}
Client ClientB, event 0 meta { principal = "me" } event Added { item = "b" }
Client ClientB, event 1 meta { principal = "me" } event Added { item = "a" }
Client ClientB, event 2 meta { principal = "me" } event Removed { name = null }
Unhandled Event: Category Misc, Id x, Index 0, Event: "Dummy"

 *)
