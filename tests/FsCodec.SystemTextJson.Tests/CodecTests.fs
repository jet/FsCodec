module FsCodec.SystemTextJson.Tests.CodecTests

open FsCodec.SystemTextJson // bring in ToByteArrayCodec etc extension methods
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

let ignoreNullOptions = FsCodec.SystemTextJson.Options.Create(ignoreNulls = true)
let elementEncoder : TypeShape.UnionContract.IEncoder<System.Text.Json.JsonElement> =
    FsCodec.SystemTextJson.Core.JsonElementEncoder(ignoreNullOptions) :> _

let eventCodec = FsCodec.SystemTextJson.Codec.Create<Union>(ignoreNullOptions)
let doubleHopCodec = eventCodec.ToByteArrayCodec().ToJsonElementCodec()

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
    let serdes = Options.Create() |> Serdes
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
    let decoded = eventCodec.TryDecode wrapped |> Option.get

    let expected =
        match value with
        | AO ({ opt = Some null } as v) -> AO { v with opt = None }
        | BO ({ opt = Some null } as v) -> BO { v with opt = None }
        | x -> x
    test <@ expected = decoded @>

    // Also validate the adapters work when put in series (NewtonsoftJson tests are responsible for covering the individual hops)
    let decodedDoubleHop = doubleHopCodec.TryDecode wrapped |> Option.get
    test <@ expected = decodedDoubleHop @>
