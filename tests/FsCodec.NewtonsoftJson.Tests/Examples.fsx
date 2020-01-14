open FsCodec

// Compile the fsproj by either a) right-clicking or b) typing
// dotnet build tests/FsCodec.NewtonsoftJson.Tests before attempting to send this to FSI with Alt-Enter

#if VISUALSTUDIO
#r "netstandard"
#endif
#I "bin/Debug/net461"
#r "Newtonsoft.Json.dll"
#r "Serilog.dll"
#r "Serilog.Sinks.Console.dll"
#r "TypeShape.dll"
#r "FsCodec.dll"
#r "FsCodec.NewtonsoftJson.dll"
#r "FSharp.UMX.dll"

open FsCodec.NewtonsoftJson
open Newtonsoft.Json
open System

module Contract =

    type Item = { value : string option }
    // implies default settings from Settings.Create(), which includes OptionConverter
    let serialize (x : Item) : string = FsCodec.NewtonsoftJson.Serdes.Serialize x
    // implies default settings from Settings.Create(), which includes OptionConverter
    let deserialize (json : string) = FsCodec.NewtonsoftJson.Serdes.Deserialize json

module Contract2 =

    type TypeThatRequiresMyCustomConverter = { mess : int }
    type MyCustomConverter() = inherit JsonPickler<string>() override __.Read(_,_) = "" override __.Write(_,_,_) = ()
    type Item = { value : string option; other : TypeThatRequiresMyCustomConverter }
    /// Settings to be used within this contract
    // note OptionConverter is also included by default
    let settings = FsCodec.NewtonsoftJson.Settings.Create(converters = [| MyCustomConverter() |])
    let serialize (x : Item) = FsCodec.NewtonsoftJson.Serdes.Serialize(x,settings)
    let deserialize (json : string) : Item = FsCodec.NewtonsoftJson.Serdes.Deserialize(json,settings)

let inline ser x = Serdes.Serialize(x)
let inline des<'t> x = Serdes.Deserialize<'t>(x)

(* Global vs local Converters

It's recommended to avoid global converters, for at least the following reasons:
- they're less efficient
- they're more easy to get wrong if you have the wrong policy in place
- Explicit is better than implicit *)
type GuidConverter() =
    inherit JsonIsomorphism<Guid, string>()
    override __.Pickle g = g.ToString "N"
    override __.UnPickle g = Guid.Parse g

type WithEmbeddedGuid = { a: string; [<Newtonsoft.Json.JsonConverter(typeof<GuidConverter>)>] b: Guid }

ser { a = "testing"; b = Guid.Empty }
// {"a":"testing","b":"00000000000000000000000000000000"}

ser Guid.Empty
// "00000000-0000-0000-0000-000000000000"

let settings = Settings.Create(converters = [| GuidConverter() |])
Serdes.Serialize(Guid.Empty,settings)
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
    override __.Pickle v =
        TypeSafeEnum.toString v
    override __.UnPickle json =
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

(*

Illustrating usage of IEventCodec and its accompanying active patterns

*)

module EventCodec =

    /// Uses the supplied codec to decode the supplied event record `x` (iff at LogEventLevel.Debug, detail fails to `log` citing the `stream` and content)
    let tryDecode (codec : FsCodec.IEventCodec<_,_,_>) (log : Serilog.ILogger) (stream : string) (x : FsCodec.ITimelineEvent<byte[]>) =
        match codec.TryDecode x with
        | None ->
            if log.IsEnabled Serilog.Events.LogEventLevel.Debug then
                log.ForContext("event", System.Text.Encoding.UTF8.GetString(x.Data), true)
                    .Debug("Codec {type} Could not decode {eventType} in {stream}", codec.GetType().FullName, x.EventType, stream)
            None
        | x -> x

open FSharp.UMX

type [<Measure>] clientId
type ClientId = string<clientId>
module ClientId =
    let parse (str : string) : ClientId = % str
    let toString (value : ClientId) : string = % value

module Events =

    // By convention, each contract defines a 'category' used as the first part of the stream name (e.g. `"Favorites-ClientA"`)
    let [<Literal>] categoryId = "Favorites"
    // The second part of the stream name is the ClientId; here we define an Active Pattern to enable easy decoding of this portion into a UMX type
    let (|ClientId|) = ClientId.parse

    type Added = { item : string }
    type Removed = { name: string }
    type Event =
        | Added of Added
        | Removed of Removed
        interface TypeShape.UnionContract.IUnionContract

    let codec = FsCodec.NewtonsoftJson.Codec.Create<Event>()

    let (|Decode|_|) stream = EventCodec.tryDecode codec Serilog.Log.Logger stream

module StreamName =

    let private catSeparators = [|'-'|]
    let private split (streamName : string) = streamName.Split(catSeparators, 2, StringSplitOptions.RemoveEmptyEntries)
    let category (streamName : string) = let fragments = split streamName in fragments.[0]
    let (|Category|Other|) (streamName : string) =
        match split streamName with
        | [| category; id |] -> Category (category, id)
        | _ -> Other streamName

let utf8 (s : string) = System.Text.Encoding.UTF8.GetBytes(s)
let events = [
    "Favorites-ClientA", FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "a" }""")
    "Favorites-ClientB", FsCodec.Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "b" }""")
    "Favorites-ClientA", FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "b" }""")
    "Favorites-ClientB", FsCodec.Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "a" }""")
    "Favorites-ClientB", FsCodec.Core.TimelineEvent.Create(2L, "Removed",   utf8 """{ "item": "a" }""")
    "Favorites-ClientB", FsCodec.Core.TimelineEvent.Create(3L, "Exported",  utf8 """{ "count": 2 }""")
    "Misc-x", FsCodec.Core.TimelineEvent.Create(0L, "Dummy",   utf8 """{ "item": "z" }""")
]

let runCodec () =
    for stream, event in events do
        match stream, event with
        | StreamName.Category (Events.categoryId, Events.ClientId id), (Events.Decode stream e) ->
            printfn "Client %s, event %A" (ClientId.toString id) e
        | StreamName.Category (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType
        | StreamName.Other streamName, _e ->
            failwithf "Invalid Stream Name: %s" streamName
runCodec ()

// Switch on debug logging to get detailed information about events that don't match (which has no singificant perf cost when not switched on)
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

module EventsWithMeta =

    type EventWithMeta = int64 * DateTimeOffset * Events.Event
    let codec =
        let up (raw : FsCodec.ITimelineEvent<byte[]>, contract : Events.Event) : EventWithMeta =
            raw.Index, raw.Timestamp, contract
        let down ((_index, timestamp, event) : EventWithMeta) =
            event, None, Some timestamp
        FsCodec.NewtonsoftJson.Codec.Create(up, down)
    let (|Decode|_|) stream event : EventWithMeta option = EventCodec.tryDecode codec Serilog.Log.Logger stream event

let runWithContext () =
    for stream, event in events do
        match stream, event with
        | StreamName.Category (Events.categoryId, Events.ClientId id), (EventsWithMeta.Decode stream (index, ts, e)) ->
            printfn "Client %s index %d time %O event %A" (ClientId.toString id) index (ts.ToString "u") e
        | StreamName.Category (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType
        | StreamName.Other streamName, _e ->
            failwithf "Invalid Stream Name: %s" streamName
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