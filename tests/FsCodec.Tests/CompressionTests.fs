module FsCodec.Tests.CompressionTests

open System
open Swensen.Unquote
open Xunit

let inline roundtrip (sut : FsCodec.IEventCodec<_, _, _>) value =
    let encoded = sut.Encode((), value = value)
    let loaded = FsCodec.Core.TimelineEvent.Create(-1L, encoded)
    sut.Decode loaded

(* Base Fixture Round-trips a String encoded as ReadOnlyMemory<byte> UTF-8 blob *)

module StringUtf8 =

    let eventType = "n/a"
    let enc (s : string) : ReadOnlyMemory<byte> = System.Text.Encoding.UTF8.GetBytes s |> ReadOnlyMemory
    let dec (b : ReadOnlySpan<byte>) : string = System.Text.Encoding.UTF8.GetString b
    let stringUtf8Encoder =
        let encode e = struct (eventType, enc e)
        let decode s (b : ReadOnlyMemory<byte>) = if s = eventType then ValueSome (dec b.Span) else invalidOp "Invalid eventType value"
        FsCodec.Codec.Create(encode, decode)

    let sut = stringUtf8Encoder

    let [<Fact>] roundtrips () =
        let value = "TestValue"
        let res' = roundtrip sut value
        res' =! ValueSome value

module TryCompress =

    let sut = FsCodec.Compression.EncodeTryCompress(StringUtf8.sut)

    let compressibleValue = String('x', 5000)

    let [<Fact>] roundtrips () =
        let res' = roundtrip sut compressibleValue
        res' =! ValueSome compressibleValue

    let [<Fact>] ``compresses when possible`` () =
        let encoded = sut.Encode((), value = compressibleValue)
        let struct (_encoding, encodedValue) = encoded.Data
        encodedValue.Length <! compressibleValue.Length

    let [<Fact>] ``uses raw value where compression not possible`` () =
        let value = "NotCompressible"
        let directResult = StringUtf8.sut.Encode((), value).Data
        let encoded = sut.Encode((), value = value)
        let struct (_encoding, result) = encoded.Data
        true =! directResult.Span.SequenceEqual(result.Span)

module Uncompressed =

    let sut = FsCodec.Compression.EncodeUncompressed(StringUtf8.sut)

    // Borrow a demonstrably compressible value
    let value = TryCompress.compressibleValue

    let [<Fact>] roundtrips () =
        let res' = roundtrip sut value
        res' =! ValueSome value

    let [<Fact>] ``does not compress, even if it was possible to`` () =
        let directResult = StringUtf8.sut.Encode((), value).Data
        let encoded = sut.Encode((), value)
        let struct (_encoding, result) = encoded.Data
        true =! directResult.Span.SequenceEqual(result.Span)

module Decoding =

    let raw = struct(0, Text.Encoding.UTF8.GetBytes("Hello World") |> ReadOnlyMemory)
    let deflated = struct(1, Convert.FromBase64String("8kjNyclXCM8vykkBAAAA//8=") |> ReadOnlyMemory)
    let brotli = struct(2, Convert.FromBase64String("CwWASGVsbG8gV29ybGQ=") |> ReadOnlyMemory)

    let [<Fact>] ``Can decode all known bodies`` () =
        let decode = FsCodec.Compression.EncodedToByteArray >> Text.Encoding.UTF8.GetString
        test <@ decode raw = "Hello World"  @>
        test <@ decode deflated = "Hello World"  @>
        test <@ decode brotli = "Hello World"  @>

    let [<Fact>] ``Defaults to leaving the memory alone if unknown`` () =
        let struct(_, mem) = raw
        let body = struct(99, mem)
        let decoded = body |> FsCodec.Compression.EncodedToByteArray |> Text.Encoding.UTF8.GetString
        test <@ decoded = "Hello World" @>
