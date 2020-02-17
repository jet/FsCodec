module FsCodec.Tests.StreamNameTests

open FsCodec
open Swensen.Unquote
open Xunit

let [<Fact>] ``Can roundtrip multi-ids with embedded dashes`` () =
    let cat, e1, e2 = "Cat", "a-b", "c-d"

    let sn = StreamName.compose cat [e1;e2]

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.CategoryAndIds (scat, elems)) = sn
            scat = cat && [e1;e2] = List.ofArray elems @>

    test <@ let (StreamName.CategoryAndId (scat, aggId)) = sn
            scat = cat && aggId = "a-b_c-d" @>

let [<Fact>] ``Can roundtrip single aggregateIs with embedded dashes and underscores`` () =
    let cat, aggId = "Cat", "a-b_c-d"

    let sn = StreamName.create cat aggId

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.CategoryAndId (scat, aggId)) = sn
            scat = cat && "a-b_c-d" = aggId @>
    test <@ let (StreamName.CategoryAndIds (scat, aggIds)) = sn
            scat = cat && ["a-b";"c-d"] = List.ofArray aggIds @>
