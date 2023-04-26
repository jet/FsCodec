// Compile the fsproj by either a) right-clicking or b) typing
// dotnet build tests/FsCodec.SystemTextJson.Tests before attempting to send this to FSI with Alt-Enter

#if !USE_LOCAL_BUILD
(* Rider's FSI is not happy without the explicit references :shrug: *)
#I "bin/Debug/net6.0"
#r "FsCodec.dll"
//#r "System.Text.Json.dll" // Does not work atm :(
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
open System.Text.Json
open System.Text.Json.Serialization // JsonConverter is in that namespace
open System

module Contract =

    type Item = { value : string option }
    // while no converter actually gets applied, as STJ v6 handles Options out of the box, this makes it explicit that we have a policy
    let private serdes = Serdes.Default
    // applies default settings from Options.Create(), i.e., includes UnsafeRelaxedJsonEscaping
    let serialize (x : Item) = serdes.Serialize x
    let deserialize (json : string) = serdes.Deserialize json

module Contract2 =

    type TypeThatRequiresMyCustomConverter = { mess : int }
    type MyCustomConverter() = inherit JsonPickler<string>() override _.Read(_,_) = "" override _.Write(_,_,_) = ()
    // NOTE: Pascal-cased field that needs to be converted to camelCase, see `camelCase = true`
    type Item = { Value : string option; other : TypeThatRequiresMyCustomConverter }
    // Note we add a custom converter here
    let private serdes = Options.Create(converters = [| MyCustomConverter() |], camelCase = true) |> Serdes
    let serialize (x : Item) = serdes.Serialize x
    let deserialize (json : string) = serdes.Deserialize json

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
    
    // We are encoding to JsonElement bodies for minimal allocation overhead
    type Event = FsCodec.ITimelineEvent<EventBody>
    and EventBody = JsonElement
    and Codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> = FsCodec.IEventCodec<'E, EventBody, unit>
    
    let options = Options.Create(autoTypeSafeEnumToJsonString = true, autoUnionToJsonObject = true)
    
    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> = CodecJsonElement.Create(options)

open FSharp.UMX
type ClientId = string<clientId>
and [<Measure>] clientId
module ClientId =
    let parse (str : string) : ClientId = % str
    let toString (value : ClientId) : string = % value
    let (|Parse|) = parse

// By convention, each contract defines a 'category' used as the first part of the stream name (e.g. `"Favorites-ClientA"`)
let [<Literal>] Category = "Favorites"

/// Generates a strongly typed StreamName from the supplied Id (incorporating the Category name)
let streamName (id : ClientId) = FsCodec.StreamName.create Category (ClientId.toString id)

/// Active Pattern to determine whether a given {category}-{streamId} StreamName represents the stream associated with this Aggregate
/// Yields a strongly typed id from the streamId if the Category matches
let [<return: Struct>] (|StreamName|_|) = function
    | FsCodec.StreamName.CategoryAndId (Category, ClientId.Parse clientId) -> ValueSome clientId
    | _ -> ValueNone

module Events =

    type Added = { item : string }
    type Removed = { name : string }
    type Event =
        | Added of Added
        | Removed of Removed
        interface TypeShape.UnionContract.IUnionContract
    let codec = Store.codec<Event>

let utf8 (s : string) = System.Text.Encoding.UTF8.GetBytes(s) |> ReadOnlyMemory
let streamForClient c = streamName (ClientId.parse c)
let events = [
    streamForClient "ClientA",                       FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "a" }""")
    streamForClient "ClientB",                       FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "b" }""")
    FsCodec.StreamName.parse "Favorites-ClientA",    FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "b" }""")
    streamForClient "ClientB",                       FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "a" }""")
    streamForClient "ClientB",                       FsCodec.Core.TimelineEvent.Create(2L, "Removed",   utf8 """{ "item": "a" }""")
    FsCodec.StreamName.create "Favorites" "ClientB", FsCodec.Core.TimelineEvent.Create(3L, "Exported",  utf8 """{ "count": 2 }""")
    FsCodec.StreamName.parse "Misc-x",               FsCodec.Core.TimelineEvent.Create(0L, "Dummy",     utf8 """{ "item": "z" }""")
]

