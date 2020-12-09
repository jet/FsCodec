module FsCodec.SystemTextJson.Tests.UnionConverterTests

open FsCheck
open FsCodec.SystemTextJson
open Swensen.Unquote.Assertions
open System
open System.Text.Json
open System.Text.Json.Serialization
open global.Xunit
open FsCodec.SystemTextJson.Tests.Fixtures

type TestRecordPayload =
    {
        test: string
    }

type TrickyRecordPayload =
    {
        Item: string
    }

[<JsonConverter(typeof<TypeSafeEnumConverter<Mode>>)>]
type Mode =
    | Fast
    | Slow

[<NoComparison>] // NB this is not a general restriction; it's forced by use of Nullable<T> in some of the cases in this specific one
[<JsonConverter(typeof<UnionConverter<TestDU>>)>]
type TestDU =
    | CaseA of TestRecordPayload
    | CaseB
    | CaseC of string
    | CaseD of a: string
    | CaseE of string * int
    | CaseF of a: string * b: int
    | CaseG of TrickyRecordPayload
    | CaseH of a: TestRecordPayload
    | CaseI of a: TestRecordPayload * b: string
    | CaseJ of a: Nullable<int>
    | CaseK of a: int * b: Nullable<int>
    | CaseL of a: Nullable<int> * b: Nullable<int>
    | CaseM of a: int option
    | CaseN of a: int * b: int option
    | CaseO of a: int option * b: int option
    | CaseP of CartId
    | CaseQ of SkuId
    | CaseR of a: CartId
    | CaseS of a: SkuId
    | CaseT of a: SkuId option * b: CartId
    | CaseU of SkuId[]
    | CaseV of skus: SkuId[]
    | CaseW of CartId * SkuId[]
    | CaseX of a: CartId * skus: SkuId[]
    | CaseY of a: Mode * b: Mode
    | CaseZ of a: Mode * b: Mode option

#nowarn "1182" // From hereon in, we may have some 'unused' privates (the tests)

// no camel case, because I want to test "Item" as a record property
// Centred on ignoreNulls for backcompat; round-tripping test covers the case where they get rendered too
let defaultOptions = Options.Create(camelCase = false, ignoreNulls = true)
let inline serialize x = JsonSerializer.Serialize(x, defaultOptions)

[<Fact>]
let ``produces expected output`` () =
    let a = CaseA {test = "hi"}
    test <@ """{"case":"CaseA","test":"hi"}""" = serialize a @>

    let b = CaseB
    test <@ """{"case":"CaseB"}""" = serialize b @>

    let c = CaseC "hi"
    test <@ """{"case":"CaseC","Item":"hi"}""" = serialize c @>

    let d = CaseD "hi"
    test <@ """{"case":"CaseD","a":"hi"}""" = serialize d @>

    let e = CaseE ("hi", 0)
    test <@ """{"case":"CaseE","Item1":"hi","Item2":0}""" = serialize e @>

    let f = CaseF ("hi", 0)
    test <@ """{"case":"CaseF","a":"hi","b":0}""" = serialize f @>

    let g = CaseG {Item = "hi"}
    test <@ """{"case":"CaseG","Item":"hi"}""" = serialize g @>

    // this may not be expected, but I don't intend changing it
    let h = CaseH {test = "hi"}
    test <@ """{"case":"CaseH","test":"hi"}""" = serialize h @>

    let i = CaseI ({test = "hi"}, "bye")
    test <@ """{"case":"CaseI","a":{"test":"hi"},"b":"bye"}""" = serialize i @>

    let p = CaseP (CartId.Parse "0000000000000000948d503fcfc20f17")
    test <@ """{"case":"CaseP","Item":"0000000000000000948d503fcfc20f17"}""" = serialize p @>

    let u = CaseU [| SkuId.Parse "f09f17cb4c9744b4a979afb53be0847f"; SkuId.Parse "c747d53a644d42548b3bbc0988561ce1" |]
    test<@ """{"case":"CaseU","Item":["f09f17cb4c9744b4a979afb53be0847f","c747d53a644d42548b3bbc0988561ce1"]}""" = serialize u @>

