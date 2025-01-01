module FsCodec.SystemTextJson.Tests.EncodedBodyTests

open Swensen.Unquote
open System
open System.Text.Json
open Xunit

let inline roundtrip (sut: FsCodec.IEventCodec<'event, 'F, unit>) value =
    let encoded = sut.Encode((), value = value)
    let loaded = FsCodec.Core.TimelineEvent.Create(-1L, encoded)
    sut.Decode loaded

(* Base Fixture Round-trips a String encoded as JsonElement *)

module StringUtf8 =

    let eventType = "EventType"
    let enc (x: 't): JsonElement = JsonSerializer.SerializeToElement x
    let dec (b: JsonElement): 't = JsonSerializer.Deserialize b
    let jsonElementCodec<'t> =
        let encode e = struct (eventType, enc e)
        let decode s (b: JsonElement) = if s = eventType then ValueSome (dec b) else invalidOp "Invalid eventType value"
        FsCodec.Codec.Create(encode, decode)

    let sut<'t> = jsonElementCodec<'t>

    let [<Fact>] roundtrips () =
        let value = {| value = "Hello World" |}
        let res' = roundtrip sut value
        res' =! ValueSome value

module InternalDecoding =

    let inputValue = {| value = "Hello World" |}
    // A JsonElement that's a JSON Object should be handled as an uncompressed value
    let direct = struct (0, JsonSerializer.SerializeToElement inputValue)
    let explicitDeflate = struct (1, JsonSerializer.SerializeToElement "qlYqS8wpTVWyUvJIzcnJVwjPL8pJUaoFAAAA//8=")
    let explicitBrotli = struct (2, JsonSerializer.SerializeToElement "CwuAeyJ2YWx1ZSI6IkhlbGxvIFdvcmxkIn0D")

    let decode useRom =
        if useRom then FsCodec.SystemTextJson.EncodedBody.ToByteArray >> JsonSerializer.Deserialize
        else FsCodec.SystemTextJson.EncodedBody.ToJsonElement >> JsonSerializer.Deserialize

    let [<Theory; InlineData false; InlineData true>] ``Can decode all known representations`` useRom =
        test <@ decode useRom direct = inputValue @>
        test <@ decode useRom explicitDeflate = inputValue @>
        test <@ decode useRom explicitBrotli = inputValue @>

    let [<Theory; InlineData false; InlineData true>] ``Defaults to leaving the body alone if unknown`` useRom =
        let struct (_, je) = direct
        let body = struct (99, je)
        let decoded = decode useRom body
        test <@ decoded = inputValue @>

    let [<Theory; InlineData false; InlineData true>] ``Defaults to leaving the body alone if string`` useRom =
        let body = struct (99, JsonSerializer.SerializeToElement "test")
        let decoded = decode useRom body
        test <@ "test" = decoded @>

type JsonElement with member x.Utf8ByteCount = if x.ValueKind = JsonValueKind.Null then 0 else x.GetRawText() |> System.Text.Encoding.UTF8.GetByteCount

module TryCompress =

    let sut = FsCodec.SystemTextJson.EncodedBody.EncodeTryCompress StringUtf8.sut

    let compressibleValue = {| value = String('x', 5000) |}

    let [<Fact>] roundtrips () =
        let res' = roundtrip sut compressibleValue
        res' =! ValueSome compressibleValue

    let [<Fact>] ``compresses when possible`` () =
        let encoded = sut.Encode((), value = compressibleValue)
        let struct (_encoding, encodedValue) = encoded.Data
        encodedValue.Utf8ByteCount <! JsonSerializer.Serialize(compressibleValue).Length

    let [<Fact>] ``produces equivalent JsonElement where compression not possible`` () =
        let value = {| value = "NotCompressible" |}
        let directResult = StringUtf8.sut.Encode((), value).Data
        let failedToCompressResult = sut.Encode((), value = value)
        let struct (_encoding, result) = failedToCompressResult.Data
        true =! JsonElement.DeepEquals(directResult, result)

module Uncompressed =

    let sut = FsCodec.SystemTextJson.EncodedBody.EncodeUncompressed StringUtf8.sut

    // Borrow the value we just demonstrated to be compressible
    let compressibleValue = TryCompress.compressibleValue

    let [<Fact>] roundtrips () =
        let res' = roundtrip sut compressibleValue
        res' =! ValueSome compressibleValue

    let [<Fact>] ``does not compress (despite it being possible to)`` () =
        let directResult = StringUtf8.sut.Encode((), compressibleValue).Data
        let shouldNotBeCompressedResult = sut.Encode((), value = compressibleValue)
        let struct (_encoding, result) = shouldNotBeCompressedResult.Data
        result.Utf8ByteCount =! directResult.Utf8ByteCount
