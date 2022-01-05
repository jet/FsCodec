module FsCodec.SystemTextJson.Tests.UnionOrTypeSafeEnumConverterFactoryTests

open FsCodec.SystemTextJson
open Swensen.Unquote
open System.Text.Json

type ATypeSafeEnum = A | B | C
type NotAUnion = { body : string }
type AUnion = D of value : string | E of ATypeSafeEnum | F
type Any = Tse of enum : ATypeSafeEnum | Not of NotAUnion | Union of AUnion

let opts = Options.Create(converters=[| UnionOrTypeSafeEnumConverterFactory() |])
let inline ser (x : 't) = JsonSerializer.Serialize<'t>(x, opts)
let inline des (x : string) : 't = JsonSerializer.Deserialize<'t>(x, opts)

let [<Xunit.Fact>] ``Basic characteristics`` () =
    test <@ "\"B\"" = ser B @>
    test <@ "{\"body\":\"A\"}" = ser { body = "A" } @>
    test <@ "{\"case\":\"D\",\"value\":\"A\"}" = ser (D "A") @>
    test <@ "{\"case\":\"Tse\",\"enum\":\"B\"}" = ser (Tse B) @>
    test <@ Tse B = des "{\"case\":\"Tse\",\"enum\":\"B\"}" @>
    test <@ Not { body = "A" } = des "{\"case\":\"Not\",\"body\":\"A\"}" @>

let [<FsCheck.Xunit.Property>] ``auto-encodes Unions and non-unions`` (x : Any) =
    let encoded = ser x
    let decoded : Any = des encoded
    test <@ decoded = x @>
