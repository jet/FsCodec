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

    let serdesWithOutcomeConverter = Options.Create(TypeSafeEnumConverter<Outcome>()) |> Serdes
    test <@ Joy = serdesWithOutcomeConverter.Deserialize "\"Joy\"" @>
    test <@ Some Joy = serdesWithOutcomeConverter.Deserialize "\"Joy\"" @>
    raises<KeyNotFoundException> <@ serdesWithOutcomeConverter.Deserialize<Outcome> "\"Confusion\"" @>
    // Was a JsonException prior to V6
    let serdes = Serdes.Default
    raises<NotSupportedException> <@ serdes.Deserialize<Outcome> "1" @>

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
    let serdes = Serdes.Default
    test <@ Joy = serdes.Deserialize<OutcomeWithOther> "\"Joy\"" @>
    test <@ Some Other = serdes.Deserialize<OutcomeWithOther option> "\"Wat\"" @>
    test <@ Other = serdes.Deserialize<OutcomeWithOther> "\"Wat\"" @>
    raises<JsonException> <@ serdes.Deserialize<OutcomeWithOther> "1" @>
    test <@ Seq.forall (fun (x,y) -> x = y) <| Seq.zip [Joy; Other] (serdes.Deserialize "[\"Joy\", \"Wat\"]") @>
