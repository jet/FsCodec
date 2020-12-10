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
    let ootbOptions = Options.CreateDefault()

    let [<Fact>] ``OOTB STJ records`` () =
        // Ver 5.x includes standard support for calling a single ctor (4.x required a custom implementation)
        let value = { a = 1 }
        let ser = Serdes.Serialize(value, ootbOptions)
        test <@ ser = """{"a":1}""" @>

        let res = Serdes.Deserialize<Record>(ser, ootbOptions)
        test <@ res = value @>

    let [<Fact>] ``OOTB STJ options`` () =
        let value = { a = 1; b = Some "str" }
        let ser = Serdes.Serialize(value, ootbOptions)
        test <@ ser = """{"a":1,"b":{"Value":"str"}}""" @>

        let correctSer = """{"a":1,"b":"str"}"""
        raisesWith <@ Serdes.Deserialize<RecordWithOption>(correctSer, ootbOptions) @>
            <| fun e -> <@ e.Message.Contains "The JSON value could not be converted to Microsoft.FSharp.Core.FSharpOption`1[System.String]" @>

    let [<Fact>] ``OOTB STJ lists`` () =
        let value = [ "A"; "B" ]
        let ser = Serdes.Serialize(value, ootbOptions)
        test <@ ser = """["A","B"]""" @>

        let correctSer = """["A,"B"]"""
        raisesWith <@ Serdes.Deserialize<string list>(correctSer, ootbOptions) @>
            <| fun e -> <@ e.Message.Contains "s abstract, an interface, or is read only, and could not be instantiated and populated" @>

    // System.Text.Json's JsonSerializerOptions by default escapes HTML-sensitive characters when generating JSON strings
    // while this arguably makes sense as a default
    // - it's not particularly relevant for event encodings
    // - and is not in alignment with the FsCodec.NewtonsoftJson default options
    // see https://github.com/dotnet/runtime/issues/28567#issuecomment-53581752 for lowdown
    let asRequiredForExamples : System.Text.Json.Serialization.JsonConverter [] = [| JsonOptionConverter() |]
    type OverescapedOptions() as this =
        inherit TheoryData<System.Text.Json.JsonSerializerOptions>()

        do // OOTB System.Text.Json over-escapes HTML-sensitive characters - `CreateDefault` honors this
           this.Add(Options.CreateDefault(converters = asRequiredForExamples)) // the value we use here requires two custom Converters
           // Options.Create provides a simple way to override it
           this.Add(Options.Create(unsafeRelaxedJsonEscaping = false))
    let [<Theory; ClassData(typedefof<OverescapedOptions>)>] ``provides various ways to use HTML-escaped encoding``(opts : System.Text.Json.JsonSerializerOptions) =
        let value = { a = 1; b = Some "\"" }
        let ser = Serdes.Serialize(value, opts)
        test <@ ser = """{"a":1,"b":"\u0022"}""" @>
        let des = Serdes.Deserialize(ser, opts)
        test <@ value = des @>

(* Serdes + default Options behavior, i.e. the stuff we do *)

let [<Fact>] records () =
    let value = { a = 1 }
    let res = Serdes.Serialize value
    test <@ res = """{"a":1}""" @>
    let des = Serdes.Deserialize res
    test <@ value = des @>

let [<Fact>] arrays () =
    let value = [|"A"; "B"|]
    let res = Serdes.Serialize value
    test <@ res = """["A","B"]""" @>
    let des = Serdes.Deserialize res
    test <@ value = des @>

let [<Fact>] options () =
    let value : RecordWithOption = { a = 1; b = Some "str" }
    let ser = Serdes.Serialize value
    test <@ ser = """{"a":1,"b":"str"}""" @>
    let des = Serdes.Deserialize<RecordWithOption> ser
    test <@ value = des @>

// For maps, represent the value as an IDictionary<'K, 'V> or Dictionary and parse into a model as appropriate
let [<Fact>] maps () =
    let value = Map(seq { "A",1; "b",2 })
    let ser = Serdes.Serialize<IDictionary<string,int>> value
    test <@ ser = """{"A":1,"b":2}""" @>
    let des = Serdes.Deserialize<IDictionary<string,int>> ser
    test <@ value = Map.ofSeq (des |> Seq.map (|KeyValue|)) @>

type RecordWithArrayOption = { str : string; arr : string[] option }
type RecordWithArrayVOption = { str : string; arr : string[] voption }

// Instead of using `list`s, it's recommended to use arrays as one would in C#
// where there's a possibility of deserializing a missing or null value, that hence maps to a `null` value
// A supported way of managing this is by wrapping the array in an `option`
let [<Fact>] ``array options`` () =
    let value = [|"A"; "B"|]
    let res = Serdes.Serialize value
    test <@ res = """["A","B"]""" @>
    let des = Serdes.Deserialize<string[] option> res
    test <@ Some value = des @>
    let des = Serdes.Deserialize<string[] option> "null"
    test <@ None = des @>
    let des = Serdes.Deserialize<RecordWithArrayVOption> "{}"
    test <@ { str = null; arr = ValueNone } = des @>

let [<Fact>] ``Switches off the HTML over-escaping mechanism`` () =
    let value = { a = 1; b = Some "\"+" }
    let ser = Serdes.Serialize value
    test <@ ser = """{"a":1,"b":"\"+"}""" @>
    let des = Serdes.Deserialize ser
    test <@ value = des @>