[<Fact>]
let ``deserializes properly`` () =
    let deserialize (json : string) = JsonSerializer.Deserialize<TestDU>(json, defaultOptions)
    test <@ CaseA {test = null} = deserialize """{"case":"CaseA"}""" @>
    test <@ CaseA {test = "hi"} = deserialize """{"case":"CaseA","test":"hi"}""" @>
    test <@ CaseA {test = "hi"} = deserialize """{"case":"CaseA","test":"hi","extraField":"hello"}""" @>

    test <@ CaseB = deserialize """{"case":"CaseB"}""" @>

    test <@ CaseC "hi" = deserialize """{"case":"CaseC","Item":"hi"}""" @>

    test <@ CaseD "hi" = deserialize """{"case":"CaseD","a":"hi"}""" @>

    test <@ CaseE ("hi", 0) = deserialize """{"case":"CaseE","Item1":"hi","Item2":0}""" @>

    test <@ CaseE (null, 0) = deserialize """{"case":"CaseE","Item3":"hi","Item4":0}""" @>

    test <@ CaseF ("hi", 0) = deserialize """{"case":"CaseF","a":"hi","b":0}""" @>

    test <@ CaseG {Item = "hi"} = deserialize """{"case":"CaseG","Item":"hi"}""" @>

    test <@ CaseH {test = "hi"} = deserialize """{"case":"CaseH","test":"hi"}""" @>

    test <@ CaseI ({test = "hi"}, "bye") = deserialize """{"case":"CaseI","a":{"test":"hi"},"b":"bye"}""" @>

    test <@ CaseJ (Nullable 1) = deserialize """{"case":"CaseJ","a":1}""" @>
    test <@ CaseK (1, Nullable 2) = deserialize """{"case":"CaseK", "a":1, "b":2 }""" @>
    test <@ CaseL (Nullable 1, Nullable 2) = deserialize """{"case":"CaseL", "a": 1, "b": 2 }""" @>

    // TOINVESTIGATE: It looks like Newtonsoft Settings.Create() behaviour is to always add
    // an OptionConverter? This might not be needed anymore?
    //let requiredSettingsToHandleOptionalFields = Settings.Create()
    let deserializeCustom s = deserialize s // JsonConvert.DeserializeObject<TestDU>(s, requiredSettingsToHandleOptionalFields)
    test <@ CaseM (Some 1) = deserializeCustom """{"case":"CaseM","a":1}""" @>
    test <@ CaseN (1, Some 2) = deserializeCustom """{"case":"CaseN", "a":1, "b":2 }""" @>
    test <@ CaseO (Some 1, Some 2) = deserializeCustom """{"case":"CaseO", "a": 1, "b": 2 }""" @>

    test <@ CaseP (CartId.Parse "0000000000000000948d503fcfc20f17") = deserialize """{"case":"CaseP","Item":"0000000000000000948d503fcfc20f17"}""" @>

    test<@ CaseU [| SkuId.Parse "f09f17cb4c9744b4a979afb53be0847f"; SkuId.Parse "c747d53a644d42548b3bbc0988561ce1" |] =
    deserialize """{"case":"CaseU","Item":["f09f17cb4c9744b4a979afb53be0847f","c747d53a644d42548b3bbc0988561ce1"]}"""@>

let (|Q|) (s: string) = JsonSerializer.Serialize(s, defaultOptions)

