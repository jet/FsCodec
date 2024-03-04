module FsCodec.SystemTextJson.Tests.StringIdTests

open System.Collections.Generic
open FsCodec.SystemTextJson
open Xunit
open Swensen.Unquote

module Guid =

    let inline gen () = System.Guid.NewGuid()
    let inline toStringN (x: System.Guid) = x.ToString "N"
    let inline parse (x: string) = System.Guid.Parse x

type StjConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute

module Bare =

    [<Sealed; AutoSerializable false; StjConverter(typeof<SkuIdConverter>)>]
    type SkuId(value: System.Guid) =
        // No JSON Ignore attribute required as read-only property
        member val Value = value
    and private SkuIdConverter() =
        inherit JsonIsomorphism<SkuId, string>()
        override _.Pickle(value: SkuId) = value.Value |> Guid.toStringN
        override _.UnPickle input = input |> Guid.parse |> SkuId

    [<Fact>]
    let comparison () =
        let g = Guid.gen ()
        let id1, id2 = SkuId g, SkuId g
        false =! id1.Equals id2
        id1 <>! id2

    [<Fact>]
    let serdes () =
        let x = Guid.gen () |> SkuId
        $"\"{Guid.toStringN x.Value}\"" =! Serdes.Default.Serialize x
        let ser = Serdes.Default.Serialize x
        $"\"{x.Value}\"" <>! ser // Default render of Guid is not toStringN
        x.Value =! Serdes.Default.Deserialize<SkuId>(ser).Value

        let d = Dictionary()
        d.Add(x, "value")
        raises<System.NotSupportedException> <@ Serdes.Default.Serialize d @>

module StringIdIsomorphism =

    [<Sealed; AutoSerializable false; StjConverter(typeof<SkuIdConverter>)>]
    type SkuId(value: System.Guid) = inherit FsCodec.StringId<SkuId>(Guid.toStringN value)
    and private SkuIdConverter() =
        inherit JsonIsomorphism<SkuId, string>()
        override _.Pickle(value: SkuId) = value |> string
        override _.UnPickle input = input |> Guid.parse |> SkuId

    [<Fact>]
    let comparison () =
        let g = Guid.gen()
        let id1, id2 = SkuId g, SkuId g
        true =! id1.Equals id2
        id1 =! id2

    [<Fact>]
    let serdes () =
        let x = Guid.gen () |> SkuId
        let ser = Serdes.Default.Serialize x
        $"\"{x}\"" =! ser
        x =! Serdes.Default.Deserialize ser

        let d = Dictionary()
        d.Add(x, "value")
        raises<System.NotSupportedException> <@ Serdes.Default.Serialize d @>

module StringIdConverter =

    [<Sealed; AutoSerializable false; StjConverter(typeof<SkuIdConverter>)>]
    type SkuId(value: System.Guid) = inherit FsCodec.StringId<SkuId>(Guid.toStringN value)
    and private SkuIdConverter() = inherit StringIdConverter<SkuId>(Guid.parse >> SkuId)

    [<Fact>]
    let comparison () =
        let g = Guid.gen()
        let id1, id2 = SkuId g, SkuId g
        true =! id1.Equals id2
        id1 =! id2

    [<Fact>]
    let serdes () =
        let x = Guid.gen () |> SkuId
        $"\"{x}\"" =! Serdes.Default.Serialize x

        let d = Dictionary()
        d.Add(x, "value")
        raises<System.NotSupportedException> <@ Serdes.Default.Serialize d @>

module StringIdOrKeyConverter =

    [<Sealed; AutoSerializable false; StjConverter(typeof<SkuIdConverter>)>]
    type SkuId(value: System.Guid) = inherit FsCodec.StringId<SkuId>(Guid.toStringN value)
    and private SkuIdConverter() = inherit StringIdOrDictionaryKeyConverter<SkuId>(Guid.parse >> SkuId)

    [<Fact>]
    let comparison () =
        let g = Guid.gen()
        let id1, id2 = SkuId g, SkuId g
        true =! id1.Equals id2
        id1 =! id2

    [<Fact>]
    let serdes () =
        let x = Guid.gen () |> SkuId
        $"\"{x}\"" =! Serdes.Default.Serialize x

        let d = Dictionary()
        d.Add(x, "value")
        $"{{\"{x}\":\"value\"}}" =! Serdes.Default.Serialize d
