// Compile the fsproj by either a) right-clicking or b) typing
// dotnet build tests/FsCodec.NewtonsoftJson.Tests before attempting to send this to FSI with Alt-Enter

#if !USE_LOCAL_BUILD
#I "bin/Debug/net6.0"
#r "FsCodec.dll"
#r "Newtonsoft.Json.dll"
#r "FsCodec.NewtonsoftJson.dll"
#r "TypeShape.dll"
#r "Serilog.dll"
#r "Serilog.Sinks.Console.dll"
#else
#r "nuget: FsCodec.NewtonsoftJson, *-*"
#r "nuget: Serilog.Sinks.Console"
#endif

open FsCodec.NewtonsoftJson
type JsonConverterAttribute = Newtonsoft.Json.JsonConverterAttribute
open System

module Contract =

    type Item = { value : string option }
    // implies an OptionConverter will be applied
    let private serdes = Serdes.Default
    let serialize (x : Item) : string = serdes.Serialize x
    let deserialize (json : string) = serdes.Deserialize json

module Contract2 =

    type TypeThatRequiresMyCustomConverter = { mess : int }
    type MyCustomConverter() = inherit JsonPickler<string>() override _.Read(_,_) = "" override _.Write(_,_,_) = ()
    type Item = { Value : string option; other : TypeThatRequiresMyCustomConverter }
    /// Options to be used within this contract
    // note OptionConverter is also included by default; Value field will write as `"value"`
    let private options = Options.Create(MyCustomConverter(), camelCase = true)
    let private serdes = Serdes options
    let serialize (x : Item) = serdes.Serialize x
    let deserialize (json : string) : Item = serdes.Deserialize json

let serdes = Serdes.Default

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

serdes.Serialize { a = "testing"; b = Guid.Empty }
// {"a":"testing","b":"00000000000000000000000000000000"}

serdes.Serialize Guid.Empty
// "00000000-0000-0000-0000-000000000000"

let serdesWithGuidConverter = Options.Create(converters = [| GuidConverter() |]) |> Serdes
serdesWithGuidConverter.Serialize(Guid.Empty)
// 00000000000000000000000000000000

(* TypeSafeEnumConverter basic usage *)

// The default rendering, without any converters in force, is a generic rendering
// This treats the values in a manner consistent with how DU values with bodies are treated
type Status = Initial | Active
type StatusMessage = { name: string option; status: Status }
let status = { name = None; status = Initial } 
// The problems here are:
// 1. the value has lots of noise, which consumes storage space, and makes parsing harder
// 2. other languages which would naturally operate on the string value if it was presented as such will have problems parsing
// 3. it's also simply unnecessarily hard to read as a human
serdes.Serialize status
// "{"name":null,"status":{"Case":"Initial"}}"
let serdesFormatted = Serdes(Options.Create(indent = true))

// If we pretty-print it, things get worse, not better: 
serdesFormatted.Serialize(status)
// "{
//   "name": null,
//   "status": {
//     "Case": "Initial"
//   }
// }"

// We can override this with the Newtonsoft.Json.JsonConverter Attribute

open FsCodec.NewtonsoftJson
let serdes2 = Serdes.Default
[<Newtonsoft.Json.JsonConverter(typeof<TypeSafeEnumConverter>)>]
type Status2 = Initial | Active
type StatusMessage2 = { name: string option; status: Status2 }
let status2 = { name = None; status = Initial }
serdes2.Serialize status2
// "{"name":null,"status":"Initial"}"

// A single registered converter supplied when creating the Serdes can automatically map all Nullary Unions to strings:
let serdesWithConverter = Serdes(Options.Create(TypeSafeEnumConverter()))
// NOTE: no JsonConverter attribute
type Status3 = Initial | Active
type StatusMessage3 = { name: string option; status: Status3 }
let status3 = { name = None; status = Initial }
serdesWithConverter.Serialize status3
// "{"name":null,"status":"Initial"}"

[<JsonConverter(typeof<TypeSafeEnumConverter>)>]
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
        FsCodec.TypeSafeEnum.toString v

    override _.UnPickle json =
        json
        |> FsCodec.TypeSafeEnum.tryParse<OutcomeWithOther>
        |> Option.defaultValue Other

type Message2 = { name: string option; outcome: OutcomeWithOther }

let value2 = { name = Some null; outcome = Joy}
serdes.Serialize value2
// {"name":null,"outcome":"Joy"}

serdes.Deserialize<Message2> """{"name":null,"outcome":"Joy"}"""
// val it : Message = {name = None; outcome = Joy;}

serdes.Deserialize<Message2> """{"name":null,"outcome":"Discomfort"}"""
// val it : Message = {name = None; outcome = Other;}
