module FsCodec.SystemTextJson.Tests.TypeSafeEnumConverterTests

open FsCodec.SystemTextJson
open System
open System.Collections.Generic
open System.Text.Json
open Swensen.Unquote
open Xunit

type Outcome = Joy | Pain | Misery

let [<Fact>] happy () =
    test <@ box Joy = TypeSafeEnum.parseT (typeof<Outcome>) "Joy" @>
    test <@ Joy = TypeSafeEnum.parse "Joy" @>
    test <@ box Joy = TypeSafeEnum.parseT (typeof<Outcome>) "Joy"  @>
    test <@ None = TypeSafeEnum.tryParse<Outcome> "Wat" @>
    raises<KeyNotFoundException> <@ TypeSafeEnum.parse<Outcome> "Wat" @>

    let optionsWithOutcomeConverter = Options.Create(TypeSafeEnumConverter<Outcome>())
    test <@ Joy = Serdes.Deserialize("\"Joy\"", optionsWithOutcomeConverter) @>
    test <@ Some Joy = Serdes.Deserialize("\"Joy\"", optionsWithOutcomeConverter) @>
    raises<KeyNotFoundException> <@ Serdes.Deserialize<Outcome>("\"Confusion\"", optionsWithOutcomeConverter) @>
    // Was a JsonException prior to V6
    raises<NotSupportedException> <@ Serdes.Deserialize<Outcome> "1" @>

let [<Fact>] sad () =
    raises<ArgumentException> <@ TypeSafeEnum.tryParse<string> "Wat" @>
    raises<ArgumentException> <@ TypeSafeEnum.toString "Wat" @>

[<System.Text.Json.Serialization.JsonConverter(typeof<OutcomeWithCatchAllConverter>)>]
type OutcomeWithOther = Joy | Pain | Misery | Other
and OutcomeWithCatchAllConverter() =
    inherit JsonIsomorphism<OutcomeWithOther, string>()
    override _.Pickle v =
        TypeSafeEnum.toString v

    override _.UnPickle json =
        json
        |> TypeSafeEnum.tryParse<OutcomeWithOther>
        |> Option.defaultValue Other

let [<Fact>] fallBackExample () =
    test <@ Joy = Serdes.Deserialize<OutcomeWithOther> "\"Joy\"" @>
    test <@ Some Other = Serdes.Deserialize<OutcomeWithOther option> "\"Wat\"" @>
    test <@ Other = Serdes.Deserialize<OutcomeWithOther> "\"Wat\"" @>
    raises<JsonException> <@ Serdes.Deserialize<OutcomeWithOther> "1" @>
    test <@ Seq.forall (fun (x,y) -> x = y) <| Seq.zip [Joy; Other] (Serdes.Deserialize "[\"Joy\", \"Wat\"]") @>
