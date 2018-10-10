﻿module Newtonsoft.Json.Converters.FSharp.Tests.UnionConverterTests

open FsCheck
open Newtonsoft.Json
open Newtonsoft.Json.Converters.FSharp
open Swensen.Unquote.Assertions
open System
open System.IO
open global.Xunit

// TODO support [<Struct>]
type TestRecordPayload =
    {
        test: string
    }

// TODO support [<Struct>]
type TrickyRecordPayload =
    {
        Item: string
    }

[<JsonConverter(typeof<TypeSafeEnumConverter>)>]
type Mode =
    | Fast
    | Slow

[<NoComparison>] // NB this is not a general restriction; it's forced by use of Nullable<T> in some of the cases in this specific one
[<JsonConverter(typeof<UnionConverter>)>]
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

// no camel case, because I want to test "Item" as a record property
let settings = Settings.CreateDefault(camelCase = false)

[<Fact>]
let ``produces expected output`` () =
    let serialize (x : obj) = JsonConvert.SerializeObject(box x, settings)
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

    // this may not be expected, but I don't itend changing it
    let h = CaseH {test = "hi"}
    test <@ """{"case":"CaseH","test":"hi"}""" = serialize h @>

    let i = CaseI ({test = "hi"}, "bye")
    test <@ """{"case":"CaseI","a":{"test":"hi"},"b":"bye"}""" = serialize i @>

    let p = CaseP (CartId.Parse "0000000000000000948d503fcfc20f17")
    test <@ """{"case":"CaseP","Item":"0000000000000000948d503fcfc20f17"}""" = serialize p @>

    let u = CaseU [| SkuId.Parse "f09f17cb4c9744b4a979afb53be0847f"; SkuId.Parse "c747d53a644d42548b3bbc0988561ce1" |]
    test<@ """{"case":"CaseU","Item":["f09f17cb4c9744b4a979afb53be0847f","c747d53a644d42548b3bbc0988561ce1"]}""" = serialize u @>

let requiredSettingsToHandleOptionalFields =
    // NB this is me documenting current behavior - ideally optionality wou
    let s = Settings.CreateDefault(camelCase = false)
    s.Converters.Add(OptionConverter())
    s

[<Fact>]
let ``deserializes properly`` () =
    let deserialize json = JsonConvert.DeserializeObject<TestDU>(json, settings)
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

    let deserialzeCustom s = JsonConvert.DeserializeObject<TestDU>(s, requiredSettingsToHandleOptionalFields)
    test <@ CaseM (Some 1) = deserialzeCustom """{"case":"CaseM","a":1}""" @>
    test <@ CaseN (1, Some 2) = deserialzeCustom """{"case":"CaseN", "a":1, "b":2 }""" @>
    test <@ CaseO (Some 1, Some 2) = deserialzeCustom """{"case":"CaseO", "a": 1, "b": 2 }""" @>

    test <@ CaseP (CartId.Parse "0000000000000000948d503fcfc20f17") = deserialize """{"case":"CaseP","Item":"0000000000000000948d503fcfc20f17"}""" @>

    test<@ CaseU [| SkuId.Parse "f09f17cb4c9744b4a979afb53be0847f"; SkuId.Parse "c747d53a644d42548b3bbc0988561ce1" |] =
    deserialize """{"case":"CaseU","Item":["f09f17cb4c9744b4a979afb53be0847f","c747d53a644d42548b3bbc0988561ce1"]}"""@>

module MissingFieldsHandling =

    [<Fact>]
    let ``handles missing fields bound to Nullable or optional types`` () =
        let deserialize json = JsonConvert.DeserializeObject<TestDU>(json, settings)
        test <@ CaseJ (Nullable<int>()) = deserialize """{"case":"CaseJ"}""" @>
        test <@ CaseK (1, (Nullable<int>())) = deserialize """{"case":"CaseK","a":1}""" @>
        test <@ CaseL ((Nullable<int>()), (Nullable<int>())) = deserialize """{"case":"CaseL"}""" @>

        test <@ CaseM None = deserialize """{"case":"CaseM"}""" @>
        test <@ CaseN (1, None) = deserialize """{"case":"CaseN","a":1}""" @>
        test <@ CaseO (None, None) = deserialize """{"case":"CaseO"}""" @>

    let rejectMissingSettings =
        [   JsonSerializerSettings(MissingMemberHandling = MissingMemberHandling.Error, NullValueHandling=NullValueHandling.Ignore)
            // Provides same settings as above wrt how UnionEncoder will handle this
            Settings.CreateDefault(ignoreNulls=true, errorOnMissing=true)]

    [<Fact>]
    let ``lets converters reject missing valus by feeding them a null`` () =
        raisesWith <@ JsonConvert.DeserializeObject<TestDU>("""{"case":"CaseY","a":"Fast"}""") @>
            (fun e -> <@ "Unexpected token when reading TypeSafeEnum: Null" = e.Message @>)
        raises<ArgumentNullException> <@ JsonConvert.DeserializeObject<TestDU>("""{"case":"CaseX"}""") @>

    type TestRecordWithArray = { sku : string; [<JsonRequired>]skus: string[] }

    [<Fact>]
    let ``currently can't guard against null Arrays, but that's not a default so we live with it`` () =
        raisesWith<JsonSerializationException> <@ JsonConvert.DeserializeObject<TestRecordWithArray>("""{"sku": null }""") @>
            (fun e -> <@ e.Message.StartsWith "Required property 'skus' not found in JSON. Path ''" @>)
        let missingTheArray = sprintf """{"case":"CaseX","a":"%O"}""" Guid.Empty
        test <@ CaseX (CartId Guid.Empty,null) = JsonConvert.DeserializeObject<TestDU>(missingTheArray) @>

