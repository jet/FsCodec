# FsCodec [![Build Status](https://dev.azure.com/jet-opensource/opensource/_apis/build/status/jet.fscodec?branchName=master)](https://dev.azure.com/jet-opensource/opensource/_build/latest?definitionId=18?branchName=master) [![release](https://img.shields.io/github/release/jet/fscodec.svg)](https://github.com/jet/fscodec/releases) [![NuGet](https://img.shields.io/nuget/vpre/fscodec.svg?logo=nuget)](https://www.nuget.org/packages/fscodec/) [![license](https://img.shields.io/github/license/jet/fscodec.svg)](LICENSE)

Defines a minimal interface for serialization and deserialization of events for event-sourcing systems on .NET.
Provides implementation packages for writing simple yet versionable Event Contract definitions in F# using ubiquitous serializers.

Typically used in [applications](https://github.com/jet/dotnet-templates) leveraging [Equinox](https://github.com/jet/equinox) and/or [Propulsion](https://github.com/jet/propulsion), but also applicable to defining DTOs for other purposes such as Web APIs.

## Components

The components within this repository are delivered as multi-targeted Nuget packages supporting `netstandard2.1` (F# 4.5+) profiles.

- [![Codec NuGet](https://img.shields.io/nuget/v/FsCodec.svg)](https://www.nuget.org/packages/FsCodec/) `FsCodec` Defines interfaces with trivial implementation helpers.
  - No dependencies.
  - [`FsCodec.IEventCodec`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L19): defines a base interface for serializers.
  - [`FsCodec.Codec`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/Codec.fs#L5): enables plugging in a serializer and/or Union Encoder of your choice (typically this is used to supply a pair `encode` and `tryDecode` functions)
  - [`FsCodec.StreamName`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/StreamName.fs): strongly-typed wrapper for a Stream Name, together with factory functions and active patterns for parsing same
- [![Box Codec NuGet](https://img.shields.io/nuget/v/FsCodec.Box.svg)](https://www.nuget.org/packages/FsCodec.Box/) `FsCodec.Box`: See [`FsCodec.Box.Codec`](#boxcodec); `IEventCodec<obj>` implementation that provides a null encode/decode step in order to enable decoupling of serialization/deserialization concerns from the encoding aspect, typically used together with  [`Equinox.MemoryStore`](https://www.fuget.org/packages/Equinox.MemoryStore)
  - [depends](https://www.fuget.org/packages/FsCodec.Box) on `FsCodec`, `TypeShape >= 10`
- [![Newtonsoft.Json Codec NuGet](https://img.shields.io/nuget/v/FsCodec.NewtonsoftJson.svg)](https://www.nuget.org/packages/FsCodec.NewtonsoftJson/) `FsCodec.NewtonsoftJson`: As described in [a scheme for the serializing Events modelled as an F# Discriminated Union](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores/), enabled tagging of F# Discriminated Union cases in a versionable manner with low-dependencies using [TypeShape](https://github.com/eiriktsarpalis/TypeShape)'s [`UnionContractEncoder`](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores)
  - Uses the ubiquitous [`Newtonsoft.Json`](https://github.com/JamesNK/Newtonsoft.Json) library to serialize the event bodies.
  - Provides relevant Converters for common non-primitive types prevalent in F#
  - [depends](https://www.fuget.org/packages/FsCodec.NewtonsoftJson) on `FsCodec.Box`, `Newtonsoft.Json >= 11.0.2`, `Microsoft.IO.RecyclableMemoryStream >= 2.2.0`, `System.Buffers >= 4.5.1`
- [![System.Text.Json Codec NuGet](https://img.shields.io/nuget/v/FsCodec.SystemTextJson.svg)](https://www.nuget.org/packages/FsCodec.SystemTextJson/) `FsCodec.SystemTextJson`: See [#38](https://github.com/jet/FsCodec/pulls/38): drop in replacement that allows one to retarget from `Newtonsoft.Json` to the .NET Core >= v 3.0 default serializer: `System.Text.Json`, solely by changing the referenced namespace.
  - [depends](https://www.fuget.org/packages/FsCodec.SystemTextJson) on `FsCodec.Box`, `System.Text.Json >= 6.0.1`,

# Features: `FsCodec`

The purpose of the `FsCodec` package is to provide a minimal interface on which libraries such as Equinox and Propulsion can depend on in order that they can avoid forcing a specific serialization mechanism.

- [`FsCodec.IEventData`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L4) represents a single event and/or related metadata in raw form (i.e. still as a UTF8 string etc, not yet bound to a specific Event Type)
- [`FsCodec.ITimelineEvent`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L23) represents a single stored event and/or related metadata in raw form (i.e. still as a UTF8 string etc, not yet bound to a specific Event Type). Inherits `IEventData`, adding `Index` and `IsUnfold` in order to represent the position on the timeline that the event logically occupies.
- [`FsCodec.IEventCodec`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L31) presents `Encode : 'Context option * 'Event -> IEventData` and `TryDecode : ITimelineEvent -> 'Event option` methods that can be used in low level application code to generate `IEventData`s or decode `ITimelineEvent`s based on a contract defined by `'Union`
- [`FsCodec.Codec.Create`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/Codec.fs#L27) implements `IEventCodec` in terms of supplied `encode : 'Event -> string * byte array` and `tryDecode : string * byte array -> 'Event option` functions (other overloads are available for advanced cases)
- [`FsCodec.Core.EventData.Create`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L44) is a low level helper to create an `IEventData` directly for purposes such as tests etc.
- [`FsCodec.Core.TimelineEvent.Create`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L58) is a low level helper to create an `ITimelineEvent` directly for purposes such as tests etc.

# Features: `FsCodec.(Newtonsoft|SystemText)Json`

## Common API

The concrete implementations implement common type/member/function signatures and behavior that offer consistent behavior using either `Newtonsoft.Json` or `System.Text.Json`, emphasizing the following qualities:

- avoid non-straightforward encodings:
  - tuples don't magically become arrays
  - union bodies don't become arrays of mixed types like they do OOTB in JSON.NET (they become JSON Objects with named fields via `UnionEncoder`, or `string` values via `TypeSafeEnumConverter`)
- don't surprise .NET developers used to `JSON.NET` or `System.Text.Json`
- having an opinionated core set of behaviors, but don't conflict with the standard extensibility mechanisms afforded by the underlying serializer (one should be able to search up and apply answers from StackOverflow to questions regarding corner cases)
- maintain a minimal but well formed set of built in converters that are implemented per supported serializer - e.g., choices like not supporting F# `list` types (although `System.Text.Json` v `>= 6` does now provide such support)

## `Codec`

[`FsCodec.NewtonsoftJson/SystemTextJson.Codec`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Codec.fs) provides an implementation of `IEventCodec` as described in [a scheme for the serializing Events modelled as an F# Discriminated Union](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores/). This yields a clean yet versionable way of managing the roundtripping events based on a contract inferred from an F# Discriminated Union Type using `Newtonsoft.Json >= 11.0.2` / `System.Text.Json` to serialize the bodies.

## Converters: `Newtonsoft.Json.Converter`s / `System.Text.Json.Serialization.JsonConverter`s

### Explicit vs Implicit

While it's alluded to in the [recommendations](#recommendations), it's worth calling out that the converters in FsCodec (aside from obvious exceptions like the Option and Record ones) are intended to be used by tagging the type with a `JsonConverterAttribute` rather than by inclusion in the global converters list of the underlying serializer.

The key effect of this is that any non-trivial mapping will manifest as the application of the relevant attribute on the `type` or property in question. This also aligns well with the notion of cordoning off a `module Events` as described in [Equinox's `module Aggregate` documentation](
https://github.com/jet/equinox/blob/master/DOCUMENTATION.md#aggregate-module): `type`s that participate in an Event union are defined _and namespaced_ together (including any snapshot serialization contracts).

### This set might be all you need ...

While this may not seem like a sufficiently large set of converters for a large app, it should be mentioned that the serializer-neutral escape hatch represented by [`JsonIsomorphism`](#jsonisimorphism) has resulted in this set alone proving sufficient for two major subsystems of a large e-commerce software suite. See [recommendations](#recommendations) for further expansion on this (TL;DR it does mean ruling out using some type constructs directly in event and/or binding contracts and using [Anti Corruption Layer](https://docs.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer) and/or [event versioning](https://leanpub.com/esversioning) techniques.

### ... but don't forget `FSharp.SystemTextJson`

The role and intention of the converters in the box in `FsCodec.SystemTextJson` and/or `FsCodec.NewtonsoftJson` has always been to be minimal but provide escape hatches; short lived shims absolutely fit within this remit. For example, with regard to `System.Text.Json`, over time the shimming provided has been adjusted in alignment with the STJ implementation:
- `System.Text.Json` v4 did not even support F# records that are not marked `[<CLIMutable>]` out-of-the-box (it was similarly spartan wrt C# types, requiring a default constructor on `class`es). This library previously provided a shim for that.
- Version 5 added support for records.
- [Version 6 added support for F# `option`s, `list`s, `Set` and `Map` out of the box](https://github.com/dotnet/runtime/pull/55108). This enabled the [removal of the `JsonOptionConverter` that once lived here](https://github.com/jet/FsCodec/pull/68).
- There is an [open issue on the System.Text.Json repo wrt supporting F# Unions](https://github.com/dotnet/runtime/issues/55744). `UnionConverter` and `TypeSafeEnumConverter` provide for round-tripping of the most common usages of F# discriminated union types in two canonical formats that are known to have good versioning properties, rendering in formats that are known to be interoperable with other ecosystems, i.e. there are clean ways of generating and consuming in the same way in e.g. on the JVM and JavaScript.  

It's worth calling out explicitly that there are no plans to extend the representations `FsCodec.SystemTextJson` can handle in any significant way over time ([the advice for `FsCodec.NewtonsoftJson` has always been to avoid stuff outside of records, `option`s and `array`s](#recommendations)) - if you have specific exotic corner cases and determine you need something more specifically tailored, the Converters abstraction affords you ability to mix and match as necessary for specific applications.

_The single most complete set of `System.Text.Json` Converters is the [`FSharp.SystemTextJson`](https://github.com/Tarmil/FSharp.SystemTextJson) library; it provides a much broader, well tested set of converters with a broader remit than what FsCodec is trying to succinctly address as its sweet spot. [In general, there should be a smooth path to transition from using FsCodec to that as and when needed](https://github.com/jet/FsCodec/pull/69#issuecomment-1006532703)_
  
### Core converters

The respective concrete Codec packages include relevant `Converter`/`JsonConverter` in order to facilitate interoperable and versionable renderings:
  - [`TypeSafeEnumConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/TypeSafeEnumConverter.fs#L33) represents discriminated union (whose cases are all nullary), as a `string` in a trustworthy manner (`Newtonsoft.Json.Converters.StringEnumConverter` permits values outside the declared values) :pray: [@amjjd](https://github.com/amjjd)
  - [`UnionConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/UnionConverter.fs#L71) represents F# discriminated unions as a single JSON `object` with both the tag value and the body content as named fields directly within :pray: [@amjdd](https://github.com/amjjd); `System.Text.Json` reimplementation :pray: [@NickDarvey](https://github.com/NickDarvey)
  
    NOTE: The encoding differs from that provided by `Newtonsoft.Json`'s default converter: `Newtonsoft.Json.Converters.DiscriminatedUnionConverter`, which encodes the fields as an array without names, which has some pros, but many obvious cons
    
    NOTE `System.Text.Json`, even in v `6.0` does not support F# unions out of the box. It's not intended to extend the representations `FsCodec.SystemTextJson` can handle in any significant way over time - if you have specific requirements, the powerful and complete [`FSharp.SystemTextJson`](https://github.com/Tarmil/FSharp.SystemTextJson) library is likely your best option in this space.
    
### Custom converter base classes

- [`JsonIsomorphism`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Pickler.fs#L49) - allows one to cleanly map a type's internal representation to something that the underlying serializer and converters can already cleanly handle :pray: [@EirikTsarpalis](https://github.com/eiriktsarpalis)
- [`JsonPickler`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Pickler.fs#L15) - removes boilerplate from simple converters, used in implementation of `JsonIsomorphism` :pray: [@EirikTsarpalis](https://github.com/eiriktsarpalis) 

### `Newtonsoft.Json`-specific low level converters

  - [`OptionConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/OptionConverter.fs#L7) represents F#'s `Option<'t>` as a value or `null`; included in the standard `Options.Create` profile.
  - [`VerbatimUtf8JsonConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/VerbatimUtf8JsonConverter.fs#L7) captures/renders known valid UTF8 JSON data into a `byte array` without decomposing it into an object model (not typically relevant for application level code, used in `Equinox.Cosmos` versions prior to `3.0`).

### `System.Text.Json`-specific low level converters

- `UnionOrTypeSafeEnumConverterFactory`: Global converter that can apply `TypeSafeEnumConverter` to all Discriminated Unions that do not have cases with values, and `UnionConverter` to ones that have values. See [this `System.Text.Json` issue](https://github.com/dotnet/runtime/issues/55744) for background information as to the reasoning behind and tradeoffs involved in applying such a policy.  
- `RejectNullStringConverter`: Global converter that can reject `null` as a valid string value
 
## `FsCodec.NewtonsoftJson.Options`

[`FsCodec.NewtonsoftJson.Options`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Options.fs#L8) provides a clean syntax for building a `Newtonsoft.Json.JsonSerializerSettings` with which to define a serialization contract profile for interoperability purposes. Methods:
- `CreateDefault`: as per `Newtonsoft.Json` defaults with the following override:
  - `DateTimeZoneHandling = DateTimeZoneHandling.Utc` (default is `RoundtripKind`)
  - no custom `IContractResolver` (one is expected to use `camelCase` field names within records, for which this does not matter)
- `Create`: as `CreateDefault` with the following difference:
  - adds an `OptionConverter` (see _Converters_, above)
- `Default`: Default settings; same as calling `Create()` produces

## `FsCodec.SystemTextJson.Options`

[`FsCodec.SystemTextJson.Options`](https://github.com/jet/FsCodec/blob/stj/src/FsCodec.SystemTextJson/Options.fs#L8) provides a clean syntax for building a `System.Text.Json.Serialization.JsonSerializerOptions` as per `FsCodec.NewtonsoftJson.Options`, above. Methods:
- `CreateDefault`: configures the settings equivalent to `new JsonSerializerSettings()` or `JsonSerializerSettings.Default`, without overrides of any kind (see `Create`, below for the relevant differences)
- `Create`: as `CreateDefault` with the following difference:
  - By default, inhibits the HTML-safe escaping that `System.Text.Json` provides as a default by overriding `Encoder` with `System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping`
  - `(camelCase = true)`: opts into camel case conversion for `PascalCased` properties and `Dictionary` keys
  - `(autoTypeSafeEnumToJsonString = true)`: triggers usage of `TypeSafeEnumConverter` for any F# Discriminated Unions that only contain nullary cases. See [`AutoUnionTests.fs`](https://github.com/jet/FsCodec/blob/master/tests/FsCodec.SystemTextJson.Tests/AutoUnionTests.fs) for examples  
  - `(autoUnionToJsonObject = true)`: triggers usage of a `UnionConverter` to round-trip F# Discriminated Unions (with at least a single case that has a body) as JSON Object structures. See [`AutoUnionTests.fs`](https://github.com/jet/FsCodec/blob/master/tests/FsCodec.SystemTextJson.Tests/AutoUnionTests.fs) for examples
  - `(rejectNullStrings = true)`: triggers usage of [`RejectNullStringConverter`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.SystemTextJson/RejectNullStringConverter.fs) to reject `null` as a value for strings (`string option` is still supported).
- `Default`: Default settings; same as calling `Create()` produces (same intent as [`JsonSerializerOptions.Default`](https://github.com/dotnet/runtime/pull/61434)) 

## `Serdes`

[`FsCodec.SystemTextJson/NewtonsoftJson.Serdes`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.SystemTextJson/Serdes.fs#L7) provides light wrappers over `(JsonConvert|JsonSerializer).(Des|S)erialize(Object)?` based on an explicitly supplied serialization profile created by `Options.Create` (above), or using `Options.Default`. This enables one to smoothly switch between `System.Text.Json` vs `Newtonsoft.Json` serializers with minimal application code changes, while also ensuring consistent and correct options get applied in each case. Methods:
- `Serialize<T>`: serializes an object per its type using the settings defined in `Options.Create`
- `Deserialize<T>`: deserializes an object per its type using the settings defined in `Options.Create`
- `Options`: Allows one to access the `JsonSerializerSettings`/`JsonSerializerOptions` used by this instance.

# Usage of Converters with ASP.NET Core

ASP.NET Core's out-of-the-box behavior is to use `System.Text.Json`. One can explicitly opt to use the more ubiquitous `Newtonsoft.Json` via the `Microsoft.AspNetCore.Mvc.NewtonsoftJson` package's `AddNewtonsoftJson` by adjusting one's `.AddMvc)`.

If you follow the policies covered in the rest of the documentation here, your DTO types (and/or types in your `module Events` that you surface while you are scaffolding and/or hacking without an anti-corruption layer) will fall into one of two classifications:

1. Types that have an associated Converter explicitly annotated (e.g., DU types bear an associated `UnionConverter`, `TypeSafeEnumConverter` or `JsonIsomorphism`-based custom converter, custom types follow the conventions or define a `JsonIsomorphism`-based converter)
2. Types that require a global converter to be registered. _While it may seem that the second set is open-ended and potentially vast, experience teaches that you want to keep it minimal._. This boils down to:
  - records arrays and all other good choices for types Just Work already
  - `Nullable<MyType>`: Handled out of the box by both NSJ and STJ - requires no converters, provides excellent interop with other CLR languages. Would recommend.
  - `MyType option`: Covered by the global `OptionConverter` for Newtonsoft, handled intrinsically by `System.Text.Json` versions `>= 6` (see below for a clean way to add them to the default MVC view rendering configuration). Note that while this works well with ASP.NET Core, it may be problematic if you share contracts (yes, not saying you should) or rely on things like Swashbuckle which will need to be aware of the types when they reflect over them.

**The bottom line is that using exotic types in DTOs is something to think very hard about before descending into. The next sections are thus only relevant if you decide to add that extra complexity to your system...**

<a name="aspnetnsj"></a>
## ASP.NET Core with `Newtonsoft.Json`
Hence the following represents the recommended default policy:-

    /// Define a Serdes instance with a given policy somewhere (globally if you need to do explicit JSON generation) 
    let serdes = Serdes Options.Default

    services.AddMvc(fun options -> ...
    ).AddNewtonsoftJson(fun options ->
        // Borrow the Converters from the Options the Serdes is holding
        serdes.Options.Converters |> Seq.iter options.SerializerSettings.Converters.Add
        // OR, in the trivial case: Options.Default.Converters |> Seq.iter options.SerializerSettings.Converters.Add
    ) |> ignore	        

This adds all the converters used by the `serdes` serialization/deserialization policy (currently only `FsCodec.NewtonsoftJson.OptionConverter`) into the equivalent managed by ASP.NET.

<a name="aspnetstj"></a>
## ASP.NET Core with `System.Text.Json`

The equivalent for the native `System.Text.Json`, as v6, thanks [to the great work of the .NET team](https://github.com/dotnet/runtime/pull/55108), is presently a no-op.

The following illustrates how opt into [`autoTypeSafeEnumToJsonString` and/or `autoUnionToJsonObject` modes](https://github.com/jet/FsCodec/blob/master/tests/FsCodec.SystemTextJson.Tests/AutoUnionTests.fs) for the rendering of View Models by ASP.NET:

    // Default behavior throws an exception if you attempt to serialize a DU or TypeSafeEnum without an explicit JsonConverterAttribute
    // let serdes = FsCodec.SystemTextJson.Options.Default |> FsCodec.SystemTextJson.Serdes

    // If you use autoTypeSafeEnumToJsonString = true or autoUnionToJsonObject = true, serdes.Serialize / Deserialize applies the relevant converters
    let serdes = FsCodec.SystemTextJson.Options.Create(autoTypeSafeEnumToJsonString = true, autoUnionToJsonObject = true) |> FsCodec.SystemTextJson.Serdes

    services.AddMvc(fun options -> ...
    ).AddJsonOptions(fun options ->
        // Register the converters (actually a single JsonConverterFactory) from the Options passed to the `serdes` above
        serdes.Options.Converters |> Seq.iter options.JsonSerializerOptions.Converters.Add
    ) |> ignore

# Examples: `FsCodec.(Newtonsoft|SystemText)Json`

There's a test playground in [tests/FsCodec.NewtonsoftJson.Tests/Examples.fsx](tests/FsCodec.NewtonsoftJson.Tests/Examples.fsx). It's highly recommended to experiment with conversions using FSI. (Also, PRs adding examples are much appreciated...)

There's an equivalent of that for `FsCodec.SystemTextJson`: [tests/FsCodec.SystemTextJson.Tests/Examples.fsx](tests/FsCodec.SystemTextJson.Tests/Examples.fsx).

<a name="contracts"></a>
### Examples of using `Serdes` to define a contract

In a contract assembly used as a way to supply types as part of a client library, one way of encapsulating the conversion rules that need to be applied is as follows:

#### Simple contracts that tag all types or fields necessitating `Converter`s directly and only records and `option`s

The minimal code needed to define helpers to consistently roundtrip where one only uses simple types is to simply state" _Please use `FsCodec.NewtonsoftJson.Serdes` to encode/decode JSON payloads correctly. However, an alternate approach is to employ the convention of providing a pair of helper methods alongside the type :-

```fsharp
module Contract =
    type Item = { value : string option }
    /// Options to be used within this contract (opinionated ones compared to just using JsonSerializer.Serialize / Deserialize)
    let private serdes = FsCodec.SystemTextJson.Serdes FsCodec.SystemTextJson.Default
    // implies default settings from Options.Create(), i.e., includes UnsafeRelaxedJsonEscaping
    let serialize (x : Item) : string = serdes.Serialize x
    // implies default settings from Options.Create()
    let deserialize (json : string) = serdes.Deserialize json
```

#### More advanced case necessitating a custom converter

While it's hard to justify the wrapping in the previous case, this illustrates how one can employ the same basic layout yet override a setting (register a necessary custom `Newtonsoft.Json.Converter` type):

```fsharp
module Contract =
    type Item = { Value : string option; other : TypeThatRequiresMyCustomConverter }
    /// Options to be used within this contract - note the Pascal Cased Value property compared to the previous record definition
    let private serdes = FsCodec.SystemTextJson.Options.Create(converters = [| MyCustomConverter() |], camelCase = true) |> FsCodec.SystemTextJson.Serdes
    let serialize (x : Item) = serdes.Serialize x
    let deserialize (json : string) : Item = serdes.Deserialize json
```

## Encoding and conversion of F# types
 
 <a name="recommendations"></a>
### Recommended round-trippable constructs

`Newtonsoft.Json`, thanks to its broad usage throughout .NET systems has well known (with some idiosyncratic quirks) behaviors for most common types one might use for C# DTOs.

Normal primitive F#/.NET such as `bool`, `byte`, `int16`, `int`, `int64`, `float32` (`Single`), `float` (`Double`), `decimal` work as expected.

The default settings for FsCodec applies Json.NET's default behavior, which is to render fields that have a `null` or `null`-equivalent value with the value `null`. This behavior can be overridden via `Options(ignoreNulls = true)`, which will cause such JSON fields to be omitted.

The recommendations here apply particularly to Event Contracts - the data in your store will inevitably outlast your code, so being conservative in the complexity of one's encoding scheme is paramount. Explicit is better than Implicit.

| Type kind | TL;DR                                                                                                  | Notes                                                                                                                                                                                                                                                 | Example input | Example output |
| :--- |:-------------------------------------------------------------------------------------------------------|:------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------| :--- | :--- |
| `'t[]` | As per C#                                                                                              | Don't forget to handle `null`                                                                                                                                                                                                                         | `[ 1; 2; 3]` | `[1,2,3]` |
| `DateTimeOffset` | Roundtrips cleanly                                                                                     | The default `Options.Create` requests `RoundtripKind`                                                                                                                                                                                                | `DateTimeOffset.Now` | `"2019-09-04T20:30:37.272403+01:00"` |
| `Nullable<'t>` | As per C#; `Nullable()` -> `null`, `Nullable x` -> `x`                                                 | OOTB Json.NET and STJ roundtrip cleanly. Works with `Options.CreateDefault()`. Worth considering if your contract does not involve many `option` types                                                                                               | `Nullable 14` | `14` |
| `'t option` | `Some null`,`None` -> `null`, `Some x` -> `x` _with the converter `Options.Create()` adds_             | OOTB Json.NET does not roundtrip `option` types cleanly; `Options.Create` wires in an `OptionConverter` by default in `FsCodec.NewtonsoftJson`<br/> NOTE `Some null` will produce `null`, but deserialize as `None` - i.e., it's not round-trippable | `Some 14` | `14` | 
| `string` | As per C#; need to handle `null`. Can opt into rejecting null values with `(rejectNullStrings = true)` | One can use a `string option` to map `null` and `Some null` to `None`                                                                                                                                                                                 | `"Abc"` | `"Abc"` |
| types with unit of measure | Works well (doesnt encode the unit)                                                                    | Unit of measure tags are only known to the compiler; Json.NET does not process the tags and treats it as the underlying primitive type                                                                                                                | `54<g>` | `54` | 
| [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) tagged `string`, `DateTimeOffset` | Works well                                                                                             | [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) enables one to type-tag `string` and `DateTimeOffset` values using the units of measure compiler feature, which Json.NET will render as if they were unadorned                               | `SkuId.parse "54-321"` | `"000-054-321"` |
| records | Just work                                                                                              | For `System.Text.Json` v `4.x`, usage of `[<CLIMutable>]` or a custom `JsonRecordConverter` was once required                                                                                                                                         | `{\| a = 1; b = Some "x" \|}` | `"{"a":1,"b":"x"}"` |
| Nullary unions (Enum-like DU's without bodies) | Tag `type` with `TypeSafeEnumConverter`                                                                | Works well - guarantees a valid mapping, as opposed to using a `System.Enum` and `StringEnumConverter`, which can map invalid values and/or silently map to `0` etc                                                                                   | `State.NotFound` | `"NotFound"` |
| Discriminated Unions (where one or more cases has a body) | Tag `type` with `UnionConverter`                                                                       | This format can be readily consumed in Java, JavaScript and Swift. Nonetheless, exhaust all other avenues before considering encoding a union in JSON. The `"case"` label id can be overridden.                                                       | `Decision.Accepted { result = "54" }` | `{"case": "Accepted","result":"54"}` |

### _Unsupported_ types and/or constructs

The mechanisms in the previous section have proven themselves sufficient for diverse systems inside and outside Jet. Here, we summarize some problematic constructs, with suggestions for alternate approaches to apply in preference.  

| Type kind | TL;DR | Example input | Example output | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `'t list` | __Don't use__; use `'t[]` | `[ 1; 2; 3]` | `[1,2,3]` | While the happy path works, `null` or  missing field maps to a `null` object rather than `[]` [which is completely wrong from an F# perspective]. (`System.Text.Json` v `>= 6` does now handle them correctly, but arrays are still the preferred representation, and there is no plan at present to have `FsCodec.NewtonsoftJson` provide support for it in the name of interoperability) |
| `DateTime` | __Don't use__; use `DateTimeOffset` | | | Round-tripping can be messy, wrong or lossy; `DateTimeOffset` covers same use cases |
| `Guid` or [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) tagged `Guid` | __don't use__; wrap as a reference `type` and use a `JsonIsomorphism`, or represent as a tagged `string` | `Guid.NewGuid()` | `"ba7024c7-6795-413f-9f11-d3b7b1a1fe7a"` | If you wrap the value in a type, you can have that roundtrip with a specific format via a Converter implemented as a `JsonIsomorphism`. Alternately, represent in your contract as a [`FSharp.UMX`](https://github.com/fsprojects/FSharp.UMX) tagged-string. |
| maps/`Dictionary` etc. | avoid; prefer arrays | | | As per C#; not always the best option for many reasons, both on the producer and consumer side. Json.NET has support for various maps with various idiosyncracies typically best covered by Stack Overflow, but often a list of records is clearer<br/>For `System.Text.Json`, use an `IDictionary<'K, 'V>` or `Dictionary<'K, 'V>` |
| tuples | __Don't use__; use records | `(1,2)` | `{"Item1":1,"Item2":2}` | While converters are out there, using tuples in contracts of any kind is simply Not A Good Idea |

 <a name="JsonIsomorphism"></a>
## Custom converters using `JsonIsomorphism`
[`JsonIsomorphism`](https://github.com/jet/FsCodec/blob/master/src/FsCodec.NewtonsoftJson/Pickler.fs#L49) enables one to express the `Read`ing and `Write`ing of the JSON for a type in terms of another type. As alluded to above, rendering and parsing of `Guid` values can be expressed succinctly in this manner. The following Converter, when applied to a field, will render it without dashes in the rendered form:

```fsharp
type GuidConverter() =
    inherit JsonIsomorphism<Guid, string>()
    override _.Pickle g = g.ToString "N"
    override _.UnPickle g = Guid.Parse g
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
    override _.Pickle v =
        TypeSafeEnum.toString v
    override _.UnPickle json =
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

<a name="IEventCodec"></a>
# Features: `IEventCodec`

_See [tests/FsCodec.NewtonsoftJson.Tests/Examples.fsx](tests/FsCodec.NewtonsoftJson.Tests/Examples.fsx) for a worked example suitable for playing with in F# interactive based on the following tutorial_

<a name="IEventCodec"></a>
## [`FsCodec.IEventCodec`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L31)

```fsharp
/// Defines a contract interpreter that encodes and/or decodes events representing the known set of events borne by a stream category
type IEventCodec<'Event, 'Format, 'Context> =
    /// Encodes a <c>'Event</c> instance into a <c>'Format</c> representation
    abstract Encode : context: 'Context * value: 'Event -> IEventData<'Format>
    /// Decodes a formatted representation into a <c>'Event<c> instance. Does not throw exception on undefined <c>EventType</c>s
    abstract TryDecode : encoded: ITimelineEvent<'Format> -> 'Event voption
```

`IEventCodec` represents a standard contract for the encoding and decoding of events used in event sourcing and event based notification scenarios:
- encoding pending/tentative "source of truth" events ('Facts') in Event Sourced systems (including encoding ones on the way to the store that are not yet accepted on a _Timeline_) - (see [`FsCodec.IEventData`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L4))
- decoding event records from an Event Store in a [programming model](https://github.com/jet/equinox/blob/master/DOCUMENTATION.md#programming-model), which involves mapping from the source event together with contextual information (see [`FsCodec.ITimelineEvent`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L23)) such as:
  - The _event type_, which signifies the event that has taken place (if you're familiar with ADTs, this maps to the Discriminator in a Discriminated Union)
  - the core _event data_ (often encoded as JSON, protobufs etc), the schema for which typically varies by _event type_
  - _event metadata_ (contextual information optionally stored alongside the event)
  - the `Timestamp` at which the event was generated
  - the `Index` representing the position of this event within the sequence of events on the timeline represented by the stream from which one is hydrating the event
  - Correlation/causation identifiers for the activity that triggered the event
- routing and filtering of events for the purpose of managing projections, notification or reactions to events. Such events may either emanate directly from an Event Store's timeline as in the preceding cases, or represent versioned [summary events](http://verraes.net/2019/05/patterns-for-decoupling-distsys-summary-event/)

<a name="IEventData"></a>
## [`FsCodec.IEventData`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L4)

Pending and timeline Events share the following common contract:

```fsharp
/// Common form for either a Domain Event or an Unfolded Event, without any context regarding its place in the timeline of events
type IEventData<'Format> =
    /// The Event Type, used to drive deserialization
    abstract member EventType : string
    /// Event body, as UTF-8 encoded JSON ready to be injected into the Store
    abstract member Data : 'Format
    /// Optional metadata (null, or same as Data, not written if missing)
    abstract member Meta : 'Format
    /// Application-generated identifier used to drive idempotent writes based on deterministic Ids and/or Request Id
    abstract member EventId : System.Guid
    /// The Correlation Id associated with the flow that generated this event. Can be `null`
    abstract member CorrelationId : string
    /// The Causation Id associated with the flow that generated this event. Can be `null`
    abstract member CausationId : string
    /// The Event's Creation Time (as defined by the writer, i.e. in a mirror, this is intended to reflect the original time)
    /// <remarks>- For EventStore, this value is not honored when writing; the server applies an authoritative timestamp when accepting the write.</remarks>
    abstract member Timestamp : System.DateTimeOffset
```

<a name="ITimelineEvent"></a>
## [`FsCodec.ITimelineEvent`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/FsCodec.fs#L23)

Events from a versioned feed and/or being loaded from an Event Store bring additional context beyond the base information in [IEventData](#IEventData)

```fsharp
/// Represents a Domain Event or Unfold, together with it's 0-based <c>Index</c> in the event sequence
type ITimelineEvent<'Format> =
    inherit IEventData<'Format>
    /// The 0-based index into the event sequence of this Event
    abstract member Index : int64
    /// Application-supplied context related to the origin of this event
    abstract member Context : obj
    /// Indicates this is not a true Domain Event, but actually an Unfolded Event based on the State inferred from the Events up to and including that at <c>Index</c>
    abstract member IsUnfold : bool
```

## Contracts for parsing / routing event records

See [a scheme for the serializing Events modelled as an F# Discriminated Union](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores/) for details of the representation scheme used for the events when using `FsCodec.NewtonsoftJson.Codec.Create`. We'll use the following example contract for the illustration:

```fsharp
module Events =

    // By convention, each contract defines a 'category' used as the first part of the stream name (e.g. `"Favorites-ClientA"`)
    let [<Literal>] Category = "Favorites"

    type Added = { item : string }
    type Removed = { name: string }
    type Event =
        | Added of Added
        | Removed of Removed
        interface TypeShape.UnionContract.IUnionContract

    let codec = FsCodec.NewtonsoftJson.Codec.Create<Event>()

    // See "logging unmatched events" later in this section for information about this wrapping using an EventCodec helper
    let [<return: Struct>] (|TryDecode|_|) stream = EventCodec.tryDecode codec Serilog.Log.Logger stream
```

<a name="umx"></a>
## Strongly typed stream ids using [FSharp.UMX](https://github.com/fsprojects/FSharp.UMX)

The example event stream contract above uses a `ClientId` type which (while being a string at heart) represents the identifier for a specific entity. We use the `FSharp.UMX` library that leans on the F# units of measure feature to tag the strings such that they can't be confused with other identifiers - think of it as a type alias on steroids.

```fsharp
open FSharp.UMX

type [<Measure>] clientId
type ClientId = string<clientId>
module ClientId =
    let parse (str : string) : ClientId = % str
    let toString (value : ClientId) : string = % value
    let (|Parse|) = ClientId.parse
```

<a name="streamname"></a>
## Stream naming conventions

The de-facto standard Event Store [EventStore.org](https://eventstore.org) and its documentation codifies the following convention for the naming of streams:-

    {Category}-{StreamId}

Where:

- `{Category}` represents a high level contract/grouping; all stream names starting with `{Category}-` are the same category. Must not contain any `-` characters.
- `-` (hyphen/minus) represents the by-convention standard separator between category and identifier
- `{StreamId}` represents the identity of the Aggregate / Aggregate Root instance for which we're storing the events within this stream. The `_` character is used to separate composite ids; see [the code](https://github.com/jet/FsCodec/blob/master/src/FsCodec/StreamName.fs).

The `StreamName` module will reject invalid values by throwing exceptions when fields have erroneously embedded `-` or `_` values.

It's important to apply some consideration in mapping from values in your domain to a `StreamName`. Domain values might include characters such as `-` (which may cause issues with EventStoreDb's [`$by_category`](https://developers.eventstore.com/server/5.0.8/server/projections/system-projections.html#by-category) projections) and/or arbitrary Unicode chars (which may not work well for other backing stores e.g. if CosmosDB were to restrict the character set that may be used for a Partition Key). You'll also want to ensure it's appropriately cleansed, validated and/or canonicalized to cover SQL Injection and/or XSS concerns. In short, no, you shouldn't just stuff an email address into the `{Identifier}` portion.

[`FsCodec.StreamName`](https://github.com/jet/FsCodec/blob/master/src/FsCodec/StreamName.fs): presents the following set of helpers that are useful for splitting and filtering Stream Names by Categories and/or Identifiers. Similar helpers would of course make sense in other languages e.g. C#:

```fsharp
// Type aliases for a type-tagged `string`
type [<Measure>] streamName
type StreamName = string<streamName>

module StreamName =

    (* Creators: Building from constituent parts
       Guards against malformed category, streamId and/or streamIdElements with exceptions *)

    // Recommended way to specify a stream identifier; a category identifier and a streamId representing the aggregate's identity
    // category is separated from id by `-`
    let create (category : string) streamId : StreamName = ...

    // Composes a StreamName from a category and > 1 name elements.
    // category is separated from the streamId by '-'; elements are separated from each other by '_'
    let compose (category : string) (streamIdElements : string seq) : StreamName = ...

    // Validates and maps a trusted Stream Name consisting of a Category and an Id separated by a '-` (dash)
    // Throws <code>InvalidArgumentException</code> if it does not adhere to that form
    let parse (rawStreamName : string) : StreamName = ...

    (* Rendering *)

    let toString (streamName : StreamName) : string = UMX.untag streamName

    (* Parsing: Raw Stream name Validation functions/pattern that handle malformed cases without throwing *)

    // Attempts to split a Stream Name in the form {category}-{streamId} into its two elements.
    // The {streamId} segment is permitted to include embedded '-' (dash) characters
    let trySplitCategoryAndId (rawStreamName : string) : (string * string) voption = ...

    // Attempts to split a Stream Name in the form {category}-{streamId} into its two elements.
    // The {streamId} segment is permitted to include embedded '-' (dash) characters
    // Yields <code>NotCategorized</code> if it does not adhere to that form.
    let (|Categorized|NotCategorized|) (rawStreamName : string) : Choice<struct (string*string), unit> = ...

    (* Splitting: functions/Active patterns for (i.e. generated via `parse`, `create` or `compose`) well-formed Stream Names
       Will throw if presented with malformed strings [generated via alternate means] *)

    /// Extracts the category portion of the StreamName
    let category (x : StreamName) : string = ...
    let (|Category|) = category
    
    // Splits a well-formed Stream Name of the form {category}-{streamId} into its two elements.
    // Throws <code>InvalidArgumentException</code> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).
    // <remarks>Inverse of <code>create</code>
    let splitCategoryAndStreamId (streamName : StreamName) : struct (string * string) = ...
    let (|CategoryAndId|) : StreamName -> struct (string * string) = splitCategoryAndId

    // Splits a `_`-separated set of id elements (as formed by `compose`) into its (one or more) constituent elements.
    // <remarks>Inverse of what <code>compose</code> does to the streamIdElements
    let (|IdElements|) (streamId : string) : string[] = ...

    // Splits a well-formed Stream Name of the form {category}-{id1}_{id2}_{idN} into a pair of category and ids
    // Throws <code>InvalidArgumentException</code> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).
    // <remarks>Inverse of <code>create</code>
    let splitCategoryAndIds (streamName : StreamName) : struct (string * string)[] = ...
    let (|CategoryAndIds|) : StreamName -> struct (string * string[]) = splitCategoryAndIds
```

## Decoding events

Given the following example events from across streams:

```fsharp
let utf8 (s : string) = System.Text.Encoding.UTF8.GetBytes(s)
let events = [
    StreamName.parse "Favorites-ClientA",    Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "a" }""")
    StreamName.parse "Favorites-ClientB",    Core.TimelineEvent.Create(0L, "Added",     utf8 """{ "item": "b" }""")
    StreamName.parse "Favorites-ClientA",    Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "b" }""")
    StreamName.parse "Favorites-ClientB",    Core.TimelineEvent.Create(1L, "Added",     utf8 """{ "item": "a" }""")
    StreamName.parse "Favorites-ClientB",    Core.TimelineEvent.Create(2L, "Removed",   utf8 """{ "item": "a" }""")
    StreamName.create "Favorites" "ClientB", Core.TimelineEvent.Create(3L, "Exported",  utf8 """{ "count": 2 }""")
    StreamName.parse "Misc-x",               Core.TimelineEvent.Create(0L, "Dummy",     utf8 """{ "item": "z" }""")
]
```

and the helpers defined above, we can route and/or filter them as follows:

```fsharp
let runCodec () =
    for stream, event in events do
        match struct (stream, event) with
        | StreamName.Category (Events.Category, ClientId.Parse id), (Events.Decode stream e) ->
            printfn "Client %s, event %A" (ClientId.toString id) e
        | StreamName.Category (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType
        | StreamName.Other streamName, _e ->
            failwithf "Invalid Stream Name: %s" streamName
```

invoking `runCodec ()` yields:

```
Client ClientA, event Added {item = "a";}
Client ClientB, event Added {item = "b";}
Client ClientA, event Added {item = "b";}
Client ClientB, event Added {item = "a";}
Client ClientB, event Removed {name = null;}
Unhandled Event: Category Favorites, Id ClientB, Index 3, Event: "Exported"
Unhandled Event: Category Misc, Id x, Index 0, Event: "Dummy"
```
There are two events that we were not able to decode, for varying reasons:

1. `"Misc-x", FsCodec.Core.TimelineEvent.Create(0L, "Dummy",   utf8 """{ "item": "z" }""")` represents an Event that happens to pass through our event processor that we don't want to decode and/or handle - we don't need to define a contract type for
2. `"Favorites" "ClientB", FsCodec.Core.TimelineEvent.Create(3L, "Exported",  utf8 """{ "count": 2 }""")` represents an Event that has recently been defined in the source system, but not yet handled by the processor. In the event of such an unclassified event occurring within a stream contract we want to know when we're not handling a given event. That's trapped above and logged as `Unhandled Event: Category Favorites, Id ClientB, Index 3, Event: "Exported"`.

_Note however, that we don't have a clean way to trap the data and log it. See [Logging unmatched events](#logging-unmatched-events) for an example of how one might log such unmatched events_

### Handling introduction of new fields in JSON

The below example demonstrates the addition of a `CartId` property in a newer version of `CreateCart`. It's worth noting that
deserializing `CartV1.CreateCart` into `CartV2.CreateCart` requires `CartId` to be an optional property or the property will
deserialize into `null` which is an invalid state for the `CartV2.CreateCart` record in F# (F# `type`s are assumed to never be `null`).

```
module CartV1 =
    type CreateCart = { name: string }

    type Events =
        | Created of CreateCart
        interface IUnionContract

module CartV2 =
    type CreateCart = { name: string; cartId: CartId option }
    type Events =
        | Created of CreateCart
        interface IUnionContract
```

FsCodec.SystemTextJson looks to provide an analogous mechanism. In general, FsCodec is seeking to provide a pragmatic middle way of
using NewtonsoftJson or SystemTextJson in F# without completely changing what one might expect to happen when using JSON.NET in
order to provide an F# only experience.

The aim is to provide helpers to smooth the way for using reflection based serialization in a way that would not surprise
people coming from a C# background and/or in mixed C#/F# codebases.

## Adding Matchers to the Event Contract

We can clarify the consuming code a little by adding further helper Active Patterns alongside the event contract :-

```fsharp
module Events =

    // ... (as above)

    // Pattern to determine whether a given {category}-{streamId} StreamName represents the stream associated with this Aggregate
    // Yields a strongly typed id from the streamId if the Category does match
    let [<return: Struct>] (|StreamName|_|) = function
        | FsCodec.StreamName.CategoryAndId (Category, ClientId.Parse clientId) -> Some clientId
        | _ -> None

    // ... (as above)

    // Yields decoded events and relevant strongly typed ids if the category of the Stream Name is correct
    let [<return: Struct>] (|Match|_|) (streamName, event) =
        match struct (streamName, event) with
        | MatchesCategory clientId, TryDecode streamName e -> ValueSome struct (clientId, e)
        | _ -> ValueNone
        
    module ValueOption = let toOption = function ValueSome x -> Some x | ValueNone -> None
    let (|Decode|) stream = Seq.choose ((|TryDecode|_|) stream >> ValueOption.toOption)
    let [<return: Struct>] (|Parse|_|) struct (streamName, span) =
        match streamName, span with
        | Events.MatchesCategory clientId, Decode streamName es -> ValueSome struct (clientId, es)
        | _ -> ValueNone
```

That boxes off the complex pattern matching close to the contract itself, and lets us match on the events in a handler as follows:

```fsharp
let runCodecCleaner () =
    for stream, event in events do
        match struct (stream, event) with
        | Events.Match (clientId, event) ->
            printfn "Client %s, event %A" (ClientId.toString clientId) event
        | StreamName.CategoryAndId (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType
```

## Logging unmatched events

The following helper (which uses the [`Serilog`](https://github.com/serilog/serilog) library), can be used to selectively layer on some logging when run with logging upped to `Debug` level:

```fsharp
module EventCodec =

    // Uses the supplied codec to decode the supplied event record `x` (iff at LogEventLevel.Debug, detail fails to `log` citing the `stream` and content)
    let tryDecode (codec : FsCodec.IEventCodec<_,_,_>) (log : Serilog.ILogger) streamName (x : FsCodec.ITimelineEvent<ReadOnlyMemory<byte>>) =
        match codec.TryDecode x with
        | None ->
            if log.IsEnabled Serilog.Events.LogEventLevel.Debug then
                log.ForContext("event", System.Text.Encoding.UTF8.GetString(x.Data), true)
                    .Debug("Codec {type} Could not decode {eventType} in {stream}", codec.GetType().FullName, x.EventType, streamName)
            None
        | x -> x
```

Normally, the `log.IsEnabled` call instantly rules out any need for logging. We can activate this inert logging hook by reconfiguring the logging as follows:

```fsharp
// Switch on debug logging to get detailed information about events that don't match (which has no singificant perf cost when not switched on)
open Serilog
open Serilog.Events
let outputTemplate = "{Message} {Properties}{NewLine}"
Serilog.Log.Logger <-
    LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console(LogEventLevel.Debug, outputTemplate=outputTemplate)
        .CreateLogger()
runCodec ()
```

This adds the following additional output when triggering `runCodec ()`:-

    Codec "<Snipped>" Could not decode "Exported" in "Favorites-ClientB" {event="{ \"count\": 2 }"}

## Decoding contextual information

Events arriving from a store (e.g. Equinox etc) or source (e.g. Propulsion) bear contextual information.

Where relevant, a decoding process may want to extract such context alongside mapping the base information.

For example, we may wish to log (or process as part of our domain logic) metadata accompanying an event, while still leaning on the TypeShape UnionEncoder and FsCodec to automate the decoding.

A clean way to wrap such a set of transitions is as follows:

```fsharp
module Reactions =

    type Event = (struct (int64 * DateTimeOffset * Events.Event))
    let codec =
        let up (raw : FsCodec.ITimelineEvent<ReadOnlyMemory<byte>>, contract : Events.Event) : Event = (raw.Index, raw.Timestamp, contract)
        let down ((_index, timestamp, event) : Event) = struct (event, ValueNone, ValueSome timestamp)
        FsCodec.NewtonsoftJson.Codec.Create(up, down)
        
    let [<return: Struct>] (|TryDecode|_|) stream event : Event option = EventCodec.tryDecode codec Serilog.Log.Logger stream event
    let [<return: Struct>] (|Match|_|) (streamName, event) =
        match streamName, event with
        | Events.MatchesCategory clientId, TryDecode streamName event -> Some (clientId, event)
        | _ -> None
        
    module ValueOption = let toOption = function ValueSome x -> Some x | ValueNone -> None
    let (|Decode|) stream = Seq.choose ((|TryDecode|_|) stream >> ValueOption.toOption)
    let [<return: Struct>] (|Parse|_|) struct (streamName, span) =
        match streamName, span with
        | Events.MatchesCategory clientId, Decode streamName es -> ValueSome struct (clientId, es)
        | _ -> ValueNone        
```

This allows us to tweak the `runCodec` above as follows to also surface additional contextual information:

```fsharp
let runWithContext () =
    for struct (stream, event) in events do
        match stream, event with
        | Reactions.Match (clientId, (index, ts, e)) ->
            printfn "Client %s index %d time %O event %A" (ClientId.toString clientId) index (ts.ToString "u") e
        | StreamName.CategoryAndId (cat, id), e ->
            printfn "Unhandled Event: Category %s, Id %s, Index %d, Event: %A " cat id e.Index e.EventType
```

which yields the following output:

    Client ClientA index 0 time 2020-01-13 09:44:37Z event Added {item = "a";}
    Client ClientB index 0 time 2020-01-13 09:44:37Z event Added {item = "b";}
    Client ClientA index 1 time 2020-01-13 09:44:37Z event Added {item = "b";}
    Client ClientB index 1 time 2020-01-13 09:44:37Z event Added {item = "a";}
    Client ClientB index 2 time 2020-01-13 09:44:37Z event Removed {name = null;}
    Codec "<Snipped>" Could not decode "Exported" in "Favorites-ClientB" {event="{ \"count\": 2 }"}
    Unhandled Event: Category Favorites, Id ClientB, Index 3, Event: "Exported"
    Unhandled Event: Category Misc, Id x, Index 0, Event: "Dummy"

<a name="boxcodec"></a>
# Features: `FsCodec.Box.Codec`

`FsCodec.Box.Codec` is a drop-in-equivalent for `FsCodec.(Newtonsoft|SystemText)Json.Codec` with equivalent `.Create` overloads that encode as `ITimelineEvent<obj>` (as opposed to `ITimelineEvent<ReadOnlyMemory<byte>>` / `ITimelineEvent<JsonElement>`).

This is useful when storing events in a `MemoryStore` as it allows one to take the perf cost and ancillary yak shaving induced by round-tripping arbitrary event payloads to the concrete serialization format out of the picture when writing property based unit and integration tests.

NOTE this does not imply one should avoid testing this aspect; the opposite in fact -- one should apply the [Test Pyramid principles](https://martinfowler.com/articles/practical-test-pyramid.html):
- have a focused series of tests that validate that the various data representations in the event bodies are round-trippable
  a. in the chosen encoding format (i.e. UTF8 JSON)
  b. with the selected concrete json encoder (i.e. `Newtonsoft.Json` for now 🙁)
- integration tests can in general use `BoxEncoder` and `MemoryStore`

_You should absolutely have acceptance tests that apply the actual serialization encoding with the real store for a representative number of scenarios at the top of the pyramid_

<a name="articles"></a>
## RELATED ARTICLES / BLOG POSTS etc

- [Contracts for Event Sourced Systems with FsCodec](https://asti.dynz.net/post/fscodec/) by [@deviousasti](https://github.com/deviousasti)
- [A Contract Pattern for Schemaless DataStores](https://eiriktsarpalis.wordpress.com/2018/10/30/a-contract-pattern-for-schemaless-datastores/) by [Eirik Tsarpalis](https://github.com/eiriktsarpalis)

## CONTRIBUTING

The intention is to keep this set of converters minimal and interoperable, e.g., many candidates are deliberately being excluded from this set; _its definitely a non-goal for this to become a compendium of every possible converter_. **So, especially in this repo, the bar for adding converters will be exceedingly high and hence any contribution should definitely be preceded by a discussion.**

Examples, tests and docs are welcomed with open arms.

General guidelines:

- Less [converters] is more - [has a converter _really_ proved itself broadly applicable](https://en.wikipedia.org/wiki/Rule_of_three_(computer_programming)) ?
- this is not the final complete set of converters; Json.NET and System.Text.Json are purposefully extensible and limited only by your imagination, for better or worse. However such specific conversions are best kept within the app.
- If the upstream library (`Newtonsoft.Json`, `System.Text.Json`) can or should be made to do something, it should. Also for `System.Text.Json`, if it's an F#-specific, the powerful and complete [`FSharp.SystemTextJson`](https://github.com/Tarmil/FSharp.SystemTextJson) library may be much more aligned.

Please raise GitHub issues for any questions so others can benefit from the discussion.

# Building

```powershell
# verify the integrity of the repo wrt being able to build/pack/test
./dotnet build build.proj
```
