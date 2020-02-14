module FsCodec.Tests.StreamNameTests

open FsCodec
open Swensen.Unquote
open Xunit

let [<Fact>] ``Can roundtrip multi-ids with embedded dashes`` () =
    let cat, e1, e2 = "Cat", "a-b", "c-d"

    let sn = StreamName.compose cat [e1;e2]

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.CategoryAndIds ("Cat", elems)) = sn
            [e1;e2] = List.ofArray elems @>

    test <@ let (StreamName.CategoryAndId ("Cat", aggId)) = sn
            aggId = "a-b_c-d" @>

let [<Fact>] ``Can roundtrip single aggregateIs with embedded dashes and underscores`` () =
    let cat, aggId = "Cat", "a-b_c-d"

    let sn = StreamName.create cat aggId

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.CategoryAndId ("Cat", aggId)) = sn
            "a-b_c-d" = aggId @>
    test <@ let (StreamName.CategoryAndIds ("Cat", aggIds)) = sn
            ["a-b";"c-d"] = List.ofArray aggIds @>

