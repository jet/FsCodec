module FsCodec.SystemTextJson.Tests.SerdesTests

open System
open System.Collections.Generic
open FsCodec.SystemTextJson
open Swensen.Unquote
open Xunit

type Record = { a : int }

type RecordWithOption = { a : int; b : string option }
type RecordWithString = { c : int; d : string }

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

    let [<Fact>] ``OOTB STJ Some null decodes as None as per NSJ`` () =
        let value = { a = 1; b = Some null }
        let ser = ootb.Serialize value
        test <@ ser = """{"a":1,"b":null}""" @>

        // sic: does not roundtrip
        test <@ { value with b = None } = ootb.Deserialize<RecordWithOption> ser @>

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

let [<Fact>] ``RejectNullStringConverter rejects null strings`` () =
    let serdes = Serdes(Options.Create(rejectNullStrings = true))

    let value: string = null
    raises<ArgumentNullException> <@ serdes.Serialize value @>

    let value = [| "A"; null |]
    raises<ArgumentNullException> <@ serdes.Serialize value @>

    let value = { c = 1; d = null }
    raises<ArgumentNullException> <@ serdes.Serialize value @>

let [<Fact>] ``RejectNullStringConverter serializes strings correctly`` () =
    let serdes = Serdes(Options.Create(rejectNullStrings = true))
    let value = { c = 1; d = "some string" }
    let res = serdes.Serialize value
    test <@ res = """{"c":1,"d":"some string"}""" @>
    let des = serdes.Deserialize res
    test <@ des = value @>

[<Theory; InlineData(true); InlineData(false)>]
let ``string options are supported regardless of "rejectNullStrings" value`` rejectNullStrings =
    let serdes = Serdes(Options.Create(rejectNullStrings = rejectNullStrings))
    let value = [| Some "A"; None |]
    let res = serdes.Serialize value
    test <@ res = """["A",null]""" @>
    let des = serdes.Deserialize res
    test <@ des = value @>


(* Serdes + default Options behavior, i.e. the stuff we do *)

let serdes = Serdes Options.Default

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
