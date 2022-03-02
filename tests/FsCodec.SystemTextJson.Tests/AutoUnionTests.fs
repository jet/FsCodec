module FsCodec.SystemTextJson.Tests.AutoUnionTests

open FsCodec.SystemTextJson
open Swensen.Unquote

type ATypeSafeEnum = A | B | C
type NotAUnion = { body : string; opt : string option; list: string list }
type AUnion = D of value : string | E of ATypeSafeEnum | F | G of value : string option
type Any = Tse of enum : ATypeSafeEnum | Not of NotAUnion | Union of AUnion

let serdes = Options.Create(autoUnion = true) |> Serdes

let [<Xunit.Fact>] ``Basic characteristics`` () =
    test <@ "\"B\"" = serdes.Serialize B @>
    test <@ "{\"body\":\"A\",\"opt\":null,\"list\":[]}" = serdes.Serialize { body = "A"; opt = None ; list = [] } @>
    test <@ "{\"body\":\"A\",\"opt\":\"A\",\"list\":[\"A\"]}" = serdes.Serialize { body = "A"; opt = Some "A"; list = ["A"] } @>
    test <@ "{\"body\":\"A\",\"opt\":\"A\",\"list\":[]}" = serdes.Serialize { body = "A"; opt = Some "A"; list = [] } @>
    test <@ "{\"case\":\"D\",\"value\":\"A\"}" = serdes.Serialize (D "A") @>
    test <@ "{\"case\":\"G\",\"value\":\"A\"}" = serdes.Serialize (G (Some "A")) @>
    test <@ "{\"case\":\"Tse\",\"enum\":\"B\"}" = serdes.Serialize (Tse B) @>
    test <@ Tse B = serdes.Deserialize "{\"case\":\"Tse\",\"enum\":\"B\"}" @>
    test <@ Not { body = "A"; opt = None; list = [] } = serdes.Deserialize "{\"case\":\"Not\",\"body\":\"A\",\"list\":[]}" @>
    test <@ Not { body = "A"; opt = None; list = ["A"] } = serdes.Deserialize "{\"case\":\"Not\",\"body\":\"A\",\"list\":[\"A\"]}" @>

let [<FsCheck.Xunit.Property>] ``auto-encodes Unions and non-unions`` (x : Any) =
    let encoded = serdes.Serialize x
    let decoded : Any = serdes.Deserialize encoded

    // Special cases for (non-roundtrippable) Some null => None conversion that STJ (and NSJ OptionConverter) do
    // See next test for a debatable trick
    match decoded, x with
    | Union (G None), Union (G (Some null)) -> ()
    | Not rr, Not ({ opt = Some null } as rx) -> test <@ rr = { rx with opt = None } @>
    | _ ->

    test <@ decoded = x @>

(* 🙈 *)

let (|ReplaceSomeNullWithNone|) value = TypeShape.Generic.map (function Some (null : string) -> None | x -> x) value

let [<FsCheck.Xunit.Property>] ``Some null roundtripping hack for tests`` (ReplaceSomeNullWithNone (x : Any)) =
    let encoded = serdes.Serialize x
    let decoded : Any = serdes.Deserialize encoded
    test <@ decoded = x @>
