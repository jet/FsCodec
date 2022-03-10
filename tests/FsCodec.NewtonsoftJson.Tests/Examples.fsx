// Compile the fsproj by either a) right-clicking or b) typing
// dotnet build tests/FsCodec.NewtonsoftJson.Tests before attempting to send this to FSI with Alt-Enter

#if USE_LOCAL_BUILD
#I "bin/Debug/net5.0"
#r "FsCodec.dll"
#r "Newtonsoft.Json.dll"
#r "FsCodec.NewtonsoftJson.dll"
#r "TypeShape.dll"
#r "FSharp.UMX.dll"
#r "Serilog.dll"
#r "Serilog.Sinks.Console.dll"
#else
#r "nuget: FsCodec.NewtonsoftJson"
#r "nuget: Serilog.Sinks.Console"
#endif

open FsCodec.NewtonsoftJson
open Newtonsoft.Json
open System

module Contract =

    type Item = { value : string option }
    // implies an OptionConverter will be applied
    let private serdes = Serdes Settings.Default
    let serialize (x : Item) : string = serdes.Serialize x
    let deserialize (json : string) = serdes.Deserialize json

module Contract2 =

    type TypeThatRequiresMyCustomConverter = { mess : int }
    type MyCustomConverter() = inherit JsonPickler<string>() override _.Read(_,_) = "" override _.Write(_,_,_) = ()
    type Item = { Value : string option; other : TypeThatRequiresMyCustomConverter }
    /// Settings to be used within this contract
    // note OptionConverter is also included by default; Value field will write as `"value"`
    let private serdes = Settings.Create(MyCustomConverter(), camelCase = true) |> FsCodec.NewtonsoftJson.Serdes
    let serialize (x : Item) = serdes.Serialize x
    let deserialize (json : string) : Item = serdes.Deserialize json

let private serdes = Settings.Default |> Serdes
let inline ser x = serdes.Serialize(x)
let inline des<'t> x = serdes.Deserialize<'t>(x)

(* Global vs local Converters

It's recommended to avoid global converters, for at least the following reasons:
- they're less efficient
- they're more easy to get wrong if you have the wrong policy in place
- Explicit is better than implicit *)
type GuidConverter() =
    inherit JsonIsomorphism<Guid, string>()
    override _.Pickle g = g.ToString "N"
    override _.UnPickle g = Guid.Parse g

type WithEmbeddedGuid = { a: string; [<Newtonsoft.Json.JsonConverter(typeof<GuidConverter>)>] b: Guid }

ser { a = "testing"; b = Guid.Empty }
// {"a":"testing","b":"00000000000000000000000000000000"}

ser Guid.Empty
// "00000000-0000-0000-0000-000000000000"

let serdesWithGuidConverter = Settings.Create(converters = [| GuidConverter() |]) |> Serdes
serdesWithGuidConverter.Serialize(Guid.Empty)
// 00000000000000000000000000000000

(* TypeSafeEnumConverter basic usage *)

[<JsonConverter(typeof<TypeSafeEnumConverter>)>]
type Outcome = Joy | Pain | Misery

type Message = { name: string option; outcome: Outcome }

let value = { name = Some null; outcome = Joy}
ser value
// {"name":null,"outcome":"Joy"}

des<Message> """{"name":null,"outcome":"Joy"}"""
// val it : Message = {name = None; outcome = Joy;}

// By design, we throw when a value is unknown. Often this is the correct design.
// If, and only if, your software can do something useful with catch-all case, see the technique in `OutcomeWithOther`
try des<Message> """{"name":null,"outcome":"Discomfort"}""" with e -> printf "%A" e; Unchecked.defaultof<Message>
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
ser value2
// {"name":null,"outcome":"Joy"}

des<Message2> """{"name":null,"outcome":"Joy"}"""
// val it : Message = {name = None; outcome = Joy;}

des<Message2> """{"name":null,"outcome":"Discomfort"}"""
// val it : Message = {name = None; outcome = Other;}

(* Illustrating usage of IEventCodec and its accompanying active patterns *)

module EventCodec =

    /// Uses the supplied codec to decode the supplied event record `x` (iff at LogEventLevel.Debug, detail fails to `log` citing the `stream` and content)
    let tryDecode (codec : FsCodec.IEventCodec<_, _, _>) (log : Serilog.ILogger) streamName (x : FsCodec.ITimelineEvent<byte[]>) =
        match codec.TryDecode x with
        | None ->
            if log.IsEnabled Serilog.Events.LogEventLevel.Debug then
                log.ForContext("event", System.Text.Encoding.UTF8.GetString(x.Data), true)
                    .Debug("Codec {type} Could not decode {eventType} in {stream}", codec.GetType().FullName, x.EventType, streamName)
            None
        | x -> x

open FSharp.UMX

type ClientId = string<clientId>
and [<Measure>] clientId
module ClientId =
    let parse (str : string) : ClientId = % str
    let toString (value : ClientId) : string = % value
    let (|Parse|) = parse

