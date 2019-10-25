# FsCodec [![Build Status](https://dev.azure.com/jet-opensource/opensource/_apis/build/status/jet.fscodec?branchName=master)](https://dev.azure.com/jet-opensource/opensource/_build/latest?definitionId=18?branchName=master) [![release](https://img.shields.io/github/release/jet/fscodec.svg)](https://github.com/jet/fscodec/releases) [![NuGet](https://img.shields.io/nuget/vpre/fscodec.svg?logo=nuget)](https://www.nuget.org/packages/fscodec/) [![license](https://img.shields.io/github/license/jet/fscodec.svg)](LICENSE)

Defines a minimal interface for serialization and deserialization of events for event-sourcing systems on .NET.
Includes ready-to-go packages for writing simple yet versionable Event Contract definitions in F# using ubiquitous serializers (presently `Newtonsoft.Json`).

Typically used in [applications](https://github.com/jet/dotnet-templates) leveraging [Equinox](https://github.com/jet/equinox) and/or [Propulsion](https://github.com/jet/propulsion), but also applicable to defining DTOs for other purposes such as Web APIs.

## Components

The components within this repository are delivered as multi-targeted Nuget packages supporting `net461` (F# 3.1+) and `netstandard2.0` (F# 4.5+) profiles.

- [![Codec NuGet](https://img.shields.io/nuget/v/FsCodec.svg)](https://www.nuget.org/packages/FsCodec/) `FsCodec` Defines interfaces with trivial implementation helpers.
  - No dependencies.
  - [`FsCodec.IUnionEncoder`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L19): defines a base interface for serializers.
  - [`FsCodec.Codec`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/Codec.fs#L5): enables plugging in a serializer and/or Union Encoder of your choice (typically this is used to supply a pair `encode` and `tryDecode` functions)
- [![Newtonsoft.Json Codec NuGet](https://img.shields.io/nuget/v/FsCodec.NewtonsoftJson.svg)](https://www.nuget.org/packages/FsCodec.NewtonsoftJson/) `FsCodec.NewtonsoftJson`: As described in [a scheme for the serializing Events modelled as an F# Discriminated Union](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores/), enabled tagging of F# Discriminated Union cases in a versionable manner with low-dependencies using [TypeShape](https://github.com/eiriktsarpalis/TypeShape)'s [`UnionContractEncoder`](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores)
  - Uses the ubiquitous [`Newtonsoft.Json`](https://github.com/JamesNK/Newtonsoft.Json) library to serialize the event bodies.
  - Provides relevant Converters for common non-primitive types prevalent in F#
  - [depends](https://www.fuget.org/packages/FsCodec.NewtonsoftJson) on `FsCodec`, `Newtonsoft.Json >= 11.0.2`, `TypeShape >= 8`, `Microsoft.IO.RecyclableMemoryStream >= 1.2.2`, `System.Buffers >= 4.5`
- [_(planned)_ `FsCodec.SystemTextJson`](https://github.com/jet/FsCodec/issues/14): drop in replacement that allows one to retarget from `Newtonsoft.Json` to imminently ubiquitous .NET `System.Text.Json` serializer solely by changing the referenced namespace.

# Features: `FsCodec`

The purpose of the `FsCodec` package is to provide a minimal interface on which libraries such as Equinox and Propulsion can depend on in order that they can avoid forcing a specific serialization mechanism.

- [`FsCodec.IEventData`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L4) represents a single event and/or related metadata in raw form (i.e. still as a UTF8 string etc, not yet bound to a specific Event Type)
- [`FsCodec.ITimelineEvent`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L23) represents a single stored event and/or related metadata in raw form (i.e. still as a UTF8 string etc, not yet bound to a specific Event Type). Inherits `IEventData`, adding `Index` and `IsUnfold` in order to represent the position on the timeline which the event logically occupies.
- [`FsCodec.IUnionEncoder`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L31) presents `Encode : 'Context option * 'Union -> IEventData` and `TryDecode : ITimelineEvent -> 'Union option` methods that can be used in low level application code to generate `IEventData`s or decode `ITimelineEvent`s based on a contract defined by `'Union`
- [`FsCodec.Codec.Create`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/Codec.fs#L27) implements `IUnionEncoder` in terms of supplied `encode : 'Union -> string * byte[]` and `tryDecode : string * byte[] -> 'Union option` functions (other overloads are available for advanced cases)
- [`FsCodec.Core.EventData.Create`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L44) is a low level helper to create an `IEventData` directly for purposes such as tests etc.
- [`FsCodec.Core.TimelineEvent.Create`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L58) is a low level helper to create an `ITimelineEvent` directly for purposes such as tests etc.

# Examples: `FsCodec.NewtonsoftJson`

There's a test playground in [tests/FsCodec.NewtonsoftJson.Tests/Examples.fsx](tests/FsCodec.NewtonsoftJson.Tests/Examples.fsx). It's highly recommended to experiment with conversions using FSI. (Also, PRs adding examples are much appreciated...)

# Features: `FsCodec.NewtonsoftJson`

[`FsCodec.NewtonsoftJson.Codec`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Codec.fs) provides an implementation of `IUnionEncoder` as described in [a scheme for the serializing Events modelled as an F# Discriminated Union](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores/). This yields a clean yet versionable way of managing the roundtripping events based on a contract inferred from an F# Discriminated Union Type using `Newtonsoft.Json >= 11.0.2` to serialize the bodies.

## `Newtonsoft.Json.Converter`s

`FsCodec.NewtonsoftJson` includes relevant `Converters` in order to facilitate interoperable and versionable renderings:
  - [`OptionConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/OptionConverter.fs#L7) represents F#'s `Option<'t>` as a value or `null`
  - [`TypeSafeEnumConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/TypeSafeEnumConverter.fs#L33) represents discriminated union (whose cases are all nullary), as a `string` in a trustworthy manner (`Newtonsoft.Json.Converters.StringEnumConverter` permits values outside the declared values) :pray: [@amjjd](https://github.com/amjjd)
  - [`UnionConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/UnionConverter.fs#L71) represents F# discriminated unions as a single JSON `object` with both the tag value and the body content as named fields directly within(`Newtonsoft.Json.Converters.DiscriminatedUnionConverter` encodes the fields as an array without names, which has some pros, but many obvious cons) :pray: [@amjdd](https://github.com/amjjd)
  - [`VerbatimUtf8JsonConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/VerbatimUtf8JsonConverter.fs#L7) captures/renders known valid UTF8 JSON data into a `byte[]` without decomposing it into an object model (not typically relevant for application level code)  

## Custom converter base classes

- [`JsonIsomorphism`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Pickler.fs#L49) - allows one to cleanly map a type's internal representation to something that Json.net can already cleanly handle :pray: [@EirikTsarpalis](https://github.com/eiriktsarpalis)
- [`JsonPickler`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Pickler.fs#L15) - removes boilerplate from simple converters, used in implementation of `JsonIsomorphism` :pray: [@EirikTsarpalis](https://github.com/eiriktsarpalis) 

## `Settings`

[`FsCodec.NewtonsoftJson.Settings`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Settings.fs#L8) provides a clean syntax for building a `Newtonsoft.Json.JsonSerializerSettings` with which to define a serialization contract profile for interoperability purposes. Methods:
- `CreateDefault`: as per `Newtonsoft.Json` defaults with the following override:
  - `DateTimeZoneHandling = DateTimeZoneHandling.Utc` (default is `RoundtripKind`)
  - no custom `IContractResolver` (one is expected to use `camelCase` field names within records, for which this does not matter)
- `Create`: as `CreateDefault` with the following difference:
  - adds an `OptionConverter`; included in default `Settings` (see _Converters_, above and _Setttings_ below)

## `Serdes`

[`FsCodec.NewtonsoftJson.Serders`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Serdes.fs#L7) provides light wrappers over `JsonConvert\.(Des|S)erializeObject` that utilize the serialization profile defined by `Settings.Create` (above). Methods:
- `Serialize<T>`: serializes an object per its type using the settings defined in `Settings.Create`
- `Deserialize<T>`: deserializes an object per its type using the settings defined in `Settings.Create`

 <a name="contracts"></a>
### Examples of using `Settings` and `Serdes` to define a contract

In a contract assembly used as a way to supply types as part of a client library, one way of encapsulating the conversion rules that need to be applied is as follows:

#### Simple contracts that tag all types or fields necessitating `Converter`s directly and only records and `option`s

The minimal code needed to define helpers to consistently roundtrip where one only uses simple types is to simply state" _Please use `FsCodec.NewtonsoftJson.Serdes` to encode/decode json payloads correctly. However, an alternate approach is to employ the convention of providing a pair of helper methods alongside the type :-

```fsharp
module Contract =
    type Item = { value : string option }
    // implies default settings from Settings.Create(), which includes OptionConverter
    let serialize (x : Item) : string = FsCodec.NewtonsoftJson.Serdes.Serialize x
    // implies default settings from Settings.Create(), which includes OptionConverter
    let deserialize (json : string) = FsCodec.NewtonsoftJson.Serdes.Deserialize json
```

#### More advanced case necessitating a custom converter

While it's hard to justify the wrapping in the previous case, this illustrates how one can employ the same basic layout yet override a setting (register a necessary custom `Newtonsoft.Json.Converter` type):

```fsharp
module Contract =
    type Item = { value : string option; other : TypeThatRequiresMyCustomConverter }
    /// Settings to be used within this contract
    // note OptionConverter is also included by default
    let settings = FsCodec.NewtonsoftJson.Settings.Create(converters = [| MyCustomConverter() |])
    let serialize (x : Item) = FsCodec.NewtonsoftJson.Serdes.Serialize(x,settings)
    let deserialize (json : string) : Item = FsCodec.NewtonsoftJson.Serdes.Deserialize(json,settings)
```

## Encoding and conversion of F# types
 
 <a name="recommendations"></a>
### Recommended round-trippable constructs

`Newtonsoft.Json`, thanks to its broad usage thoughout .NET systems has well known (with some idiosyncratic quirks) behaviors for most common types one might use for C# DTOs.

Normal primitive F#/.NET such as `bool`, `byte`, `int16`, `int`, `int64`, `float32` (`Single`), `float` (`Double`), `decimal` work as expected.

The default settings for FsCodec applies Json.net's default behavior, whis is to render fields that have a `null` or `null`-equivalent value with the value `null`. This behavior can be overridden via `Settings(ignoreNulls = true)`, which will cause such JSON fields to be omitted.

The recommendations here apply particularly to Event Contracts - the data in your store will inevitably outlast your code, so being conservative in the complexity of ones's encoding scheme is paramount. Explicit is better than Implicit.

| Type kind | TL;DR | Notes | Example input | Example output |
| :--- | :--- | :--- | :--- | :--- |
| `'t[]` | As per C# | Don't forget to handle `null` | `[ 1; 2; 3]` | `[1,2,3]` |
| `DateTimeOffset` | Roundtrips cleanly | The default `Settings.Create` requests `RoundtripKind` | `DateTimeOffset.Now` | `"2019-09-04T20:30:37.272403+01:00"` |
| `Nullable<'t>` | As per C#; `Nullable()` -> `null`, `Nullable x` -> `x` | OOTB Json.net roundtrips cleanly. Works with `Settings.CreateDefault()`. Worth considering if your contract does not involve many `option` types | `Nullable 14` | `14` |
| `'t option` | `None` -> `null`, `Some x` -> `x` _with the converter `Settings.Create()` adds_ | OOTB Json.net does not roundtrip `option` types cleanly; `Settings.Create` and `NewtonsoftJson.Codec.Create` wire in an `OptionConverter` by default | `Some 14` | `14` | 
| `string` | As per C#; need to handle `null` | One can use a `string option` to map `null` and `Some null` to `None` | `"Abc"` | `"Abc"` |
| types with unit of measure | Works well (doesnt encode the unit) | Unit of measure tags are only known to the compiler; Json.net does not process the tags and treats it as the underlying primitive type | `54<g>` | `54` | 
| [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) tagged `string`, `DateTimeOffset` | Works well | [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) enables one to type-tag `string` and `DateTimeOffset` values using the units of measure compiler feature, which Json.net will render as if they were unadorned | `SkuId.parse "54-321"` | `"000-054-321"` |
| records | Just work | FYI records are not supported OOTB yet in `System.Text.Json` | `{\| a = 1; b = Some "x" \|}` | `"{"a":1,"b":"x"}"` |
| Nullary unions (Enum-like DU's without bodies) | Tag `type` with `TypeSafeEnumConverter` | Works well - guarantees a valid mapping, as opposed to using a `System.Enum` and `StringEnumConverter`, which can map invalid values and/or silently map to `0` etc | `State.NotFound` | `"NotFound"` |
| Discriminated Unions (where one or more cases has a body) | Tag `type` with `UnionConverter` | This format can be readily consumed in Java, JavaScript and Swift. Nonetheless, exhaust all other avenues before considering encoding a union in JSON. The `"case"` label id can be overridden. | `Decision.Accepted { result = "54" }` | `{"case": "Accepted","result":"54"}` |

### _Unsupported_ types and/or constructs

The mechanisms in the previous section have proven themselves sufficient for diverse systems inside and outside Jet. Here, we summarize some problematic constructs, with suggestions for alternate approaches to apply in preference.  

| Type kind | TL;DR | Example input | Example output | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `'t list` | __Don't use__; use `'t[]` | `[ 1; 2; 3]` | `[1,2,3]` | While the happy path works, `null` or  missing field maps to a `null` object rather than `[]` [which is completely wrong from an F# perspective] |
| `DateTime` | __Don't use__; use `DateTimeOffset` | | | Roundtripping can be messy, wrong or lossy; `DateTimeOffset` covers same use cases |
| `Guid` or [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) tagged `Guid` | __don't use__; wrap as a reference `type` and use a `JsonIsomorphism`, or represent as a tagged `string` | `Guid.NewGuid()` | `"ba7024c7-6795-413f-9f11-d3b7b1a1fe7a"` | If you wrap the value in a type, you can have that roundtrip with a specific format via a Converter implemented as a `JsonIsomorphism`. Alternately, represent in your contract as a [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) tagged-string. |
| maps/`Dictionary` etc. | avoid; prefer arrays | | | As per C#; not always the best option for many reasons, both on the producer and consumer side. Json.net has support for various maps with various idiosyncracies typically best covered by Stack Overflow, but often a list of records is clearer |
| tuples | __Don't use__; use records | `(1,2)` | `{"Item1":1,"Item2":2}` | While converters are out there, using tuples in contracts ofany kind is simply Not A Good Idea |

 <a name="JsonIsomorphism"></a>
## Custom converters using `JsonIsomorphism`
[`JsonIsomorphism`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Pickler.fs#L49) enables one to express the `Read`ing and `Write`ing of the JSON for a type in terms of another type. As alluded to above, rendering and parsing of `Guid` values can be expressed succinctly in this manner. The following Converter, when applied to a field, will render it without dashes in the rendered form:

```fsharp
type GuidConverter() =
    inherit JsonIsomorphism<Guid, string>()
    override __.Pickle g = g.ToString "N"
    override __.UnPickle g = Guid.Parse g
```

## `TypeSafeEnumConverter` basic usage

```fsharp
[<JsonConverter(typeof<TypeSafeEnumConverter>)>]
type Outcome = Joy | Pain | Misery

type Message = { name: string option; outcome: Outcome }

let value = { name = Some null; outcome = Joy}
ser value
// {"name":null,"outcome":"Joy"}

des<Message> """{"name":null,"outcome":"Joy"}"""
// val it : Message = {name = None; outcome = Joy;}
```

By design, we throw when a value is unknown. Often this is the correct design. If, and only if, your software can do something useful with catch-all case, see the technique in `OutcomeWithOther` (below)

```fsharp
des<Message> """{"name":null,"outcome":"Discomfort"}"""
// throws System.Collections.Generic.KeyNotFoundException: Could not find case 'Discomfort' for type 'FSI_0012+Outcome'
```

##  `TypeSafeEnum` fallback converters using `JsonIsomorphism`

While, in general, one wants to version contracts such that invalid values simply don't arise, in some cases you want to explicitly handle out of range values.
Here we implement a converter as a JsonIsomorphism to achieve such a mapping

```fsharp
[<JsonConverter(typeof<OutcomeWithCatchAllConverter>)>]
type OutcomeWithOther = Joy | Pain | Misery | Other
and OutcomeWithCatchAllConverter() =
    inherit JsonIsomorphism<OutcomeWithOther, string>()
    override __.Pickle v =
        TypeSafeEnum.toString v
    override __.UnPickle json =
        json
        |> TypeSafeEnum.tryParse<OutcomeWithOther>
        |> Option.defaultValue Other

type Message2 = { name: string option; outcome: OutcomeWithOther }
```

Because the `type` is tagged with a Converter attribute, valid values continue to be converted correctly:

```fsharp
let value2 = { name = Some null; outcome = Joy}
ser value2
// {"name":null,"outcome":"Joy"}

des<Message2> """{"name":null,"outcome":"Joy"}"""
// val it : Message = {name = None; outcome = Joy;}
```

More importantly, the formerly invalid value now gets mapped to our fallback value (`Other`) as intended.

```fsharp
des<Message2> """{"name":null,"outcome":"Discomfort"}"""
// val it : Message = {name = None; outcome = Other;}
```

 <a name="boxcodec"></a>
# Features: `FsCcodec.Box.Codec`

`FsCodec.Box.Codec` is a drop-in-equivalent for `FsCodec.NewtonsoftJson.Codec` with equivalent `.Create` overloads that encode as `ITimelineEvent<obj>` (as opposed to `ITimelineEvent<byte[]>`.

This is useful when storing events in a `MemoryStore` as it allows one to take the perf cost and ancillary yak shaving induced by round-tripping arbitrary event payloads to the concrete serialization format out of the picture when writing property based unit and integration tests.

NOTE this does not imply one should avoid testing this aspect; the opposite in fact -- one should apply the [Test Pyramid principles](https://martinfowler.com/articles/practical-test-pyramid.html):
- have a focused series of tests that validate that the various data representations in the event bodies are round-trippable
  a. in the chosen encoding format (i.e. UTF8 json)
  b. with the selected concrete json encoder (i.e. `Newtonsoft.Json` for now 🙁)
- integration tests can in general use `BoxEncoder` and `MemoryStore`

_You should absolutely have acceptance tests that apply the actual serialization encoding with the real store for a representative number of scenarios at the top of the pyramid_

## CONTRIBUTING

The intention is to keep this set of converters minimal and interoperable, e.g., many candidates are deliberately being excluded from this set; _its definitely a non-goal for this to become a compendium of every possible converter_. **So, especially in this repo, the bar for adding converters will be exceedingly high and hence any contribution should definitely be preceded by a discussion.**

Examples, tests and docs are welcomed with open arms.

General guidelines:

- Less [converters] is more - [has a converter _really_ proved itself broadly applicable](https://en.wikipedia.org/wiki/Rule_of_three_(computer_programming)) ?
- this is not the final complete set of converters; Json.net is purposefully extensible and limited only by your imagination, for better or worse. However such specific conversions are best kept within the app.
- If `Newtonsoft.Json` can or should be made to do something, it should - this library is for extensions that absolutely positively can't go into Json.net itself.

Please raise GitHub issues for any questions so others can benefit from the discussion.

# Building

```powershell
# verify the integrity of the repo wrt being able to build/pack/test
./dotnet build build.proj
```