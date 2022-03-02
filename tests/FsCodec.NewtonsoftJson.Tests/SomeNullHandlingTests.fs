#if SYSTEM_TEXT_JSON
module FsCodec.SytemTextJson.Tests.SomeNullHandlingTests

open FsCodec.SystemTextJson
open Swensen.Unquote
open Xunit

let serdes = Options.Create() |> Serdes

let [<Fact>] ``Options.Create does not roundtrip Some null`` () =
    let value : string option = Some null
    let ser = serdes.Serialize value
    "null" =! ser
    // But it doesn't roundtrip
    value <>! serdes.Deserialize ser

#else
module FsCodec.NewtonsoftJson.Tests.SomeNullHandlingTests

open FsCodec.NewtonsoftJson
open Swensen.Unquote
open Xunit

let ootb = Settings.CreateDefault() |> Serdes
let serdes = Settings.Create() |> Serdes

let [<Fact>] ``Settings.CreateDefault roundtrips null string option, but rendering is ugly`` () =
    let value : string option = Some null
    let ser = ootb.Serialize value
    test <@ ser = "{\"Case\":\"Some\",\"Fields\":[null]}" @>
    test <@ value = ootb.Deserialize ser @>

let [<Fact>] ``Settings.Create does not roundtrip Some null`` () =
    let value : string option = Some null
    let ser = serdes.Serialize value
    "null" =! ser
    // But it doesn't roundtrip
    value <>! serdes.Deserialize ser
#endif

let hasSomeNull value = TypeShape.Generic.exists(fun (x : string option) -> x = Some null) value
let replaceSomeNullsWithNone value = TypeShape.Generic.map (function Some (null : string) -> None | x -> x) value

let [<Fact>] ``Workaround is to detect and/or substitute such non-roundtrippable values`` () =

    let value : string option = Some null
    // So we detect the condition (we could e.g. exclude such cases from the tests)
    test <@ hasSomeNull value @>
    // Or we can plough on, replacing the input with a roundtrippable value
    let value : string option = replaceSomeNullsWithNone value
    None =! value
    test <@ (not << hasSomeNull) value @>
    let ser = serdes.Serialize value
    ser =! "null"
    // ... and validate that the [substituted] value did roundtrip
    test <@ value = serdes.Deserialize ser @>

type RecordWithStringOptions = { x : int; y : Nested }
and Nested = { z : string option }

let [<Fact>] ``Can detect and/or substitute null string option when using Options/Settings.Create`` () =
    let value : RecordWithStringOptions = { x = 9; y = { z = Some null } }
    test <@ hasSomeNull value @>
    let value = replaceSomeNullsWithNone value
    test <@ (not << hasSomeNull) value @>
    let ser = serdes.Serialize value
    ser =! """{"x":9,"y":{"z":null}}"""
    test <@ value = serdes.Deserialize ser @>

#if SYSTEM_TEXT_JSON
    // As one might expect, the ignoreNulls setting is also honored
    let ignoreNullsSerdes = Options.Create(ignoreNulls = true) |> Serdes
#else
    // As one might expect, the ignoreNulls setting is also honored
    let ignoreNullsSerdes = Settings.Create(ignoreNulls = true) |> Serdes
#endif
    let ser = ignoreNullsSerdes.Serialize value
    ser =! """{"x":9,"y":{}}"""
    test <@ value = serdes.Deserialize ser @>
