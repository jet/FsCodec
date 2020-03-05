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

let defaultOptions = FsCodec.SystemTextJson.Options.Create(ignoreNulls=true)
let elementEncoder : TypeShape.UnionContract.IEncoder<System.Text.Json.JsonElement> =
    FsCodec.SystemTextJson.Core.JsonElementEncoder(defaultOptions) :> _

let eventCodec = FsCodec.SystemTextJson.Codec.Create<Union>()

[<NoComparison>]
type Envelope = { d : JsonElement }

[<Property>]
let roundtrips value =
    let eventType, embedded =
        match value with
        | A e -> "A",Choice1Of2 e
        | AO e -> "AO",Choice2Of2 e
        | B e -> "B",Choice1Of2 e
        | BO e -> "BO",Choice2Of2 e

    let encoded, ignoreSomeNull =
        match embedded with
        | Choice1Of2 e -> elementEncoder.Encode e, false
        | Choice2Of2 eo -> elementEncoder.Encode eo, eo.opt = Some null

    let enveloped = { d = encoded }
    let ser = FsCodec.SystemTextJson.Serdes.Serialize enveloped

    match embedded with
    | x when obj.ReferenceEquals(null, x) ->
        test <@ ser.StartsWith("""{"d":{""") @>
    | Choice1Of2 { embed = null }
    | Choice2Of2 { embed = null; opt = None } ->
        test <@ ser = """{"d":{}}""" @>
    | Choice2Of2 { embed = null; opt = Some null } ->
        // TOCONSIDER - should ideally treat Some null as equivalent to None
        test <@ ser.StartsWith("""{"d":{"opt":null}}""") @>
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
    test <@ value = decoded || ignoreSomeNull @>
