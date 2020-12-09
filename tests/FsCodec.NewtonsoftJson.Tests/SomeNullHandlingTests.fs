module FsCodec.NewtonsoftJson.Tests.SomeNullHandlingTests

open FsCodec.NewtonsoftJson
open Swensen.Unquote
open Xunit

let def = Settings.CreateDefault()

let [<Fact>] ``Settings.CreateDefault roundtrips null string option, but rendering is ugly`` () =
    let value : string option = Some null
    let ser = Serdes.Serialize(value, def)
    test <@ ser = "{\"Case\":\"Some\",\"Fields\":[null]}" @>
    test <@ value = Serdes.Deserialize(ser, def) @>

let [<Fact>] ``Settings.Create does not roundtrip Some null`` () =
    let value : string option = Some null
    let ser = Serdes.Serialize value
    "null" =! ser
    // But it doesn't roundtrip
    value <>! Serdes.Deserialize ser

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
    let ser = Serdes.Serialize value
    ser =! "null"
    // ... and validate that the [substituted] value did roundtrip
    test <@ value = Serdes.Deserialize ser @>

type RecordWithStringOptions = { x : int; y : Nested }
and Nested = { z : string option }

let [<Fact>] ``Can detect and/or substitute null string option when using Settings.Create`` () =
    let value : RecordWithStringOptions = { x = 9; y = { z = Some null } }
    test <@ hasSomeNull value @>
    let value = replaceSomeNullsWithNone value
    test <@ (not << hasSomeNull) value @>
    let ser = Serdes.Serialize value
    ser =! """{"x":9,"y":{"z":null}}"""
    test <@ value = Serdes.Deserialize ser @>

    // As one might expect, the ignoreNulls setting is also honored
    let ignoreNullsSettings = Settings.Create(ignoreNulls=true)
    let ser = Serdes.Serialize(value,ignoreNullsSettings)
    ser =! """{"x":9,"y":{}}"""
    test <@ value = Serdes.Deserialize ser @>
