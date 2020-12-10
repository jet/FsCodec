module FsCodec.NewtonsoftJson.Tests.UnionConverterTests

open FsCheck
open FsCodec.NewtonsoftJson
open Newtonsoft.Json
open Swensen.Unquote.Assertions
open System
open System.IO
open global.Xunit

open Fixtures

// TODO support [<Struct>]
type TestRecordPayload =
    {   test: string
    }

// TODO support [<Struct>]
type TrickyRecordPayload =
    {   Item: string
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
    | CaseZ of a: Mode * b: Mode option

#nowarn "1182" // From hereon in, we may have some 'unused' privates (the tests)

// no camel case, because I want to test "Item" as a record property
// Centred on ignoreNulls for backcompat; round-tripping test covers the case where they get rendered too
let settings = Settings.Create(camelCase = false, ignoreNulls = true)

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
    let deserialize json = JsonConvert.DeserializeObject<TestDU>(json, settings)
    test <@ CaseA {test = null} = deserialize """{"case":"CaseA"}""" @>
    test <@ CaseA {test = "hi"} = deserialize """{"case":"CaseA","test":"hi"}""" @>
    test <@ CaseA {test = "hi"} = deserialize """{"case":"CaseA","test":"hi","extraField":"hello"}""" @>

    test <@ CaseB = deserialize """{"case":"CaseB"}""" @>

    test <@ CaseC "hi" = deserialize """{"case":"CaseC","Item":"hi"}""" @>

    test <@ CaseD "hi" = deserialize """{"case":"CaseD","a":"hi"}""" @>

    test <@ CaseE ("hi", 0) = deserialize """{"case":"CaseE","Item1":"hi","Item2":0}""" @>
    // NB this only passes by virtue of MissingMemberHandling=Ignore and NullValueHandling=Ignore in default settings
    test <@ CaseE (null, 0) = deserialize """{"case":"CaseE","Item3":"hi","Item4":0}""" @>

    test <@ CaseF ("hi", 0) = deserialize """{"case":"CaseF","a":"hi","b":0}""" @>

    test <@ CaseG {Item = "hi"} = deserialize """{"case":"CaseG","Item":"hi"}""" @>

    test <@ CaseH {test = "hi"} = deserialize """{"case":"CaseH","test":"hi"}""" @>

    test <@ CaseI ({test = "hi"}, "bye") = deserialize """{"case":"CaseI","a":{"test":"hi"},"b":"bye"}""" @>

    test <@ CaseJ (Nullable 1) = deserialize """{"case":"CaseJ","a":1}""" @>
    test <@ CaseK (1, Nullable 2) = deserialize """{"case":"CaseK", "a":1, "b":2 }""" @>
    test <@ CaseL (Nullable 1, Nullable 2) = deserialize """{"case":"CaseL", "a": 1, "b": 2 }""" @>

    let requiredSettingsToHandleOptionalFields = Settings.Create()
    let deserializeCustom s = JsonConvert.DeserializeObject<TestDU>(s, requiredSettingsToHandleOptionalFields)
    test <@ CaseM (Some 1) = deserializeCustom """{"case":"CaseM","a":1}""" @>
    test <@ CaseN (1, Some 2) = deserializeCustom """{"case":"CaseN", "a":1, "b":2 }""" @>
    test <@ CaseO (Some 1, Some 2) = deserializeCustom """{"case":"CaseO", "a": 1, "b": 2 }""" @>

    test <@ CaseP (CartId.Parse "0000000000000000948d503fcfc20f17") = deserialize """{"case":"CaseP","Item":"0000000000000000948d503fcfc20f17"}""" @>

    test<@ CaseU [| SkuId.Parse "f09f17cb4c9744b4a979afb53be0847f"; SkuId.Parse "c747d53a644d42548b3bbc0988561ce1" |] =
    deserialize """{"case":"CaseU","Item":["f09f17cb4c9744b4a979afb53be0847f","c747d53a644d42548b3bbc0988561ce1"]}"""@>

module MissingFieldsHandling =

    let rejectMissingSettings =
        [   JsonSerializerSettings(MissingMemberHandling = MissingMemberHandling.Error)
            Settings.CreateDefault(errorOnMissing=true)
            Settings.Create(errorOnMissing=true)]

    [<Fact>]
    let ``lets converters reject missing values by feeding them a null`` () =
        raisesWith <@ JsonConvert.DeserializeObject<TestDU>("""{"case":"CaseY","a":"Fast"}""") @>
            (fun e -> <@ "Unexpected token when reading TypeSafeEnum: Null" = e.Message @>)
        raises<ArgumentNullException> <@ JsonConvert.DeserializeObject<TestDU>("""{"case":"CaseX"}""") @>

    [<Fact>]
    let ``types with converters that would reject missing values can be guarded by wrapping in an option`` () =
        test <@ CaseZ (Fast,None) = JsonConvert.DeserializeObject<TestDU>("""{"case":"CaseZ","a":"Fast"}""") @>

    [<Fact>]
    let ``rejects missing fields bound to non-optional value types when requested`` () =
        for s in rejectMissingSettings do
            let deserializeWithMissingMembersAsError json = JsonConvert.DeserializeObject<TestDU>(json, s)
            raisesWith <@ deserializeWithMissingMembersAsError """{"case":"CaseE","Item3":"hi","Item4":0}"""  @>
                (fun (e : JsonSerializationException) ->
                    <@  e.Message.StartsWith "Error converting value {null} to type 'System.Int32'. Path ''"
                        && e.InnerException.GetType() = typeof<InvalidCastException>
                        && e.InnerException.Message.Contains "Null object cannot be converted to a value type." @>)

    type TestRecordWithArray = { sku : string; [<JsonRequired>]skus: string[] }

    [<Fact>]
    let ``currently can't guard against null Arrays, but that's not a default so we live with it`` () =
        raisesWith<JsonSerializationException> <@ JsonConvert.DeserializeObject<TestRecordWithArray>("""{"sku": null }""") @>
            (fun e -> <@ e.Message.StartsWith "Required property 'skus' not found in JSON. Path ''" @>)
        let missingTheArray = sprintf """{"case":"CaseX","a":"%O"}""" Guid.Empty
        test <@ CaseX (CartId Guid.Empty,null) = JsonConvert.DeserializeObject<TestDU>(missingTheArray) @>

    [<Fact>]
    let ``handles missing fields bound to Nullable or optional types`` () =
        let deserialize json = JsonConvert.DeserializeObject<TestDU>(json, settings)
        test <@ CaseJ (Nullable<int>()) = deserialize """{"case":"CaseJ"}""" @>
        test <@ CaseK (1, (Nullable<int>())) = deserialize """{"case":"CaseK","a":1}""" @>
        test <@ CaseL ((Nullable<int>()), (Nullable<int>())) = deserialize """{"case":"CaseL"}""" @>

        test <@ CaseM None = deserialize """{"case":"CaseM"}""" @>
        test <@ CaseN (1, None) = deserialize """{"case":"CaseN","a":1}""" @>
        test <@ CaseO (None, None) = deserialize """{"case":"CaseO"}""" @>

let (|Q|) (s: string) = Newtonsoft.Json.JsonConvert.SerializeObject s

// Renderings when NullValueHandling=Include, which is the default for Json.net, and used by the recommended Settings.CreateCorrect profile
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

let roundtripProperty ignoreNulls (profile : JsonSerializerSettings) value =
    let serialized = JsonConvert.SerializeObject(value, profile)
    render ignoreNulls value =! serialized
    let deserialized = JsonConvert.DeserializeObject<_>(serialized, profile)
    deserialized =! value

let includeNullsProfile = Settings.CreateDefault(OptionConverter())
[<DomainProperty(MaxTest=1000)>]
let ``UnionConverter ignoreNulls Profile roundtrip property test`` (x: TestDU) =
    let ignoreNulls, profile = false, includeNullsProfile
    profile.NullValueHandling =! NullValueHandling.Include
    roundtripProperty ignoreNulls profile x

let defaultProfile = Settings.Create()
[<DomainProperty(MaxTest=1000)>]
let ``UnionConverter opinionated Profile roundtrip property test`` (x: TestDU) =
    let ignoreNulls, profile = false, defaultProfile
    profile.NullValueHandling =! NullValueHandling.Include
    roundtripProperty ignoreNulls profile x

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

module CustomDiscriminator =

    [<JsonConverter(typeof<UnionConverter>, "esac")>]
    type DuWithCustomDiscriminator =
        | Known
        | Catchall

    [<Fact>]
    let ``UnionConverter handles custom discriminator`` () =
        let json = """{"esac":"Known"}"""
        test <@ DuWithCustomDiscriminator.Known = JsonConvert.DeserializeObject<_> json @>

    [<Fact>]
    let ``UnionConverter can complain about missing case with custom discriminator without catchall`` () =
        let aJson = """{"esac":"CaseUnknown"}"""
        let act () = JsonConvert.DeserializeObject<DuWithCustomDiscriminator>(aJson, settings)

        fun (e : System.InvalidOperationException) -> <@ -1 <> e.Message.IndexOf "No case defined for 'CaseUnknown', and no catchAllCase nominated" @>
        |> raisesWith <@ act() @>

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
        let expected  = "{\r\n  \"case\": \"CaseUnknown\",\r\n  \"a\": \"s\",\r\n  \"b\": 1,\r\n  \"c\": true\r\n}".Replace("\r\n",Environment.NewLine)
        test <@ expected = string jo @>

module Nested =

    [<JsonConverter(typeof<UnionConverter>)>]
    type U =
        | B of NU
        | C of UUA
        | D of UU
        | E of E
        | EA of E[]
        | R of {| a : int; b : NU |}
        | S
    and [<JsonConverter(typeof<UnionConverter>)>]
        NU =
        | A of string
        | B of int
        | R of {| a : int; b : NU |}
        | S
    and [<JsonConverter(typeof<UnionConverter>)>]
        UU =
        | A of string
        | B of int
        | E of E
        | EO of E option
        | R of {| a: int; b: string |}
        | S
    and [<JsonConverter(typeof<UnionConverter>, "case2")>]
        UUA =
        | A of string
        | B of int
        | E of E
        | EO of E option
        | R of {| a: int; b: string |}
        | S
    and [<JsonConverter(typeof<TypeSafeEnumConverter>)>]
        E =
        | V1
        | V2

    let [<FsCheck.Xunit.Property>] ``can nest`` (value : U) =
        let ser = Serdes.Serialize value
        test <@ value = Serdes.Deserialize ser @>

    let [<Fact>] ``nesting Unions represents child as item`` () =
        let v : U = U.C(UUA.B 42)
        let ser = Serdes.Serialize v
        """{"case":"C","Item":{"case2":"B","Item":42}}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

    let [<Fact>] ``TypeSafeEnum converts direct`` () =
        let v : U = U.C (UUA.E E.V1)
        let ser = Serdes.Serialize v
        """{"case":"C","Item":{"case2":"E","Item":"V1"}}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

        let v : U = U.E E.V2
        let ser = Serdes.Serialize v
        """{"case":"E","Item":"V2"}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

        let v : U = U.EA [|E.V2; E.V2|]
        let ser = Serdes.Serialize v
        """{"case":"EA","Item":["V2","V2"]}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

        let v : U = U.C (UUA.EO (Some E.V1))
        let ser = Serdes.Serialize v
        """{"case":"C","Item":{"case2":"EO","Item":"V1"}}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

        let v : U = U.C (UUA.EO None)
        let ser = Serdes.Serialize v
        """{"case":"C","Item":{"case2":"EO","Item":null}}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

        let v : U = U.C UUA.S
        let ser = Serdes.Serialize v
        """{"case":"C","Item":{"case2":"S"}}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

/// And for everything else, JsonIsomorphism allows plenty ways of customizing the encoding and/or decoding
module IsomorphismUnionEncoder =

    type [<JsonConverter(typeof<TopConverter>)>]
        Top =
        | S
        | N of Nested
    and Nested =
        | A
        | B of int
    and TopConverter() =
        inherit JsonIsomorphism<Top, Flat<int>>()
        override __.Pickle value =
            match value with
            | S -> { disc = TS; v = None }
            | N A -> { disc = TA; v = None }
            | N (B v) -> { disc = TB; v = Some v }
        override __.UnPickle flat =
            match flat with
            | { disc = TS } -> S
            | { disc = TA } -> N A
            | { disc = TB; v = v} -> N (B (Option.get v))
    and Flat<'T> = { disc : JiType; v : 'T option }
    and [<JsonConverter(typeof<TypeSafeEnumConverter>)>]
        JiType = TS | TA | TB

    let [<Fact>] ``Can control the encoding to the nth degree`` () =
        let v : Top = N (B 42)
        let ser = Serdes.Serialize v
        """{"disc":"TB","v":42}""" =! ser
        test <@ v = Serdes.Deserialize ser @>

    let [<FsCheck.Xunit.Property>] ``can roundtrip`` (value : Top) =
        let ser = Serdes.Serialize value
        test <@ value = Serdes.Deserialize ser @>