// Switch on debug logging to get detailed information about events that don't match (which has no significant perf cost when not switched on)
open Serilog
let outputTemplate = "{Message} {Properties}{NewLine}"
Log.Logger <-
    LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console(Serilog.Events.LogEventLevel.Debug, outputTemplate=outputTemplate)
        .CreateLogger()

(* Explicit matching, showing how some ugly things get into the code if you do the streamName matching and event parsing separately *)

// When we obtain events from an event store via streaming notifications, we typically receive them as ReadOnlyMemory<byte> bodies
type Event = FsCodec.ITimelineEvent<EventBody>
and EventBody = ReadOnlyMemory<byte>
and Codec<'E> = FsCodec.IEventCodec<'E, EventBody, unit>

let streamCodec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
    Codec.Create<'E>(Store.options)
        
let dec = streamCodec<Events.Event>
let [<return:Struct>] (|TryDecodeEvent|_|) (codec : Codec<'E>) event = codec.TryDecode event

let runCodecExplicit () =
    for stream, event in events do
        match stream, event with
        | StreamName clientId, TryDecodeEvent dec e ->
            printfn "Client %s, event %A" (ClientId.toString clientId) e
        | FsCodec.StreamName.CategoryAndId struct (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A" cat id e.Index e.EventType

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
    
    let (|MatchSingle|_|) = function
        | StreamName clientId, TryDecodeEvent dec event -> Some (clientId, event)
        | _ -> None
        
let reactSingle (clientId : ClientId) (event : Events.Event) =
    printfn "Client %s, event %A" (ClientId.toString clientId) event
    
let runCodecMatch () =
    for streamName, event in events do
        match streamName, event with
        | ReactionsBasic.MatchSingle (clientId, event) ->
            reactSingle clientId event
        | FsCodec.StreamName.CategoryAndId (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType

runCodecMatch ()

(* Using Propulsion, events may arrive as contiguous arrays of events *)

module Streams =

    // When we obtain events from an event store via streaming notifications, we typically receive them as ReadOnlyMemory<byte> bodies
    type Event = FsCodec.ITimelineEvent<EventBody>
    and EventBody = ReadOnlyMemory<byte>
    and Codec<'E> = FsCodec.IEventCodec<'E, EventBody, unit>

    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        Codec.Create<'E>(Store.options)

    /// Uses the supplied codec to decode the supplied event record `x` (iff at LogEventLevel.Debug, detail fails to `log` citing the `stream` and content)
    let tryDecode<'E> (codec : Codec<'E>) (log : Serilog.ILogger) (streamName : FsCodec.StreamName) (x : Event) =
        match codec.TryDecode x with
        | ValueNone ->
            if log.IsEnabled Serilog.Events.LogEventLevel.Debug then
                log.ForContext("event", string x.Data, true)
                    .Debug("Codec {type} Could not decode {eventType} in {stream}", codec.GetType().FullName, x.EventType, streamName)
            None
        | ValueSome x -> Some x
    let (|TryDecode|_|) (codec : Codec<'E>) struct (streamName, event) = tryDecode codec Serilog.Log.Logger streamName event
    let decode<'E> (codec : Codec<'E>) struct (streamName, events : Event[]) : 'E[] = events |> Array.choose (tryDecode<'E> codec Serilog.Log.Logger streamName)
    let (|Decode|) = decode

(* When using Propulsion, Events are typically delivered as an array of contiguous events *)

module Reactions =    
   
    let dec = Streams.codec<Events.Event>
    
    /// Yields decoded events and relevant strongly typed ids if the Category of the Stream Name matches
    let [<return: Struct>] (|Match|_|) = function
        | struct (StreamName clientId, _) & Streams.Decode dec events -> ValueSome struct (clientId, events)
        | _ -> ValueNone
    
let reactStream (clientId : ClientId) (event : Events.Event[]) =
    printfn "Client %s, events %A" (ClientId.toString clientId) event
   
let handleStream streamName events =
    match struct (streamName, events) with
    | Reactions.Match (clientId, events) ->
        reactStream clientId events
    | FsCodec.StreamName.CategoryAndId (cat, id), _ ->
        for e in events do
        printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType
        
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

(* Roundtripping contextual information to the application using an upconverter and a downconverter

   Events being roundtripped from a store (e.g. Equinox etc), typically bear most relevant information in the EventBody 
   Where relevant, a decoding process may want to extract some contextual information based on the event envelope as the body is decoded 
*)

module StoreWithMeta =

    type Event<'E> = int64 * Metadata * 'E
    and Metadata = { principal: string }
    and Codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> = FsCodec.IEventCodec<Event<'E>, Store.EventBody, unit>

    let private deserialize<'t> (x : JsonElement) =
        JsonSerializer.Deserialize<'t>(x)
    let codec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        // here we surface the metadata from the raw event as part of the application level event based on the stored form
        let up struct (raw : Store.Event, contract : 'E) : Event<'E> =
            raw.Index, deserialize<Metadata> raw.Meta, contract
        // _index: up and down are expected to encode/decode symmetrically - when encoding, the app supplies a dummy, and the store assigns it on appending
        // the metadata is encoded as the normal bodies are
        let down ((_index, meta : Metadata, event : 'E) : Event<'E>) = struct (event, ValueSome meta, ValueNone)
        CodecJsonElement.Create<Event<'E>, 'E, Metadata>(up, down, options = Store.options) 
    
(* Decoding contextual information from Streams Metadata

   Events arriving from a source (e.g. Propulsion) can bear contextual information
   Where relevant, a decoding process may want to extract such context alongside mapping the base information.
*)

module StreamsWithMeta =

    type Event<'E> = (struct (int64 * Metadata * 'E))
    and Metadata = { principal: string }
    and Codec<'E> = FsCodec.IEventCodec<Event<'E>, Streams.EventBody, unit>

    // we assume no special requirements for complex data types when deserializing the metadata
    let serdes = Serdes Options.Default
    let private deserializeUtf8<'t> (x : ReadOnlyMemory<byte>) =
        let stream = new System.IO.MemoryStream(x.ToArray())
        serdes.DeserializeFromStream stream
    let dec<'E when 'E :> TypeShape.UnionContract.IUnionContract> : Codec<'E> =
        // here we surface some metadata from the raw event as part of the application level type  
        let up struct (raw : Streams.Event, contract : 'E) : Event<'E> =
            struct (raw.Index, deserializeUtf8<Metadata> raw.Meta, contract)
        // We are not using this codec to encode events, so we let the encoding side fail very fast
        let down _ = failwith "N/A"
        Codec.Create<Event<'E>, 'E, Metadata>(up, down, options = Store.options) 

let eventsWithMeta = seq {
    for sn, e in events ->
        let meta = utf8 """{"principal": "me"}"""
        sn, FsCodec.Core.TimelineEvent.Create(e.Index, e.EventType, e.Data, meta) }

module ReactionsWithMeta =     
    
    let dec = StreamsWithMeta.dec<Events.Event>

    let [<return: Struct>] (|Match|_|) = function
        | struct (StreamName clientId, _) & Streams.Decode dec events -> ValueSome struct (clientId, events)
        | _ -> ValueNone

let reactStreamWithMeta (clientId : ClientId) (events : StreamsWithMeta.Event<Events.Event>[]) =
    for index, meta, event in events do
        printfn "Client %s, event %i meta %A event %A" (ClientId.toString clientId) index meta event
    
let handleWithMeta streamName events =
    match struct (streamName, events) with
    | ReactionsWithMeta.Match (clientId, events) ->
        reactStreamWithMeta clientId events
    | FsCodec.StreamName.CategoryAndId (cat, id), _ ->
        for e in events do
        printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType
    
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
