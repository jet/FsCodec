module FsCodec.NewtonsoftJson.Tests.PicklerTests

open FsCodec.NewtonsoftJson
open Newtonsoft.Json
open Swensen.Unquote
open System
open Xunit

open FsCodec.NewtonsoftJson.Tests.Fixtures

// NB Feel free to ignore this opinion and copy the 4 lines into your own globals - the pinning test will remain here
/// <summary>
///     Renders all Guids without dashes.
/// </summary>
/// <remarks>
///     Can work correctly as a global converter, as some codebases do for historical reasons
///     Could arguably be usable as base class for various converters, including the above.
///     However, the above pattern and variants thereof are recommended for new types.
///     In general, the philosophy is that, beyond the Pickler base types, an identiy type should consist of explicit
///       code as much as possible, and global converters really have to earn their keep - magic starts with -100 points.
/// </remarks>
type GuidConverter() =
    inherit JsonIsomorphism<Guid, string>()
    override _.Pickle g = g.ToString "N"
    override _.UnPickle g = Guid.Parse g

type WithEmbeddedGuid = { a: string; [<JsonConverter(typeof<GuidConverter>)>] b: Guid }

let [<Fact>] ``Tagging with GuidConverter`` () =
    let value = { a = "testing"; b = Guid.Empty }

    let result = JsonConvert.SerializeObject value

    test <@ """{"a":"testing","b":"00000000000000000000000000000000"}""" = result @>

let [<Fact>] ``Global GuidConverter`` () =
    let value = Guid.Empty

    let resDashes = JsonConvert.SerializeObject(value, Options.Default)
    let resNoDashes = JsonConvert.SerializeObject(value, Options.Create(GuidConverter()))

    test <@ "\"00000000-0000-0000-0000-000000000000\"" = resDashes
            && "\"00000000000000000000000000000000\"" = resNoDashes @>

module ``Adding Fields Example`` =

    module CartV1 =
        type CreateCart = { name: string }

    module CartV2Null =
        type CreateCart = { name: string; CartId: CartId }

    module CartV2 =
        type CreateCart = { name: string; CartId: CartId option }

    let [<Fact>] ``Deserialize missing field as null value`` () =
        let createCartV1: CartV1.CreateCart =  { name = "cartName" }
        // let expectedCreateCartV2: CartV2Null.CreateCart =  { Name = "cartName"; CartId = null } // The type 'CartId' does not have 'null' as a proper value

        let createCartV1Json = JsonConvert.SerializeObject createCartV1

        let createCartV2 = JsonConvert.DeserializeObject<CartV2Null.CreateCart>(createCartV1Json)

        test <@ Unchecked.defaultof<_> = createCartV2.CartId @> // isNull or `null =` will be rejected

    let [<Fact>] ``Deserialize missing field as an optional property None value`` () =
        let createCartV1: CartV1.CreateCart =  { name = "cartName" }

        let createCartV1Json = JsonConvert.SerializeObject createCartV1

        let createCartV2 = JsonConvert.DeserializeObject<CartV2.CreateCart>(createCartV1Json)

        test <@ Option.isNone createCartV2.CartId @>

module ``Upconversion example`` =

    module Events =
        type Properties = { a: string }
        type PropertiesV2 = { a: string; b: int }
        type Event =
            | PropertiesUpdated of {| properties:Properties |}
            | PropertiesUpdatedV2 of {| properties:PropertiesV2 |}

    module EventsUpDown =
        type Properties = { a: string }
        type PropertiesV2 = { a: string; b: int }
        module PropertiesV2 =
            let defaultB = 2
        /// The possible representations within the store
        [<RequireQualifiedAccess>]
        type Contract =
            | PropertiesUpdated of {| properties: Properties |}
            | PropertiesUpdatedV2 of {| properties: PropertiesV2 |}
            interface TypeShape.UnionContract.IUnionContract
        /// Used in the model - all decisions and folds are in terms of this
        type Event =
            | PropertiesUpdated of {| properties: PropertiesV2 |}

        let up: Contract -> Event = function
            | Contract.PropertiesUpdated e -> PropertiesUpdated  {| properties = { a = e.properties.a; b = PropertiesV2.defaultB } |}
            | Contract.PropertiesUpdatedV2 e -> PropertiesUpdated e
        let down: Event -> Contract = function
            | Event.PropertiesUpdated e -> Contract.PropertiesUpdatedV2 e
        let codec = Codec.Create<Event, Contract, _>(up = (fun _e c -> up c),
                                                     down = fun e -> struct (down e, ValueNone, ValueNone))

    module Fold =

        type State = unit
        // evolve functions
        let evolve state = function
        | EventsUpDown.Event.PropertiesUpdated e -> state

module ``Upconversion active patterns`` =

    module Events =
        type Properties = { a: string }
        type PropertiesV2 = { a: string; b: int }
        module PropertiesV2 =
            let defaultB = 2
        type Event =
            | PropertiesUpdated of {| properties: Properties |}
            | PropertiesUpdatedV2 of {| properties: PropertiesV2 |}
        let (|Updated|) = function
            | PropertiesUpdated e -> {| properties = { a = e.properties.a; b = PropertiesV2.defaultB } |}
            | PropertiesUpdatedV2 e -> e
    module Fold =
        type State = { b : int }
        let evolve state : Events.Event -> State = function
        | Events.Updated e -> { state with b = e.properties.b }
