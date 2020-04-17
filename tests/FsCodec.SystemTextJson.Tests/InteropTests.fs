/// Covers interop with stores that manage event bodies as byte[]
module FsCodec.SystemTextJson.Tests.InteropTests

open FsCheck.Xunit
open Newtonsoft.Json
open System
open Swensen.Unquote
open Xunit

type Batch = FsCodec.NewtonsoftJson.Tests.VerbatimUtf8ConverterTests.Batch
type Union = FsCodec.NewtonsoftJson.Tests.VerbatimUtf8ConverterTests.Union
let mkBatch = FsCodec.NewtonsoftJson.Tests.VerbatimUtf8ConverterTests.mkBatch

let indirectCodec = FsCodec.SystemTextJson.Codec.Create() |> FsCodec.SystemTextJson.InteropExtensions.ToByteArrayCodec
let [<Fact>] ``encodes correctly`` () =
    let input = Union.A { embed = "\"" }
    let encoded = indirectCodec.Encode(None, input)
    let e : Batch = mkBatch encoded
    let res = JsonConvert.SerializeObject(e)
    test <@ res.Contains """"d":{"embed":"\""}""" @>
    let des = JsonConvert.DeserializeObject<Batch>(res)
    let loaded = FsCodec.Core.TimelineEvent.Create(-1L, des.e.[0].c, des.e.[0].d)
    let decoded = indirectCodec.TryDecode loaded |> Option.get
    input =! decoded

type EmbeddedString = { embed : string }
type EmbeddedDateTimeOffset = { embed : DateTimeOffset }
type U =
    // | S of string // Opens up some edge cases wrt handling missing/empty/null `d` fields in stores, but possible if you have time to shave that yak!
    | EDto of EmbeddedDateTimeOffset
    | ES of EmbeddedString
    | N
    interface TypeShape.UnionContract.IUnionContract

let defaultSettings = FsCodec.NewtonsoftJson.Settings.CreateDefault() // Test without converters, as that's what Equinox.Cosmos will do
let defaultEventCodec = FsCodec.NewtonsoftJson.Codec.Create<U>(defaultSettings)
let indirectCodecU = FsCodec.SystemTextJson.Codec.Create<U>() |> FsCodec.SystemTextJson.InteropExtensions.ToByteArrayCodec

let [<Property>] ``round-trips diverse bodies correctly`` (x: U, encodeDirect, decodeDirect) =
    let encoder = if encodeDirect then defaultEventCodec else indirectCodecU
    let decoder = if decodeDirect then defaultEventCodec else indirectCodecU
    let encoded = encoder.Encode(None,x)
    let e : Batch = mkBatch encoded
    let ser = JsonConvert.SerializeObject(e, defaultSettings)
    let des = JsonConvert.DeserializeObject<Batch>(ser, defaultSettings)
    let loaded = FsCodec.Core.TimelineEvent.Create(-1L, des.e.[0].c, des.e.[0].d)
    let decoded = decoder.TryDecode loaded |> Option.get
    x =! decoded
