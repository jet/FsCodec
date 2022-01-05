module FsCodec.SystemTextJson.Tests.SerdesTests

open System.Collections.Generic
open FsCodec.SystemTextJson
open Swensen.Unquote
open Xunit

type Record = { a : int }

type RecordWithOption = { a : int; b : string option }

/// Characterization tests for OOTB JSON.NET
/// The aim here is to characterize the gaps that we'll shim; we only want to do that as long as it's actually warranted
module StjCharacterization =
    let ootb = Options.CreateDefault() |> Serdes

    let [<Fact>] ``OOTB STJ records Just Works`` () =
        // Ver 5.x includes standard support for calling a single ctor (4.x required a custom implementation)
        let value = { a = 1 }
        let ser = ootb.Serialize value
        test <@ ser = """{"a":1}""" @>

        let res = ootb.Deserialize<Record >ser
        test <@ res = value @>

    let [<Fact>] ``OOTB STJ options Just Works`` () =
        let value = { a = 1; b = Some "str" }
        let ser = ootb.Serialize value
        test <@ ser = """{"a":1,"b":"str"}""" @>

        test <@ value = ootb.Deserialize<RecordWithOption> ser @>

    let [<Fact>] ``OOTB STJ lists Just Works`` () =
        let value = [ "A"; "B" ]
        let ser = ootb.Serialize value
        test <@ ser = """["A","B"]""" @>

        test <@ value = ootb.Deserialize<string list> ser @>

    // System.Text.Json's JsonSerializerOptions by default escapes HTML-sensitive characters when generating JSON strings
    // while this arguably makes sense as a default
    // - it's not particularly relevant for event encodings
    // - and is not in alignment with the FsCodec.NewtonsoftJson default options
    // see https://github.com/dotnet/runtime/issues/28567#issuecomment-53581752 for lowdown
    type OverescapedOptions() as this =
        inherit TheoryData<System.Text.Json.JsonSerializerOptions>()

        do // OOTB System.Text.Json over-escapes HTML-sensitive characters - `CreateDefault` honors this
           this.Add(Options.CreateDefault()) // the value we use here one required two custom Converters
           // Options.Create provides a simple way to override it
           this.Add(Options.Create(unsafeRelaxedJsonEscaping = false))
    let [<Theory; ClassData(typedefof<OverescapedOptions>)>] ``provides various ways to use HTML-escaped encoding``(opts : System.Text.Json.JsonSerializerOptions) =
        let value = { a = 1; b = Some "\"" }
        let serdes = Serdes opts
        let ser = serdes.Serialize value
        test <@ ser = """{"a":1,"b":"\u0022"}""" @>
        let des = serdes.Deserialize ser
        test <@ value = des @>

(* Serdes + default Options behavior, i.e. the stuff we do *)

let serdes = Options.Create() |> Serdes

let [<Fact>] records () =
    let value = { a = 1 }
    let res = serdes.Serialize value
    test <@ res = """{"a":1}""" @>
    let des = serdes.Deserialize res
    test <@ value = des @>

let [<Fact>] arrays () =
    let value = [|"A"; "B"|]
    let res = serdes.Serialize value
    test <@ res = """["A","B"]""" @>
    let des = serdes.Deserialize res
    test <@ value = des @>

let [<Fact>] options () =
    let value : RecordWithOption = { a = 1; b = Some "str" }
    let ser = serdes.Serialize value
    test <@ ser = """{"a":1,"b":"str"}""" @>
    let des = serdes.Deserialize<RecordWithOption> ser
    test <@ value = des @>

// For maps, represent the value as an IDictionary<'K, 'V> or Dictionary and parse into a model as appropriate
let [<Fact>] maps () =
    let value = Map(seq { "A",1; "b",2 })
    let ser = serdes.Serialize<IDictionary<string,int>> value
    test <@ ser = """{"A":1,"b":2}""" @>
    let des = serdes.Deserialize<IDictionary<string,int>> ser
    test <@ value = Map.ofSeq (des |> Seq.map (|KeyValue|)) @>

type RecordWithArrayOption = { str : string; arr : string[] option }
type RecordWithArrayVOption = { str : string; arr : string[] voption }

// Instead of using `list`s, it's recommended to use arrays as one would in C#
// where there's a possibility of deserializing a missing or null value, that hence maps to a `null` value
// A supported way of managing this is by wrapping the array in an `option`
let [<Fact>] ``array options`` () =
    let value = [|"A"; "B"|]
    let res = serdes.Serialize value
    test <@ res = """["A","B"]""" @>
    let des = serdes.Deserialize<string[] option> res
    test <@ Some value = des @>
    let des = serdes.Deserialize<string[] option> "null"
    test <@ None = des @>
    let des = serdes.Deserialize<RecordWithArrayVOption> "{}"
    test <@ { str = null; arr = ValueNone } = des @>

let [<Fact>] ``Switches off the HTML over-escaping mechanism`` () =
    let value = { a = 1; b = Some "\"+" }
    let ser = serdes.Serialize value
    test <@ ser = """{"a":1,"b":"\"+"}""" @>
    let des = serdes.Deserialize ser
    test <@ value = des @>
