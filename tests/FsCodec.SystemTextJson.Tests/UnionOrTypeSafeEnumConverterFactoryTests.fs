module FsCodec.SystemTextJson.Tests.UnionOrTypeSafeEnumConverterFactoryTests

open FsCodec.SystemTextJson
open Swensen.Unquote

type ATypeSafeEnum = A | B | C
type NotAUnion = { body : string }
type AUnion = D of value : string | E of ATypeSafeEnum | F
type Any = Tse of enum : ATypeSafeEnum | Not of NotAUnion | Union of AUnion

let serdes = Options.Create(autoUnion = true) |> Serdes

let [<Xunit.Fact>] ``Basic characteristics`` () =
    test <@ "\"B\"" = serdes.Serialize B @>
    test <@ "{\"body\":\"A\"}" = serdes.Serialize { body = "A" } @>
    test <@ "{\"case\":\"D\",\"value\":\"A\"}" = serdes.Serialize (D "A") @>
    test <@ "{\"case\":\"Tse\",\"enum\":\"B\"}" = serdes.Serialize (Tse B) @>
    test <@ Tse B = serdes.Deserialize "{\"case\":\"Tse\",\"enum\":\"B\"}" @>
    test <@ Not { body = "A" } = serdes.Deserialize "{\"case\":\"Not\",\"body\":\"A\"}" @>

let [<FsCheck.Xunit.Property>] ``auto-encodes Unions and non-unions`` (x : Any) =
    let encoded = serdes.Serialize x
    let decoded : Any = serdes.Deserialize encoded
    test <@ decoded = x @>
