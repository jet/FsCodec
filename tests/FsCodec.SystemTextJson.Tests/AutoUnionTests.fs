module FsCodec.SystemTextJson.Tests.AutoUnionTests

open FsCodec.SystemTextJson
open Swensen.Unquote

type ATypeSafeEnum = A | B | C
type NotAUnion = { body : string; opt : string option; list: string list }
type AUnion = D of value : string | E of ATypeSafeEnum | F | G of value : string option
type Any = Tse of enum : ATypeSafeEnum | Not of NotAUnion | Union of AUnion

let serdes = Options.Create(autoTypeSafeEnumToJsonString = true, autoUnionToJsonObject = true) |> Serdes

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

let [<Xunit.Fact>] ``Opting out`` () =
    let serdesDef = Serdes.Default
    let serdesT = Options.Create(autoTypeSafeEnumToJsonString = true) |> Serdes
    let serdesU = Options.Create(autoUnionToJsonObject = true) |> Serdes

    raises<System.NotSupportedException> <@ serdesU.Serialize(Tse A) @>
    raises<System.NotSupportedException> <@ serdesDef.Serialize(Tse A) @>
    test <@ Tse A = Tse A |> serdesT.Serialize |> serdesT.Deserialize @>

    raises<System.NotSupportedException> <@ serdesDef.Serialize(Union F) @>
    raises<System.NotSupportedException> <@ serdesT.Serialize(Union F) @>
    test <@ Union F = Union F |> serdesT.Serialize |> serdesT.Deserialize @>

module TypeSafeEnumConversion =

    type SimpleTruth = True | False

    let [<Xunit.Fact>] ``is case sensitive`` () =
        let serdesT = Options.Create(autoTypeSafeEnumToJsonString = true) |> Serdes
        True =! serdesT.Deserialize<SimpleTruth> "\"True\""
        raises<System.Collections.Generic.KeyNotFoundException> <@ serdesT.Deserialize<SimpleTruth> "\"true\"" @>

    module ``Overriding With Case Insensitive`` =

        [<System.Text.Json.Serialization.JsonConverter(typeof<LogicalConverter>)>]
        type Truth =
            | True | False | FileNotFound
            static member Parse: string -> Truth = TypeSafeEnum.parseF<Truth>(fun s inp -> s.Equals(inp, System.StringComparison.OrdinalIgnoreCase))
        and LogicalConverter() =
            inherit JsonIsomorphism<Truth, string>()
            override _.Pickle x = match x with FileNotFound -> "lost" | x -> TypeSafeEnum.toString x
            override _.UnPickle input = Truth.Parse input

        let [<Xunit.Fact>] ``specific converter wins`` () =
            let serdesT = Options.Create(autoTypeSafeEnumToJsonString = true) |> Serdes
            let serdesDef = Serdes.Default
            for serdes in [| serdesT; serdesDef |] do
                test <@ FileNotFound = serdes.Deserialize "\"fileNotFound\"" @>
                test <@ "\"lost\"" = serdes.Serialize FileNotFound @>

let [<FsCheck.Xunit.Property>] ``auto-encodes Unions and non-unions`` (x : Any) =
    let encoded = serdes.Serialize x
    let decoded : Any = serdes.Deserialize encoded

    // Special cases for (non roundtrip capable) Some null => None conversion that STJ (and NSJ OptionConverter) do
    // See next test for a debatable trick
    match decoded, x with
    | Union (G None), Union (G (Some null)) -> ()
    | Not rr, Not ({ opt = Some null } as rx) -> test <@ rr = { rx with opt = None } @>
    | _ ->

    test <@ decoded = x @>

let [<FsCheck.Xunit.Property>] ``It round trips`` (x: Any) =
    let encoded = serdes.Serialize x
    let decoded : Any = serdes.Deserialize encoded
    test <@ decoded = x @>
