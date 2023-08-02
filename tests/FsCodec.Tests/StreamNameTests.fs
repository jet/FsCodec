module FsCodec.Tests.StreamNameTests

open FsCodec
open Swensen.Unquote
open Xunit

let [<Fact>] ``Can roundtrip composed multi-ids with embedded dashes`` () =
    let cat, e1, e2 = "Cat", "a-b", "c-d"

    let sn = StreamName.compose cat [e1;e2]

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.CategoryAndIds (scat, elems)) = sn
            scat = cat && [e1;e2] = List.ofArray elems @>

    test <@ let (StreamName.CategoryAndId (scat, aggId)) = sn
            scat = cat && aggId = "a-b_c-d" @>

let [<Fact>] ``Can roundtrip streamId with embedded dashes and underscores`` () =
    let cat, aggId = "Cat", "a-b_c-d"

    let sn = StreamName.create cat aggId

    test <@ StreamName.parse "Cat-a-b_c-d" = sn @>

    test <@ let (StreamName.CategoryAndId (scat, aggId)) = sn
            scat = cat && "a-b_c-d" = aggId @>

    test <@ let (StreamName.CategoryAndIds (scat, aggIds)) = sn
            scat = cat && ["a-b";"c-d"] = List.ofArray aggIds @>

let [<Fact>] ``StreamName parse throws given 0 separators`` () =
    raisesWith <@ StreamName.parse "Cat" @> <|
        fun (e : System.ArgumentException) ->
            <@  e.ParamName = "rawStreamName"
                && e.Message.StartsWith "Stream Name 'Cat' must contain a '-' separator" @>


let [<return: Struct>] (|StreamId2|_|) = StreamName.Parse.parse2 "Cat" string string
let [<return: Struct>] (|StreamId1|_|) = StreamName.Parse.parse "Cat" string
let [<Fact>] ``Can use parser to parse identifiers`` () =
    match StreamName.create "Cat" "a-b_c-d" with
    | StreamId2(a, b) -> test <@ a = "a-b" && b = "c-d" @>
    | _ -> failwith "Nope"

let [<Fact>] ``Does not match other categories`` () =
    match StreamName.create "Cat2" "a-b_c-d" with
    | StreamId2 _ -> failwith "Unexpected match"
    | _ -> ()

let [<Fact>] ``Does not match if there are more id's than expected`` () =
    match StreamName.create "Cat" "a-b_c-d_e-f" with
    | StreamId2 _ -> failwith "Unexpected match"
    | _ -> ()

let [<Fact>] ``Does not match if there are fewer id's than expected`` () =
    match StreamName.create "Cat" "a-b" with
    | StreamId2 _ -> failwith "Unexpected match"
    | _ -> ()

let [<Fact>] ``The single id matcher doesn't care about underscored`` () =
    match StreamName.create "Cat" "a-b_c-d_e-f" with
    | StreamId1 x -> test <@ x = "a-b_c-d_e-f" @>
    | _ -> failwith "Unexpected match"


let [<Fact>] ``It works with Guid's`` () =
    let id1 = System.Guid.NewGuid()
    let id2 = System.Guid.NewGuid()
    let parse = StreamName.Parse.parse2 "Cat" System.Guid.Parse System.Guid.Parse
    test <@ parse (StreamName.create "Cat" $"{id1}_{id2}") = ValueSome struct(id1, id2) @>
    raises <@ parse (StreamName.create "Cat" $"{id1}_NotAnActualGuid") @>
