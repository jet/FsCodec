open Newtonsoft.Json

// Compile the fsproj by either a) right-clicking or b) typing
// dotnet build tests/FsCodec.NewtonsoftJson.Testss before attempting to send this to FSI with Alt-Enter
#if VISUALSTUDIO
#r "netstandard"
#endif
#I "bin/Debug/net461"
#r "Newtonsoft.Json.dll"
#r "TypeShape.dll"
#r "FsCodec.dll"
#r "FsCodec.NewtonsoftJson.dll"

open FsCodec.NewtonsoftJson
open System

module Contract =

    type Item = { value : string option }
    // implies default settings from Settings.Create(), which includes OptionConverter
    let serialize (x : Item) : string = FsCodec.NewtonsoftJson.Serdes.Serialize x
    // implies default settings from Settings.Create(), which includes OptionConverter
    let deserialize (json : string) = FsCodec.NewtonsoftJson.Serdes.Deserialize json

module Contract2 =

    type TypeThatRequiresMyCustomConverter = { mess : obj }
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