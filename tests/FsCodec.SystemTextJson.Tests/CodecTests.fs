module FsCodec.SystemTextJson.Tests.CodecTests

open FsCodec.SystemTextJson
open FsCodec.SystemTextJson.Interop // bring in ToUtf8Codec, ToJsonElementCodec extension methods
open System.Text.Json
open FsCheck.Xunit
open Swensen.Unquote

type Embedded = { embed : string }
type EmbeddedWithOption = { embed : string; opt : string option }
type Union =
    | A of Embedded
    | B of Embedded
    | AO of EmbeddedWithOption
    | BO of EmbeddedWithOption
    interface TypeShape.UnionContract.IUnionContract

let ignoreNullOptions = Options.Create(ignoreNulls = true)
let elementEncoder : TypeShape.UnionContract.IEncoder<JsonElement> =
    FsCodec.SystemTextJson.Core.JsonElementEncoder(ignoreNullOptions) :> _

let eventCodec = CodecJsonElement.Create<Union>(ignoreNullOptions)
let multiHopCodec = eventCodec.ToUtf8Codec().ToJsonElementCodec()

[<NoComparison>]
type Envelope = { d : JsonElement }

let [<Property>] roundtrips value =
    let eventType, embedded =
        match value with
        | A e  -> "A", Choice1Of2 e
        | AO e -> "AO",Choice2Of2 e
        | B e  -> "B", Choice1Of2 e
        | BO e -> "BO",Choice2Of2 e
    let encoded =
        match embedded with
        | Choice1Of2 e  -> elementEncoder.Encode e
        | Choice2Of2 eo -> elementEncoder.Encode eo
    let enveloped = { d = encoded }

    // the options should be irrelevant, but use the defaults (which would add nulls in that we don't want if it was leaking)
    let serdes = Serdes Options.Default
    let ser = serdes.Serialize enveloped

    match embedded with
    | Choice1Of2 { embed = null }
    | Choice2Of2 { embed = null; opt = None } ->
        test <@ ser = """{"d":{}}""" @>
    | Choice2Of2 { embed = null; opt = Some null } ->
        test <@ ser = """{"d":{"opt":null}}""" @>
    | Choice2Of2 { embed = null } ->
        test <@ ser.StartsWith("""{"d":{"opt":""") @>
    | Choice2Of2 { opt = x } ->
        test <@ ser.StartsWith """{"d":{"embed":""" && ser.Contains "opt" = Option.isSome x @>
    | Choice1Of2 _ ->
        test <@ ser.StartsWith """{"d":{"embed":""" && not (ser.Contains "\"opt\"") @>

    let des = serdes.Deserialize<Envelope> ser
    let wrapped = FsCodec.Core.TimelineEvent<JsonElement>.Create(-1L, eventType, des.d)
    test <@ wrapped.EventId = System.Guid.Empty
            && (let d = System.DateTimeOffset.UtcNow - wrapped.Timestamp
                abs d.TotalMinutes < 1) @>
    let decoded = eventCodec.TryDecode wrapped |> ValueOption.get
    let expected =
        match value with
        | AO ({ opt = Some null } as v) -> AO { v with opt = None }
        | BO ({ opt = Some null } as v) -> BO { v with opt = None }
        | x -> x
    test <@ expected = decoded @>

    // Also validate the adapters work when put in series (NewtonsoftJson tests are responsible for covering the individual hops)
    let decodedMultiHop = multiHopCodec.TryDecode wrapped |> ValueOption.get
    test <@ expected = decodedMultiHop @>

let [<Xunit.Fact>] ``EventData.Create basics`` () =
    let e = FsCodec.Core.EventData.Create("et", "data")

    test <@ e.EventId <> System.Guid.Empty
            && e.EventType = "et"
            && e.Data = "data"
            && (let d = System.DateTimeOffset.UtcNow - e.Timestamp
                abs d.TotalMinutes < 1) @>

let [<Xunit.Fact>] ``TimelineEvent.Create basics`` () =
    let e = FsCodec.Core.TimelineEvent.Create(42, "et", "data")

    test <@ e.EventId = System.Guid.Empty
            && not e.IsUnfold
            && e.EventType = "et"
            && e.Data = "data"
            && (let d = System.DateTimeOffset.UtcNow - e.Timestamp
                abs d.TotalMinutes < 1) @>
