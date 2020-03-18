module FsCodec.SystemTextJson.Tests.CodecTests

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
    let ser = FsCodec.SystemTextJson.Serdes.Serialize enveloped

    match embedded with
    | x when obj.ReferenceEquals(null, x) ->
        test <@ ser.StartsWith("""{"d":{""") @>
    | Choice1Of2 { embed = null }
    | Choice2Of2 { embed = null; opt = None }
    | Choice2Of2 { embed = null; opt = Some null } ->
        test <@ ser = """{"d":{}}""" @>
    | Choice2Of2 { embed = null } ->
        test <@ ser.StartsWith("""{"d":{"opt":""") @>
    | _ ->
        test <@ ser.StartsWith("""{"d":{"embed":""") @>

    match embedded with
    | Choice2Of2 { opt = None } -> test <@ not (ser.Contains "opt") @>
    | _ -> ()

    let des = FsCodec.SystemTextJson.Serdes.Deserialize<Envelope> ser
    let wrapped = FsCodec.Core.TimelineEvent<JsonElement>.Create(-1L, eventType, des.d)
    let decoded = eventCodec.TryDecode wrapped |> Option.get
    test <@ value = decoded @>
