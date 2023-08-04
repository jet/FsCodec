module FsCodec.Tests.StreamNameTests

open FsCodec
open Swensen.Unquote
open Xunit

let [<Fact>] ``Can roundtrip composed multi-ids with embedded dashes`` () =
    let cat, e1, e2 = "Cat", "a-b", "c-d"

    let sn = StreamName.compose cat [| e1; e2 |]

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.Split (scat, StreamId.Parse 2 elems)) = sn
            scat = cat && [| e1; e2 |] = elems @>

    test <@ let (StreamName.Split (scat, sid)) = sn
            cat = scat
            && StreamId.create "a-b_c-d" = sid
            && (e1 + StreamId.Elements.Separator + e2) = StreamId.toString sid  @>

let [<Fact>] ``Can roundtrip streamId with embedded dashes and underscores`` () =
    let cat, streamId = "Cat", "a-b_c-d"

    let sn = StreamName.create cat (StreamId.create streamId)

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.Split (sCat, sid)) = sn
            sCat = cat
            && streamId = StreamId.toString sid
            && [| "a-b"; "c-d" |] = StreamId.parse 2 sid @>

    test <@ let (StreamName.Split (sCat, StreamId.Parse 2 ids)) = sn
            sCat = cat
            && [| "a-b"; "c-d" |] = ids @>

let [<Fact>] ``StreamName parse throws given 0 separators`` () =
    raisesWith <@ StreamName.parse "Cat" @> <|
        fun (e: System.ArgumentException) ->
            <@  e.ParamName = "raw"
                && e.Message.StartsWith "Stream Name 'Cat' must contain a '-' separator" @>