let (|Q|) (s : string) = Newtonsoft.Json.JsonConvert.SerializeObject s

let render = function
    | CaseA { test = null } -> """{"case":"CaseA"}"""
    | CaseA { test = Q x} -> sprintf """{"case":"CaseA","test":%s}""" x
    | CaseB -> """{"case":"CaseB"}"""
    | CaseC null -> """{"case":"CaseC"}"""
    | CaseC (Q s) -> sprintf """{"case":"CaseC","Item":%s}""" s
    | CaseD null -> """{"case":"CaseD"}"""
    | CaseD (Q s) -> sprintf """{"case":"CaseD","a":%s}""" s
    | CaseE (null,y) -> sprintf """{"case":"CaseE","Item2":%d}""" y
    | CaseE (Q x,y) -> sprintf """{"case":"CaseE","Item1":%s,"Item2":%d}""" x y
    | CaseF (null,y) -> sprintf """{"case":"CaseF","b":%d}""" y
    | CaseF (Q x,y) -> sprintf """{"case":"CaseF","a":%s,"b":%d}""" x y
    | CaseG {Item = null} -> """{"case":"CaseG"}"""
    | CaseG {Item = Q s} -> sprintf """{"case":"CaseG","Item":%s}""" s
    | CaseH {test = null} -> """{"case":"CaseH"}"""
    | CaseH {test = Q s} -> sprintf """{"case":"CaseH","test":%s}""" s
    | CaseI ({test = null}, null) -> """{"case":"CaseI","a":{}}"""
    | CaseI ({test = null}, Q s) -> sprintf """{"case":"CaseI","a":{},"b":%s}""" s
    | CaseI ({test = Q s}, null) -> sprintf """{"case":"CaseI","a":{"test":%s}}""" s
    | CaseI ({test = Q s}, Q b) -> sprintf """{"case":"CaseI","a":{"test":%s},"b":%s}""" s b

    | CaseJ x when not x.HasValue -> """{"case":"CaseJ"}"""
    | CaseJ x -> sprintf """{"case":"CaseJ","a":%d}""" x.Value
    | CaseK (a,x) when not x.HasValue -> sprintf """{"case":"CaseK","a":%d}""" a
    | CaseK (a,x) -> sprintf """{"case":"CaseK","a":%d,"b":%d}""" a x.Value
    | CaseL (a,b) when not a.HasValue && not b.HasValue -> """{"case":"CaseL"}"""
    | CaseL (a,b) when not a.HasValue -> sprintf """{"case":"CaseL","b":%d}""" b.Value
    | CaseL (a,b) when not b.HasValue -> sprintf """{"case":"CaseL","a":%d}""" a.Value
    | CaseL (a,b) -> sprintf """{"case":"CaseL","a":%d,"b":%d}""" a.Value b.Value

    | CaseM None -> """{"case":"CaseM"}"""
    | CaseM (Some x) -> sprintf """{"case":"CaseM","a":%d}""" x
    | CaseN (a,None) -> sprintf """{"case":"CaseN","a":%d}""" a
    | CaseN (a,x) -> sprintf """{"case":"CaseN","a":%d,"b":%d}""" a x.Value
    | CaseO (None,None) -> """{"case":"CaseO"}"""
    | CaseO (None,b) -> sprintf """{"case":"CaseO","b":%d}""" b.Value
    | CaseO (a,None) -> sprintf """{"case":"CaseO","a":%d}""" a.Value
    | CaseO (Some a,Some b) -> sprintf """{"case":"CaseO","a":%d,"b":%d}""" a b
    | CaseP id -> sprintf """{"case":"CaseP","Item":"%s"}""" id.Value
    | CaseQ id -> sprintf """{"case":"CaseQ","Item":"%s"}""" id.Value
    | CaseR id -> sprintf """{"case":"CaseR","a":"%s"}""" id.Value
    | CaseS id -> sprintf """{"case":"CaseS","a":"%s"}""" id.Value
    | CaseT (None, x) -> sprintf """{"case":"CaseT","b":"%s"}""" x.Value
    | CaseT (Some x, y) -> sprintf """{"case":"CaseT","a":"%s","b":"%s"}""" x.Value y.Value
    | CaseU skus -> sprintf """{"case":"CaseU","Item":[%s]}""" (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseV skus -> sprintf """{"case":"CaseV","skus":[%s]}""" (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseW (id, skus) -> sprintf """{"case":"CaseW","Item1":"%s","Item2":[%s]}""" id.Value (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseX (id, skus) -> sprintf """{"case":"CaseX","a":"%s","skus":[%s]}""" id.Value (skus |> Seq.map (fun s -> sprintf "\"%s\"" s.Value) |> String.concat ",")
    | CaseY (a, b) -> sprintf """{"case":"CaseY","a":"%s","b":"%s"}""" (string a) (string b)

type FsCheckGenerators =
    static member CartId = Arb.generate |> Gen.map CartId |> Arb.fromGen
    static member SkuId = Arb.generate |> Gen.map SkuId |> Arb.fromGen

type DomainPropertyAttribute() =
    inherit FsCheck.Xunit.PropertyAttribute(QuietOnSuccess = true, Arbitrary=[| typeof<FsCheckGenerators> |])

[<DomainPropertyAttribute(MaxTest=1000)>]
let ``UnionConverter roundtrip property test`` (x: TestDU) =
    let serialized = JsonConvert.SerializeObject(x, requiredSettingsToHandleOptionalFields)
    render x =! serialized
    let deserialized = JsonConvert.DeserializeObject<_>(serialized, requiredSettingsToHandleOptionalFields)
    deserialized =! x

[<Fact>]
let ``Implementation ensures no internal errors escape (which would render a WebApi ModelState.Invalid)`` () =
    let s = JsonSerializer.CreateDefault()
    let mutable gotError = false
    s.Error.Add(fun _ -> gotError <- true)

    let dJson = """{"case":"CaseD","a":"hi"}"""
    use dReader = new StringReader(dJson)
    use dJsonReader = new JsonTextReader(dReader)
    let d = s.Deserialize<TestDU>(dJsonReader)

    test <@ (CaseD "hi") = d @>
    test <@ false = gotError @>

module ``Unmatched case handling`` =

    [<Fact>]
    let ``UnionConverter by default throws on unknown cases`` () =
        let aJson = """{"case":"CaseUnknown"}"""
        let act () = JsonConvert.DeserializeObject<TestDU>(aJson, settings)

        fun (e : System.InvalidOperationException) -> <@ -1 <> e.Message.IndexOf "No case defined for 'CaseUnknown', and no catchAllCase nominated" @>
        |> raisesWith <@ act() @>

    [<JsonConverter(typeof<UnionConverter>, "case", "Catchall")>]
    type DuWithCatchAll =
    | Known
    | Catchall

    [<Fact>]
    let ``UnionConverter supports a nominated catchall`` () =
        let aJson = """{"case":"CaseUnknown"}"""
        let a = JsonConvert.DeserializeObject<DuWithCatchAll>(aJson, settings)

        test <@ Catchall = a @>

    [<JsonConverter(typeof<UnionConverter>, "case", "CatchAllThatCantBeFound")>]
    type DuWithMissingCatchAll =
    | Known

    [<Fact>]
    let ``UnionConverter explains if nominated catchAll not found`` () =
        let aJson = """{"case":"CaseUnknown"}"""
        let act () = JsonConvert.DeserializeObject<DuWithMissingCatchAll>(aJson, settings)

        fun (e : System.InvalidOperationException) -> <@ -1 <> e.Message.IndexOf "nominated catchAllCase: 'CatchAllThatCantBeFound' not found" @>
        |> raisesWith <@ act() @>

    [<NoComparison>] // Forced by usage of JObject
    [<JsonConverter(typeof<UnionConverter>, "case", "Catchall")>]
    type DuWithCatchAllWithFields =
    | Known
    | Catchall of Newtonsoft.Json.Linq.JObject

    [<Fact>]
    let ``UnionConverter can feed unknown values into a JObject for logging or post processing`` () =
        let deserialize json = JsonConvert.DeserializeObject<DuWithCatchAllWithFields>(json, settings)
        let jo =
            trap <@ match deserialize """{"case":"CaseUnknown","a":"s","b":1,"c":true}""" with
                    | Catchall jo -> jo
                    | x -> failwithf "unexpected %A" x @>

        test <@ string jo.["a"]="s"
                && jo.["b"].Type=Newtonsoft.Json.Linq.JTokenType.Integer
                && jo.["c"].Type=Newtonsoft.Json.Linq.JTokenType.Boolean
                && string jo.["case"]="CaseUnknown" @>
        let expected  = "{\r\n  \"case\": \"CaseUnknown\",\r\n  \"a\": \"s\",\r\n  \"b\": 1,\r\n  \"c\": true\r\n}"
        test <@ expected = string jo @>