module Events =

    // By convention, each contract defines a 'category' used as the first part of the stream name (e.g. `"Favorites-ClientA"`)
    let [<Literal>] Category = "Favorites"

    /// Pattern to determine whether a given {category}-{aggregateId} StreamName represents the stream associated with this Aggregate
    /// Yields a strongly typed id from the aggregateId if the Category does match
    let (|StreamName|_|) = function
        | FsCodec.StreamName.CategoryAndId (Category, ClientId.Parse clientId) -> Some clientId
        | _ -> None

    type Added = { item : string }
    type Removed = { name : string }
    type Event =
        | Added of Added
        | Removed of Removed
        interface TypeShape.UnionContract.IUnionContract

    let codec = FsCodec.NewtonsoftJson.Codec.Create<Event>()

    let (|TryDecode|_|) stream = EventCodec.tryDecode codec Serilog.Log.Logger stream
    /// Yields decoded event and relevant strongly typed id if the category of the Stream Name is correct
    let (|Match|_|) (streamName, event) =
        match streamName, event with
        | StreamName clientId, TryDecode streamName e -> Some (clientId, e)
        | _ -> None

    let (|Decode|) stream = Seq.choose ((|TryDecode|_|) stream)
    /// Yields decoded events and relevant strongly typed id if the category of the StreamName is correct
    let (|Parse|_|) (streamName, span) =
        match streamName, span with
        | StreamName clientId, Decode streamName es -> Some (clientId, es)
        | _ -> None

open FsCodec

let utf8 (s : string) = System.Text.Encoding.UTF8.GetBytes(s)
let events = [
    StreamName.parse "Favorites-ClientA",    FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "a" }""")
    StreamName.parse "Favorites-ClientB",    FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "b" }""")
    StreamName.parse "Favorites-ClientA",    FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "b" }""")
    StreamName.parse "Favorites-ClientB",    FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "a" }""")
    StreamName.parse "Favorites-ClientB",    FsCodec.Core.TimelineEvent.Create(2L, "Removed",   utf8 """{ "item": "a" }""")
    StreamName.create "Favorites" "ClientB", FsCodec.Core.TimelineEvent.Create(3L, "Exported",  utf8 """{ "count": 2 }""")
    StreamName.parse "Misc-x",               FsCodec.Core.TimelineEvent.Create(0L, "Dummy",     utf8 """{ "item": "z" }""")
]

// Explicit matching
let runCodec () =
    for stream, event in events do
        match stream, event with
        | StreamName.CategoryAndId (Events.Category, ClientId.Parse id), Events.TryDecode stream e ->
            printfn "Client %s, event %A" (ClientId.toString id) e
        | StreamName.CategoryAndId (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType

runCodec ()

let runCodecCleaner () =
    for stream, event in events do
        match stream, event with
        | Events.Match (clientId, event) ->
            printfn "Client %s, event %A" (ClientId.toString clientId) event
        | FsCodec.StreamName.CategoryAndId (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType

runCodecCleaner ()

// Switch on debug logging to get detailed information about events that don't match (which has no significant perf cost when not switched on)
open Serilog
open Serilog.Events
let outputTemplate = "{Message} {Properties}{NewLine}"
Serilog.Log.Logger <-
    LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console(LogEventLevel.Debug, outputTemplate=outputTemplate)
        .CreateLogger()

runCodec ()
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

(* Decoding contextual information

   Events arriving from a store (e.g. Equinox etc) or source (e.g. Propulsion) bear contextual information.
   Where relevant, a decoding process may want to extract such context alongside mapping the base information.
*)

module Reactions =

    type Event = int64 * DateTimeOffset * Events.Event

    let codec =
        let up (raw : FsCodec.ITimelineEvent<byte[]>, contract : Events.Event) : Event = raw.Index, raw.Timestamp, contract
        let down ((_index, timestamp, event) : Event) = event, None, Some timestamp
        FsCodec.NewtonsoftJson.Codec.Create(up, down)

    (* Helpers for parsing individual events *)

    let (|TryDecode|_|) stream event : Event option = EventCodec.tryDecode codec Serilog.Log.Logger stream event
    let (|Match|_|) (streamName, event) =
        match streamName, event with
        | Events.StreamName clientId, TryDecode streamName event -> Some (clientId, event)
        | _ -> None

    (* Helpers for parsing spans of events (as presented by Propulsion) *)

    let (|Decode|) stream = Seq.choose ((|TryDecode|_|) stream)
    let (|Parse|_|) (streamName, span) =
        match streamName, span with
        | Events.StreamName clientId, Decode streamName es -> Some (clientId, es)
        | _ -> None

let runWithContext () =
    for stream, event in events do
        match stream, event with
        | Reactions.Match (clientId, (index, ts, e)) ->
            printfn "Client %s index %d time %O event %A" (ClientId.toString clientId) index (ts.ToString "u") e
        | FsCodec.StreamName.CategoryAndId (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType

runWithContext ()
(*
Client ClientA index 0 time 2020-01-13 09:44:37Z event Added {item = "a";}
Client ClientB index 0 time 2020-01-13 09:44:37Z event Added {item = "b";}
Client ClientA index 1 time 2020-01-13 09:44:37Z event Added {item = "b";}
Client ClientB index 1 time 2020-01-13 09:44:37Z event Added {item = "a";}
Client ClientB index 2 time 2020-01-13 09:44:37Z event Removed {name = null;}
Codec "<Snipped>" Could not decode "Exported" in "Favorites-ClientB" {event="{ \"count\": 2 }"}
Unhandled Event: Category Favorites, Id ClientB, Index 3, Event: "Exported"
Unhandled Event: Category Misc, Id x, Index 0, Event: "Dummy"
*)
