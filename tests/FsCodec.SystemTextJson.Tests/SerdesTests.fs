module FsCodec.SystemTextJson.Tests.SerdesTests

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
        let value = { a = 1 }
        let ser =  Serdes.Serialize(value, ootbOptions)
        test <@ ser = """{"a":1}""" @>

        let res = try let v = Serdes.Deserialize(ser, ootbOptions) in Choice1Of2 v with e -> Choice2Of2 e.Message
        test <@ match res with
                | Choice1Of2 v -> v = value
                | Choice2Of2 m -> m.Contains "Deserialization of reference types without parameterless constructor is not supported. Type 'FsCodec.SystemTextJson.Tests.SerdesTests+Record'" @>

    let [<Fact>] ``OOTB STJ options`` () =
        let ootbOptionsWithRecordConverter = Options.CreateDefault(converters = [|Converters.JsonRecordConverter()|])
        let value = { a = 1; b = Some "str" }
        let ser =  Serdes.Serialize(value, ootbOptions)
        test <@ ser = """{"a":1,"b":{"Value":"str"}}""" @>
        let correctSer = """{"a":1,"b":"str"}"""
        let res = try let v = Serdes.Deserialize(correctSer, ootbOptionsWithRecordConverter) in Choice1Of2 v with e -> Choice2Of2 e.Message
        test <@ match res with
                | Choice1Of2 v -> v = value
                | Choice2Of2 m -> m.Contains "The JSON value could not be converted to Microsoft.FSharp.Core.FSharpOption`1[System.String]" @>

    // System.Text.Json's JsonSerializerOptions by default escapes HTML-sensitive characters when generating JSON strings
    // while this arguably makes sense as a default
    // - it's not particularly relevant for event encodings
    // - and is not in alignment with the FsCodec.NewtonsoftJson default options
    // see https://github.com/dotnet/runtime/issues/28567#issuecomment-53581752 for lowdown
    let asRequiredForExamples : System.Text.Json.Serialization.JsonConverter [] =
        [| Converters.JsonOptionConverter()
           Converters.JsonRecordConverter() |]
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

let [<Fact>] options () =
    let value = { a = 1; b = Some "str" }
    let ser = Serdes.Serialize value
    test <@ ser = """{"a":1,"b":"str"}""" @>
    let des = Serdes.Deserialize ser
    test <@ value = des @>

// OOTB System.Text.Json over-escapes HTML-sensitive characters; the default profile for FsCodec does not do this
let [<Fact>] ``no over-escaping`` () =
    let value = { a = 1; b = Some "\"" }
    let ser = Serdes.Serialize value
    test <@ ser = """{"a":1,"b":"\""}""" @>
    let des = Serdes.Deserialize ser
    test <@ value = des @>
