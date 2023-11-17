/// Covers adapting of Codecs that map to JsonElement to instead map to ReadOnlyMemory, and interop with the VerbatimUtf8Converter
module FsCodec.SystemTextJson.Tests.InteropTests

open FsCheck.Xunit
open Newtonsoft.Json
open Swensen.Unquote
open System
open Xunit

type Batch = FsCodec.NewtonsoftJson.Tests.VerbatimUtf8ConverterTests.Batch
type Union = FsCodec.NewtonsoftJson.Tests.VerbatimUtf8ConverterTests.Union
let mkBatch = FsCodec.NewtonsoftJson.Tests.VerbatimUtf8ConverterTests.mkBatch

let indirectCodec = FsCodec.SystemTextJson.CodecJsonElement.Create() |> FsCodec.SystemTextJson.Interop.InteropHelpers.ToUtf8Codec
let [<Fact>] ``encodes correctly`` () =
    let input = Union.A { embed = "\"" }
    let encoded = indirectCodec.Encode((), input)
    let e : Batch = mkBatch encoded
    let res = JsonConvert.SerializeObject(e)
    test <@ res.Contains """"d":{"embed":"\""}""" @>
    let des = JsonConvert.DeserializeObject<Batch>(res)
    let loaded = FsCodec.Core.TimelineEvent.Create(-1L, des.e[0].c, ReadOnlyMemory des.e[0].d)
    let decoded = indirectCodec.Decode loaded |> ValueOption.get
    input =! decoded

type EmbeddedString = { embed : string }
type EmbeddedDateTimeOffset = { embed : DateTimeOffset }
type U =
    // | S of string // Opens up some edge cases wrt handling missing/empty/null `d` fields in stores, but possible if you have time to shave that yak!
    | EDto of EmbeddedDateTimeOffset
    | ES of EmbeddedString
    | N
    interface TypeShape.UnionContract.IUnionContract

let defaultSettings = FsCodec.NewtonsoftJson.Options.CreateDefault() // Test without converters, as that's what Equinox.Cosmos will do
let defaultEventCodec = FsCodec.NewtonsoftJson.Codec.Create<U>(defaultSettings)
let indirectCodecU = FsCodec.SystemTextJson.CodecJsonElement.Create<U>() |> FsCodec.SystemTextJson.Interop.InteropHelpers.ToUtf8Codec

let [<Property>] ``round-trips diverse bodies correctly`` (x: U, encodeDirect, decodeDirect) =
    let encoder = if encodeDirect then defaultEventCodec else indirectCodecU
    let decoder = if decodeDirect then defaultEventCodec else indirectCodecU
    let encoded = encoder.Encode((), x)
    let e : Batch = mkBatch encoded
    let ser = JsonConvert.SerializeObject(e, defaultSettings)
    let des = JsonConvert.DeserializeObject<Batch>(ser, defaultSettings)
    let loaded = FsCodec.Core.TimelineEvent.Create(-1L, des.e[0].c, ReadOnlyMemory des.e[0].d)
    let decoded = decoder.Decode loaded |> ValueOption.get
    x =! decoded
