module FsCodec.NewtonsoftJson.Tests.VerbatimUtf8ConverterTests

open FsCheck.Xunit
open FsCodec.NewtonsoftJson
open Newtonsoft.Json
open System
open Swensen.Unquote
open global.Xunit

type Embedded = { embed : string }
type Union =
    | A of Embedded
    | B of Embedded
    interface TypeShape.UnionContract.IUnionContract

type EmbeddedString = { embed : string }
type EmbeddedDate = { embed : DateTime }
type EmbeddedDateTimeOffset = { embed : DateTimeOffset }
type U =
    | R of Embedded
    //| ED of EmbeddedDate // Not recommended; gets mangled by timezone adjustments
    //| S of string // Too messy/confusing to support
    //| DTO of DateTimeOffset // Have not delved into what the exact problem is; no point implementing if strings cant work
    //| DT of DateTime // Have not analyzed but seems to be same issue as DTO
    | EDto of EmbeddedDateTimeOffset
    | ES of EmbeddedString
    //| I of int // works but removed as no other useful top level values work
    | N
    interface TypeShape.UnionContract.IUnionContract

type [<NoEquality; NoComparison; JsonObject(ItemRequired=Required.Always)>]
    Event =
    {   t: DateTimeOffset // ISO 8601
        c: string // required
        [<JsonConverter(typeof<VerbatimUtf8JsonConverter>)>]
        [<JsonProperty(Required=Required.AllowNull)>]
        d: byte[] // Required, but can be null so Nullary cases can work

        [<JsonConverter(typeof<VerbatimUtf8JsonConverter>)>]
        [<JsonProperty(Required=Required.Default, NullValueHandling=NullValueHandling.Ignore)>]
        m: byte[] } // optional
type [<NoEquality; NoComparison; JsonObject(ItemRequired=Required.Always)>]
    Batch =
    {   [<JsonProperty(Required=Required.Default)>] // Not requested in queries
        p: string
        id: string
        [<JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore, Required=Required.Default)>]
        _etag: string
        i: int64
        n: int64
        e: Event[] }
let mkBatch (encoded : FsCodec.IEventData<byte[]>) : Batch =
    {   p = "streamName"; id = string 0; i = -1L; n = -1L; _etag = null
        e = [| { t = DateTimeOffset.MinValue; c = encoded.EventType; d = encoded.Data; m = null } |] }

#nowarn "1182" // From hereon in, we may have some 'unused' privates (the tests)

module VerbatimUtf8Tests = // not a module or CI will fail for net461

    let eventCodec = Codec.Create<Union>()

    let [<Fact>] ``encodes correctly`` () =
        let input = Union.A { embed = "\"" }
        let encoded = eventCodec.Encode(None, input)
        let e : Batch = mkBatch encoded
        let res = JsonConvert.SerializeObject(e)
        test <@ res.Contains """"d":{"embed":"\""}""" @>
        let des = JsonConvert.DeserializeObject<Batch>(res)
        let loaded = FsCodec.Core.TimelineEvent.Create(-1L, des.e.[0].c, des.e.[0].d)
        let decoded = eventCodec.TryDecode loaded |> Option.get
        input =! decoded

    let defaultSettings = Options.CreateDefault()
    let defaultEventCodec = Codec.Create<U>(defaultSettings)

    let [<Property>] ``round-trips diverse bodies correctly`` (x: U) =
        let encoded = defaultEventCodec.Encode(None,x)
        let e : Batch = mkBatch encoded
        let ser = JsonConvert.SerializeObject(e, defaultSettings)
        let des = JsonConvert.DeserializeObject<Batch>(ser, defaultSettings)
        let loaded = FsCodec.Core.TimelineEvent.Create(-1L, des.e.[0].c, des.e.[0].d)
        let decoded = defaultEventCodec.TryDecode loaded |> Option.get
        x =! decoded

    // https://github.com/JamesNK/Newtonsoft.Json/issues/862 // doesnt apply to this case
    let [<Fact>] ``Codec does not fall prey to Date-strings being mutilated`` () =
        let x = ES { embed = "2016-03-31T07:02:00+07:00" }
        let encoded = defaultEventCodec.Encode(None,x)
        let adapted = FsCodec.Core.TimelineEvent.Create(-1L, encoded.EventType, encoded.Data, encoded.Meta, timestamp = encoded.Timestamp)
        let decoded = defaultEventCodec.TryDecode adapted |> Option.get
        test <@ x = decoded @>

    //// NB while this aspect works, we don't support it as it gets messy when you then use the VerbatimUtf8Converter
    //let sEncoder = Codec.Create<US>(defaultSettings)
    //let [<Theory; InlineData ""; InlineData null>] ``Codec can roundtrip strings`` (value: string)  =
    //    let x = SS value
    //    let encoded = sEncoder.Encode x
    //    let decoded = sEncoder.TryDecode encoded |> Option.get
    //    test <@ x = decoded @>

module VerbatimUtf8NullHandling =

    type [<NoEquality; NoComparison>] EventHolderWithAndWithoutRequired =
        {   /// Event body, as UTF-8 encoded JSON ready to be injected directly into the Json being rendered
            [<JsonConverter(typeof<VerbatimUtf8JsonConverter>)>]
            d: byte[] // required

            /// Optional metadata, as UTF-8 encoded JSON, ready to emit directly (entire field is not written if value is null)
            [<JsonConverter(typeof<VerbatimUtf8JsonConverter>)>]
            [<JsonProperty(Required=Required.Default, NullValueHandling=NullValueHandling.Ignore)>]
            m: byte[] }

    let values : obj[][] =
        [|  [| null |]
            [| [||] |]
            [| System.Text.Encoding.UTF8.GetBytes "{}" |] |]

    [<Theory; MemberData "values">]
    let ``round-trips nulls and empties consistently`` value =
        let e : EventHolderWithAndWithoutRequired = { d = value; m = value }
        let ser = JsonConvert.SerializeObject(e)
        let des = JsonConvert.DeserializeObject<EventHolderWithAndWithoutRequired>(ser)
        test <@ ((e.m = null || e.m.Length = 0) && (des.m = null)) || System.Linq.Enumerable.SequenceEqual(e.m, des.m) @>
        test <@ ((e.d = null || e.d.Length = 0) && (des.d = null)) || System.Linq.Enumerable.SequenceEqual(e.d, des.d) @>