// Renderings when NullValueHandling=Include, which is used by the recommended Settings.Create profile
let render ignoreNulls = function
    | CaseA { test = null } when ignoreNulls -> """{"case":"CaseA"}"""
    | CaseA { test = Q x} -> sprintf """{"case":"CaseA","test":%s}""" x
    | CaseB -> """{"case":"CaseB"}"""
    | CaseC null when ignoreNulls -> """{"case":"CaseC"}"""
    | CaseC (Q s) -> sprintf """{"case":"CaseC","Item":%s}""" s
    | CaseD null when ignoreNulls -> """{"case":"CaseD"}"""
    | CaseD (Q s) -> sprintf """{"case":"CaseD","a":%s}""" s
    | CaseE (null,y) when ignoreNulls -> sprintf """{"case":"CaseE","Item2":%d}""" y
    | CaseE (null,y) -> sprintf """{"case":"CaseE","Item1":null,"Item2":%d}""" y
    | CaseE (Q x,y) -> sprintf """{"case":"CaseE","Item1":%s,"Item2":%d}""" x y
    | CaseF (null,y) when ignoreNulls -> sprintf """{"case":"CaseF","b":%d}""" y
    | CaseF (null,y) -> sprintf """{"case":"CaseF","a":null,"b":%d}""" y
    | CaseF (Q x,y) -> sprintf """{"case":"CaseF","a":%s,"b":%d}""" x y
    | CaseG {Item = null} when ignoreNulls  -> """{"case":"CaseG"}"""
    | CaseG {Item = Q s} -> sprintf """{"case":"CaseG","Item":%s}""" s
    | CaseH {test = null} when ignoreNulls -> """{"case":"CaseH"}"""
    | CaseH {test = Q s} -> sprintf """{"case":"CaseH","test":%s}""" s
    | CaseI ({test = null}, null) when ignoreNulls -> """{"case":"CaseI","a":{}}"""
    | CaseI ({test = null}, null) -> """{"case":"CaseI","a":{"test":null},"b":null}"""
    | CaseI ({test = null}, Q s) when ignoreNulls -> sprintf """{"case":"CaseI","a":{},"b":%s}""" s
    | CaseI ({test = null}, Q s) -> sprintf """{"case":"CaseI","a":{"test":null},"b":%s}""" s
    | CaseI ({test = Q s}, null) when ignoreNulls -> sprintf """{"case":"CaseI","a":{"test":%s}}""" s
    | CaseI ({test = Q s}, null) -> sprintf """{"case":"CaseI","a":{"test":%s},"b":null}""" s
    | CaseI ({test = Q s}, Q b) -> sprintf """{"case":"CaseI","a":{"test":%s},"b":%s}""" s b

    | CaseJ x when not x.HasValue && ignoreNulls -> """{"case":"CaseJ"}"""
    | CaseJ x when not x.HasValue -> """{"case":"CaseJ","a":null}"""
    | CaseJ x -> sprintf """{"case":"CaseJ","a":%d}""" x.Value
    | CaseK (a,x) when not x.HasValue && ignoreNulls -> sprintf """{"case":"CaseK","a":%d}""" a
    | CaseK (a,x) when not x.HasValue -> sprintf """{"case":"CaseK","a":%d,"b":null}""" a
    | CaseK (a,x) -> sprintf """{"case":"CaseK","a":%d,"b":%d}""" a x.Value
    | CaseL (a,b) when not a.HasValue && not b.HasValue && ignoreNulls -> """{"case":"CaseL"}"""
    | CaseL (a,b) when not a.HasValue && not b.HasValue -> """{"case":"CaseL","a":null,"b":null}"""
    | CaseL (a,b) when not a.HasValue && ignoreNulls -> sprintf """{"case":"CaseL","b":%d}""" b.Value
    | CaseL (a,b) when not a.HasValue -> sprintf """{"case":"CaseL","a":null,"b":%d}""" b.Value
    | CaseL (a,b) when not b.HasValue && ignoreNulls -> sprintf """{"case":"CaseL","a":%d}""" a.Value
    | CaseL (a,b) when not b.HasValue -> sprintf """{"case":"CaseL","a":%d,"b":null}""" a.Value
    | CaseL (a,b) -> sprintf """{"case":"CaseL","a":%d,"b":%d}""" a.Value b.Value

    | CaseM None when ignoreNulls -> """{"case":"CaseM"}"""
    | CaseM None -> """{"case":"CaseM","a":null}"""
    | CaseM (Some x) -> sprintf """{"case":"CaseM","a":%d}""" x
    | CaseN (a,None) when ignoreNulls -> sprintf """{"case":"CaseN","a":%d}""" a
    | CaseN (a,None) -> sprintf """{"case":"CaseN","a":%d,"b":null}""" a
    | CaseN (a,x) -> sprintf """{"case":"CaseN","a":%d,"b":%d}""" a x.Value
    | CaseO (None,None) when ignoreNulls -> """{"case":"CaseO"}"""
    | CaseO (None,None) -> """{"case":"CaseO","a":null,"b":null}"""
    | CaseO (None,b) when ignoreNulls -> sprintf """{"case":"CaseO","b":%d}""" b.Value
    | CaseO (None,b) -> sprintf """{"case":"CaseO","a":null,"b":%d}""" b.Value
    | CaseO (a,None) when ignoreNulls -> sprintf """{"case":"CaseO","a":%d}""" a.Value
    | CaseO (a,None) -> sprintf """{"case":"CaseO","a":%d,"b":null}""" a.Value
    | CaseO (Some a,Some b) -> sprintf """{"case":"CaseO","a":%d,"b":%d}""" a b
    | CaseP id -> sprintf """{"case":"CaseP","Item":"%s"}""" id.Value
    | CaseQ id -> sprintf """{"case":"CaseQ","Item":"%s"}""" id.Value
    | CaseR id -> sprintf """{"case":"CaseR","a":"%s"}""" id.Value
    | CaseS id -> sprintf """{"case":"CaseS","a":"%s"}""" id.Value
    | CaseT (None, x) when ignoreNulls -> sprintf """{"case":"CaseT","b":"%s"}""" x.Value
    | CaseT (None, x) -> sprintf """{"case":"CaseT","a":null,"b":"%s"}""" x.Value
    | CaseT (Some x, y) -> sprintf """{"case":"CaseT","a":"%s","b":"%s"}""" x.Value y.Value
    | CaseU skus -> sprintf """{"case":"CaseU","Item":[%s]}""" (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseV skus -> sprintf """{"case":"CaseV","skus":[%s]}""" (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseW (id, skus) -> sprintf """{"case":"CaseW","Item1":"%s","Item2":[%s]}""" id.Value (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseX (id, skus) -> sprintf """{"case":"CaseX","a":"%s","skus":[%s]}""" id.Value (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseY (a, b) -> sprintf """{"case":"CaseY","a":"%s","b":"%s"}""" (string a) (string b)
    | CaseZ (a, None) when ignoreNulls -> sprintf """{"case":"CaseZ","a":"%s"}""" (string a)
    | CaseZ (a, None) -> sprintf """{"case":"CaseZ","a":"%s","b":null}""" (string a)
    | CaseZ (a, Some b) -> sprintf """{"case":"CaseZ","a":"%s","b":"%s"}""" (string a) (string b)
    | CaseZ (a, Some b) -> sprintf """{"case":"CaseZ","a":"%s","b":"%s"}""" (string a) (string b)

type FsCheckGenerators =
    static member CartId = Arb.generate |> Gen.map CartId |> Arb.fromGen
    static member SkuId = Arb.generate |> Gen.map SkuId |> Arb.fromGen

type DomainPropertyAttribute() =
    inherit FsCheck.Xunit.PropertyAttribute(QuietOnSuccess = true, Arbitrary=[| typeof<FsCheckGenerators> |])

let roundtripProperty ignoreNulls (profile : JsonSerializerOptions) value =
    let serialized = JsonSerializer.Serialize(value, profile)
    render ignoreNulls value =! serialized
    let deserialized = JsonSerializer.Deserialize<_>(serialized, profile)
    deserialized =! value

let includeNullsProfile = Options.Create(ignoreNulls = false)
[<DomainProperty(MaxTest=1000)>]
let ``UnionConverter ignoreNulls Profile roundtrip property test`` (x: TestDU) =
    let ignoreNulls, profile = false, includeNullsProfile
    profile.IgnoreNullValues =! false
    roundtripProperty ignoreNulls profile x

let defaultProfile = Options.Create ()
[<DomainProperty(MaxTest=1000)>]
let ``UnionConverter opinionated Profile roundtrip property test`` (x: TestDU) =
    let ignoreNulls, profile = false, defaultProfile
    profile.IgnoreNullValues =! false
    roundtripProperty ignoreNulls profile x

module ``Unmatched case handling`` =

    [<Fact>]
    let ``UnionConverter by default throws on unknown cases`` () =
        let options = Options.Create(UnionConverter())
        let aJson = """{"case":"CaseUnknown"}"""
        let act () = JsonSerializer.Deserialize<TestDU>(aJson, options)

        fun (e : System.InvalidOperationException) -> <@ -1 <> e.Message.IndexOf "No case defined for 'CaseUnknown', and no catchAllCase nominated" @>
        |> raisesWith <@ act() @>

    [<RequireQualifiedAccess;
      JsonConverter(typeof<UnionConverter<DuWithCatchAllWithAttributes>>); JsonUnionConverterOptions("case", CatchAllCase = "Catchall")>]
    type DuWithCatchAllWithAttributes =
        | Known
        | Catchall

    [<Fact>]
    let ``UnionConverter supports a nominated catchall via attributes`` () =
        let aJson = """{"case":"CaseUnknown"}"""
        let a = JsonSerializer.Deserialize<DuWithCatchAllWithAttributes>(aJson)

        test <@ DuWithCatchAllWithAttributes.Catchall = a @>

    [<RequireQualifiedAccess>]
    type DuWithCatchAllWithoutAttributes =
        | Known
        | Catchall

    [<Fact>]
    let ``UnionConverter supports a nominated catchall via options`` () =
        let options = Options.Create(UnionConverter<DuWithCatchAllWithoutAttributes> ("case", "Catchall"))
        let aJson = """{"case":"CaseUnknown"}"""
        let a = JsonSerializer.Deserialize<DuWithCatchAllWithoutAttributes>(aJson, options)

        test <@ DuWithCatchAllWithoutAttributes.Catchall = a @>

    [<Fact>]
    let ``UnionConverter supports a nominated catchall with attributes overriding options`` () =
        let options = Options.Create(UnionConverter<DuWithCatchAllWithAttributes>({ discriminator = "case"; catchAllCase = None }))
        let aJson = """{"case":"CaseUnknown"}"""
        let a = JsonSerializer.Deserialize<DuWithCatchAllWithAttributes>(aJson, options)

        test <@ DuWithCatchAllWithAttributes.Catchall = a @>

    type DuWithMissingCatchAll =
        | Known

    [<Fact>]
    let ``UnionConverter explains if nominated catchAll not found`` () =
        let options = Options.Create(UnionConverter<DuWithMissingCatchAll>("case", "CatchAllThatCantBeFound"))
        let aJson = """{"case":"CaseUnknown"}"""
        let act () = JsonSerializer.Deserialize<DuWithMissingCatchAll>(aJson, options)

        fun (e : System.InvalidOperationException) -> <@ -1 <> e.Message.IndexOf "nominated catchAllCase: 'CatchAllThatCantBeFound' not found" @>
        |> raisesWith <@ act() @>

    [<NoComparison>] // Forced by usage of JsonElement
    [<JsonConverter(typeof<UnionConverter<DuWithCatchAllWithFields>>); JsonUnionConverterOptions("case", CatchAllCase = "Catchall")>]
    type DuWithCatchAllWithFields =
        | Known
        | Catchall of JsonElement

    [<Fact>]
    let ``UnionConverter can feed unknown values into a JsonElement for logging or post processing`` () =
        let json = """{"case":"CaseUnknown","a":"s","b":1,"c":true}"""
        let jo =
            trap <@ match JsonSerializer.Deserialize<DuWithCatchAllWithFields> json with
                    | Catchall jo -> jo
                    | x -> failwithf "unexpected %A" x @>

        // These can't be inside test <@ @> because of https://github.com/dotnet/fsharp/issues/6293
        let p = jo.GetProperty("a")     in p.GetString() =! "s"
        let p = jo.GetProperty("b")     in p.ValueKind   =! JsonValueKind.Number
        let p = jo.GetProperty("c")     in p.ValueKind   =! JsonValueKind.True
        let p = jo.GetProperty("case")  in p.GetString() =! "CaseUnknown"

        test <@ json = string jo @>


module ``Custom discriminator`` =

    [<JsonConverter(typeof<UnionConverter<DuWithConverterAndOptionsAttribute>>);
      JsonUnionConverterOptions("kind")>]
    type DuWithConverterAndOptionsAttribute =
    | Case1

    [<Fact>]
    let ``UnionConverter supports a nominated discriminator via options attribute with converter attribute`` () =
        let aJson = """{"kind":"Case1"}"""
        let a = JsonSerializer.Deserialize<DuWithConverterAndOptionsAttribute>(aJson)

        test <@ DuWithConverterAndOptionsAttribute.Case1 = a @>

module ``Struct discriminated unions`` =

    [<Struct>]
    type TestRecordPayloadStruct = { test: string }

    [<Struct>]
    [<JsonConverter(typeof<UnionConverter<TestStructDu>>)>]
    type TestStructDu =
        | CaseA of a : TestRecordPayload
        | CaseAV of av : TestRecordPayloadStruct
        | CaseB
        | CaseC of string
        | CaseD of d : string
        | CaseE of e : string * int
        | CaseF of f : string * fb : int
        | CaseG of g : TrickyRecordPayload
        | CaseH of h : TestRecordPayload
        | CaseHV of hv : TestRecordPayloadStruct
        | CaseI of i : TestRecordPayload * ib : string
        | CaseIV of iv : TestRecordPayloadStruct * ibv : string

    let inline serialize x = JsonSerializer.Serialize(x)

    [<Fact>]
    let ``produces expected output`` () =
        let a = CaseA { test = "hi" }
        test <@ """{"case":"CaseA","test":"hi"}""" = serialize a @>
        let a = CaseAV { test = "hi" }
        test <@ """{"case":"CaseAV","test":"hi"}""" = serialize a @>

        let b = CaseB
        test <@ """{"case":"CaseB"}""" = serialize b @>

        let c = CaseC "hi"
        test <@ """{"case":"CaseC","Item":"hi"}""" = serialize c @>

        let d = CaseD "hi"
        test <@ """{"case":"CaseD","d":"hi"}""" = serialize d @>

        let e = CaseE ("hi", 0)
        test <@ """{"case":"CaseE","e":"hi","Item2":0}""" = serialize e @>

        let f = CaseF ("hi", 0)
        test <@ """{"case":"CaseF","f":"hi","fb":0}""" = serialize f @>

        let g = CaseG { Item = "hi" }
        test <@ """{"case":"CaseG","Item":"hi"}""" = serialize g @>

        let h = CaseH { test = "hi" }
        test <@ """{"case":"CaseH","test":"hi"}""" = serialize h @>
        let h = CaseHV { test = "hi" }
        test <@ """{"case":"CaseHV","test":"hi"}""" = serialize h @>

        let i = CaseI ( {test = "hi" }, "bye")
        test <@ """{"case":"CaseI","i":{"test":"hi"},"ib":"bye"}""" = serialize i @>

        let i = CaseIV ( {test = "hi" }, "bye")
        test <@ """{"case":"CaseIV","iv":{"test":"hi"},"ibv":"bye"}""" = serialize i @>
