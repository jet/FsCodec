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
