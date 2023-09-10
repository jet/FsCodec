module FsCodec.Tests.DeflateTests

open System
open Swensen.Unquote
open Xunit

let inline roundtrip (sut : FsCodec.IEventCodec<_, _, _>) value =
    let encoded = sut.Encode((), value = value)
    let loaded = FsCodec.Core.TimelineEvent.Create(-1L, encoded)
    sut.TryDecode loaded

(* Base Fixture Round-trips a String encoded as ReadOnlyMemory<byte> UTF-8 blob *)

module StringUtf8 =

    let eventType = "n/a"
    let enc (s : string) : ReadOnlyMemory<byte> = System.Text.Encoding.UTF8.GetBytes s |> ReadOnlyMemory
    let dec (b : ReadOnlySpan<byte>) : string = System.Text.Encoding.UTF8.GetString b
    let stringUtf8Encoder =
        let encode e = struct (eventType, enc e)
        let tryDecode s (b : ReadOnlyMemory<byte>) = if s = eventType then ValueSome (dec b.Span) else invalidOp "Invalid eventType value"
        FsCodec.Codec.Create(encode, tryDecode)

    let sut = stringUtf8Encoder

    let [<Fact>] roundtrips () =
        let value = "TestValue"
        let res' = roundtrip sut value
        res' =! ValueSome value

module TryDeflate =

    let sut = FsCodec.Deflate.EncodeTryCompress(StringUtf8.sut)

    let compressibleValue = String('x', 5000)

    let [<Fact>] roundtrips () =
        let res' = roundtrip sut compressibleValue
        res' =! ValueSome compressibleValue

    let [<Fact>] ``compresses when possible`` () =
        let encoded = sut.Encode((), value = compressibleValue)
        let struct (encoding, encodedValue) = encoded.Data
        encodedValue.Length <! compressibleValue.Length

    let [<Fact>] ``uses raw value where compression not possible`` () =
        let value = "NotCompressible"
        let directResult = StringUtf8.sut.Encode((), value).Data
        let encoded = sut.Encode((), value = value)
        let struct (_encoding, result) = encoded.Data
        true =! directResult.Span.SequenceEqual(result.Span)

module Uncompressed =

    let sut = FsCodec.Deflate.EncodeUncompressed(StringUtf8.sut)

    // Borrow a demonstrably compressible value
    let value = TryDeflate.compressibleValue

    let [<Fact>] roundtrips () =
        let res' = roundtrip sut value
        res' =! ValueSome value

    let [<Fact>] ``does not compress, even if it was possible to`` () =
        let directResult = StringUtf8.sut.Encode((), value).Data
        let encoded = sut.Encode((), value)
        let struct (_encoding, result) = encoded.Data
        true =! directResult.Span.SequenceEqual(result.Span)
