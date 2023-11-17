# Changelog

The repo is versioned based on [SemVer 2.0](https://semver.org/spec/v2.0.0.html) using the tiny-but-mighty [MinVer](https://github.com/adamralph/minver) from [@adamralph](https://github.com/adamralph). [See here](https://github.com/adamralph/minver#how-it-works) for more information on how it works.

All notable changes to this project will be documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

The `Unreleased` section name is replaced by the expected version of next release. A stable version's log contains all changes between that version and the previous stable version (can duplicate the prereleases logs).

## [Unreleased]

### Added
### Changed
### Removed
### Fixed

<a name="3.0.0-rc.14"></a>
## [3.0.0-rc.14] - 2023-11-17

### Changed

- `IEventCodec.TryDecode`: Rename to `Decode` (to align with the primary assumption of a `Try` prefix per BCL conventions: It won't throw, no matter what!) [#107](https://github.com/jet/FsCodec/pull/107) :pray: [@nordfjord](https://github.com/nordfjord)

<a name="3.0.0-rc.13"></a>
## [3.0.0-rc.13] - 2023-9-11

### Changed

- Rename `Deflate.EncodeTryDeflate` -> `Compression.EncodeTryCompress` [#105](https://github.com/jet/FsCodec/pull/105) :pray: [@nordfjord](https://github.com/nordfjord)
- `Compression`: Switched encoding to use Brotli Compression (Deflate compressed content can still be inflated, but will no longer be generated) [#105](https://github.com/jet/FsCodec/pull/105)

<a name="3.0.0-rc.12"></a>
## [3.0.0-rc.12] - 2023-9-4

### Added

- `Union`: Exposed internal type, featuring `isUnion`, `isNullary`, and `caseName` (that's not tied to `TypeSafeEnum`), [#102](https://github.com/jet/FsCodec/pull/102)

### Changed

- `TypeSafeEnum`: Merged two impls from `SystemTextJson` and `NewtonsoftJson` [#102](https://github.com/jet/FsCodec/pull/102)
- `StreamId.dec*`: Changed to take `struct` tuples [#103](https://github.com/jet/FsCodec/pull/103)

### Removed

- `(NewtonsoftJson|SystemTextJson).TypeSafeEnum`: Merged/moved to `FsCodec.TypeSafeEnum` [#102](https://github.com/jet/FsCodec/pull/102)

### Fixed

- `Core.Codec`: Changed default timestamp to `DateTime.UtcNow` as per docs (was: `DateTime.Now`) [#104](https://github.com/jet/FsCodec/pull/104)

<a name="3.0.0-rc.11"></a>
## [3.0.0-rc.11] - 2023-8-25

### Added

- `StreamId`: type-tagged wrapper for the streamId portion of a `StreamName` [#100](https://github.com/jet/FsCodec/pull/100)
- `StreamName.Split`: Splits a StreamName into its `{category}` and `{streamId}` portions, using `StreamId` for the latter. Replaces `CategoryAndId` [#100](https://github.com/jet/FsCodec/pull/100)
- `StreamName.tryFind`: Helper to implement `Stream.tryDecode` / `Reactions.For` pattern (to implement validation of StreamId format when parsing `StreamName`s). (See README) [#100](https://github.com/jet/FsCodec/pull/100)
- `StreamName.Category`: covers aspects of `StreamName` pertaining to the `{category}` portion (mainly moved from `StreamName`.* equivalents; see Changed) [#100](https://github.com/jet/FsCodec/pull/100)
- `TypeSafeEnum.tryParseF/parseF`: parameterizes matching of the Union Case name (to enable e.g. case insensitive matching) [#101](https://github.com/jet/FsCodec/pull/101)

### Changed

- `StreamName`: breaking changes to reflect introduction of strongly typed `StreamId` [#100](https://github.com/jet/FsCodec/pull/100)
- `StreamName`: renames: `trySplitCategoryAndStreamId` -> `Internal.tryParse`; `splitCategoryAndStreamId` -> `split`; `CategoryAndId` -> `Split`; `Categorized|NotCategorized`-> `Internal`.*; `category`->`Category.ofStreamName`, `IdElements` -> `StreamId.Parse` [#100](https://github.com/jet/FsCodec/pull/100)
- `SystemTextJson.UnionOrTypeSafeEnumConverterFactory`: Allow specific converters to override global policy [#101](https://github.com/jet/FsCodec/pull/101)

### Removed

- `StreamName.CategoryAndIds`: See new `StreamId`, `StreamId.Elements` [#100](https://github.com/jet/FsCodec/pull/100)

### Fixed

<a name="3.0.0-rc.10"></a>
## [3.0.0-rc.10] - 2023-6-05

### Added

- `Serdes`: `SerializeToUtf8`, associated `Deserialize` overloads [#94](https://github.com/jet/FsCodec/pull/94)
- `Serdes`: `Default` [#95](https://github.com/jet/FsCodec/pull/95)

### Changed

- `NewtonsoftJson.TypeSafeEnum`: Sync with `SystemTextJson.TypeSafeEnum` [#91](https://github.com/jet/FsCodec/pull/91)
-  replace all `FSharpFunc` usage with `Func` [#92](https://github.com/jet/FsCodec/pull/92) [#105](https://github.com/jet/FsCodec/pull/105)

<a name="3.0.0-rc.9"></a>
## [3.0.0-rc.9] - 2022-11-30

### Added

- `SystemTextJson.Options.Create`: Add `rejectNullStrings` option, which wires in a `RejectNullStringConverter` [#87](https://github.com/jet/FsCodec/pull/87) :pray: [@nordfjord](https://github.com/nordfjord)
- `FsCodec.Core.TimelineEvent.Create`: Add overload to create an `ITimelineEvent` given the additional properties to go with a set of baseline data from an `IEventData`

<a name="3.0.0-rc.8"></a>
## [3.0.0-rc.8] - 2022-11-16

### Added

- `StreamName.Category` + `category`: Extracts the category portion of a streamName [#85](https://github.com/jet/FsCodec/pull/85)

### Removed

- `StreamName.createStreamId`: Equinox `4.0.0-rc.3` provides a `StreamId` for this purpose [#86](https://github.com/jet/FsCodec/pull/86)

<a name="3.0.0-rc.7"></a>
## [3.0.0-rc.7] - 2022-09-06

### Added

- `Core.EventData/TimelineEvent`: Exposed default ctors [#83](https://github.com/jet/FsCodec/pull/83)

### Changed

- `Codec.Create`: Made timestamp mandatory in low level `up` / `down` signature [#83](https://github.com/jet/FsCodec/pull/83)

### Fixed

- `EventData.Create`: restored defaulting of `EventId` to `Guid.NewGuid` broken in [#82](https://github.com/jet/FsCodec/pull/82) [#83](https://github.com/jet/FsCodec/pull/83)

<a name="3.0.0-rc.6"></a>
## [3.0.0-rc.6] - 2022-09-02

### Added

- `StreamName.createStreamId`: Helper to compose a streamId (for use with Equinox V4 categoryName/streamId representation) [#82](https://github.com/jet/FsCodec/pull/82)
- `Serdes`: Add `SerializeToStream` and `DeserializeFromStream` [#83](https://github.com/jet/FsCodec/pull/83) :pray: [@deviousasti](https://github.com/deviousasti)

### Changed

- `StreamName.trySplitCategoryAndId`: renamed to `trySplitCategoryAndStreamId` to align with `createStreamId` [#82](https://github.com/jet/FsCodec/pull/82)

<a name="3.0.0-rc.5"></a>
## [3.0.0-rc.5] - 2022-09-01

### Added

- `TimelineEvent.Size`: Enables stores to surface the stored size at the point of loading [#82](https://github.com/jet/FsCodec/pull/82)

### Changed

- `Option/Tuple`: Replace with `ValueOption`/`ValueTuple` [#82](https://github.com/jet/FsCodec/pull/82)
- `Codec 'Context`: replace `'Context option` with `Context` [#82](https://github.com/jet/FsCodec/pull/82)

<a name="3.0.0-rc.4"></a>
## [3.0.0-rc.4] - 2022-07-05

- `TryDeflate`:`ToByteArrayCodec`/`ToUtf8ArrayCodec`: Maps `int * ReadOnlyMemory<byte>` encodings to (uncompressed) `byte[]`/`ReadOnlyMemory<byte>`  [#81](https://github.com/jet/FsCodec/pull/81)

### Added

<a name="3.0.0-rc.3"></a>
## [3.0.0-rc.3] - 2022-07-04

### Added

- `EncodeTryDeflate/EncodeUncompressed`: Maps `ReadOnlyMemory<byte>` bodies to `int * ReadOnlyMemory<byte>` (with a non-zero value indicating compression was applied) [#80](https://github.com/jet/FsCodec/pull/80)

### Changed

- Replaced `SourceLink` with `DotNet.ReproducibleBuilds` wrapper

<a name="3.0.0-rc.2"></a>
## [3.0.0-rc.2] - 2022-05-07

### Added

- `Core.EventData/TimelineEvent/EventCodec.Map`: Exposed building blocks for mapping event envelopes and/or codecs over Body Format types [#77](https://github.com/jet/FsCodec/pull/77)

<a name="3.0.0-rc.1"></a>
## [3.0.0-rc.1] - 2022-05-05

### Added

- `SystemTextJson.CodecJsonElement`: Maps Unions to/from Events with `JsonElement` Bodies as `SystemTextJson.Codec` did in in `2.x` [#75](https://github.com/jet/FsCodec/pull/75)
- `SystemTextJson.ToUtf8Codec`: Adapter to map from `JsonElement` to `ReadOnlyMemory<byte>` Event Bodies (for interop scenarios; ideally one uses `SystemTextJson.Codec` directly in the first instance) [#75](https://github.com/jet/FsCodec/pull/75)

### Changed

- `NewtonsoftJson`: Rename `Settings` to `Options` [#60](https://github.com/jet/FsCodec/issues/60) [#76](https://github.com/jet/FsCodec/pull/76)
- Updated build and tests to use `net6.0`, all test package dependencies
- Updated `TypeShape` reference to v `10`, triggering min `FSharp.Core` target moving to `4.5.4`
- `SystemTextJson.Codec`: Switched Event body type from `JsonElement` to `ReadOnlyMemory<byte>` [#75](https://github.com/jet/FsCodec/pull/75)
- `NewtonsoftJson.Codec`: Switched Event body type from `byte[]` to `ReadOnlyMemory<byte>` [#75](https://github.com/jet/FsCodec/pull/75)
- `ToByteArrayCodec`: now adapts a `ReadOnlyMemory<byte>` encoder (was from `JsonElement`) (to `byte[]` bodies); Moved from `FsCodec.SystemTextJson` to `FsCodec.Box` [#75](https://github.com/jet/FsCodec/pull/75)

### Removed

- `net461` support [#60](https://github.com/jet/FsCodec/issues/60) [#76](https://github.com/jet/FsCodec/pull/76)

<a name="2.3.2"></a>
## [2.3.2] - 2022-03-10

### Added

- `SystemTextJson`: Add `Options.Default` to match [`JsonSerializerSettings.Default`](https://github.com/dotnet/runtime/pull/61434) [#73](https://github.com/jet/FsCodec/pull/73)

### Changed

- `SystemTextJson`: Replace `autoUnion=true` with individually controllable `autoTypeSafeEnumToJsonString` and `autoUnionToJsonObject` settings re [#71](https://github.com/jet/FsCodec/issues/71) [#73](https://github.com/jet/FsCodec/pull/73)

<a name="2.3.1"></a>
## [2.3.1] - 2022-03-02 **Unlisted in favor of changed API in 2.3.2**

### Fixed

- `SystemTextJson`: Prevent `UnionConverter` being applied to `option` and `list` types when using `UnionOrTypeSafeEnumConverterFactory`/`SystemTextJson.Options(autoUnion = true)` [#72](https://github.com/jet/FsCodec/pull/72)

<a name="2.3.0"></a>
## [2.3.0] - 2022-01-14 **Unlisted due to bug fixed in 2.3.1**

### Changed

- `SystemTextJson`: Target `TypeShape` v `9.0.0` in order to reinstate support for `FSharp.Core` v `4.3.4`

<a name="2.3.0-rc.2"></a>
## [2.3.0-rc.2] - 2022-01-05

### Added

- `SystemTextJson.UnionOrTypeSafeEnumConverterFactory`: Global converter that automatically applies a `TypeSafeEnumConverter` to all Discriminated Unions that support it, and `UnionConverter` to all others [#69](https://github.com/jet/FsCodec/pull/69)
- `SystemTextJson.Options(autoUnion = true)`: Automated wireup of `UnionOrTypeSafeEnumConverterFactory` [#69](https://github.com/jet/FsCodec/pull/69)

### Changed

- `Serdes`: Changed `Serdes` to be stateful, requiring a specific set of `Options`/`Settings` that are always applied consistently [#70](https://github.com/jet/FsCodec/pull/70)
- `Serdes.DefaultSettings`: Updated [README.md ASP.NET integration advice](https://github.com/jet/FsCodec#aspnetstj) to reflect minor knock-on effect [#70](https://github.com/jet/FsCodec/pull/70)

<a name="2.3.0-rc.1"></a>
## [2.3.0-rc.1] - 2022-01-04

### Added

- `SystemTextJson.UnionConverter`: Port of `NewtonsoftJson` equivalent started in [#43](https://github.com/jet/FsCodec/pull/43) [#59](https://github.com/jet/FsCodec/pull/59) :pray: [@NickDarvey](https://github.com/NickDarvey)

### Changed

- `SystemTextJson`: Target `System.Text.Json` v `6.0.1`, `TypeShape` v `10.0.0` [#68](https://github.com/jet/FsCodec/pull/68)

<a name="2.2.2"></a>
## [2.2.2] - 2021-09-12

### Fixed

- Fix CI to stop emitting builds with incorrect `AssemblyVersion 1.0.0.0` (updated MinVer to `2.5.0`)
- Update global.json to use SDK version `5.0.200`

<a name="2.2.1"></a>
## [2.2.1] - 2021-09-09

**NOTE erroneously tagged with `AssemblyVersion`/`FileVersion` 1.0.0; _unpublished_**

### Changed

- `FsCodec.SystemTextJson` - updated to target released `System.Text.Json` v `5.0.0` binaries [#66](https://github.com/jet/FsCodec/pull/66)
 
<a name="2.2.0"></a>
## [2.2.0] - 2021-05-05

**NOTE erroneously tagged with `AssemblyVersion`/`FileVersion` 1.0.0, which causes runtime errors when used by callers built against 2.x versions (i.e. Equinox, Propulsion etc); :pray: [@mousake](https://github.com/mousaka) _unlisted_**

### Added

- `NewtonsoftJson.Serdes.DefaultSettings`: Exposes default settings (for use with ASP.NET Core `.AddNewtonsoftJson`) [#63](https://github.com/jet/FsCodec/pull/63)
- `SystemTextJson.Serdes.DefaultOptions`: Exposes default options (for use with ASP.NET Core `.AddJsonOptions`) [#63](https://github.com/jet/FsCodec/pull/63)

### Fixed

- Clarify `StreamName.parse` exception message [#58](https://github.com/jet/FsCodec/pull/58) :pray: [@dharmaturtle](https://github.com/dharmaturtle)
- Remove erroneous `Converters` from `FsCodec.SystemTextJson.Converters.JsonOptionConverter` namespacing

<a name="2.1.1"></a>
## [2.1.1] - 2020-05-25

- `UnionConverter`: Handle nested unions [#52](https://github.com/jet/FsCodec/pull/52)
- `UnionConverter`: Support overriding discriminator without needing to nominate a `catchAllCase` [#51](https://github.com/jet/FsCodec/pull/51)

<a name="2.1.0"></a>
## [2.1.0] - 2020-05-10

### Added

- `FsCodec.SystemTextJson` - Feature-compatible port of `FsCodec.NewtonsoftJson` based on `System.Text.Json` v `>= 5.0.0-preview.3` [#38](https://github.com/jet/FsCodec/pull/38) :pray: [@ylibrach](https://github.com/ylibrach)

### Changed

- `FsCodec.Box` - new Package (existing Impl moved from `FsCodec.NewtonsoftJson`) [#38](https://github.com/jet/FsCodec/pull/38)

<a name="2.0.1"></a>
## [2.0.1] - 2020-02-26

### Changed

- Disabled tests for net461 on non-Windows to silence CI
- Target SDK ver `3.1.101`, target latest images in CI
- Remove `null` constraint from the `'Format` type arg [#37](https://github.com/jet/FsCodec/pull/37)

<a name="2.0.0"></a>
## [2.0.0] - 2020-02-19

### Added

- Add `EventId` to `IEventData` [#36](https://github.com/jet/FsCodec/pull/36)

### Changed

- Permit embedded dashes in `FsCodec.StreamName`'s `{aggregateId}` segment [#34](https://github.com/jet/FsCodec/pull/34)

### Fixed

- Reorder to fix consistency of type args in `FsCodec.Code.Create<'Event, 'Format, 'Context>`

<a name="2.0.0-rc3"></a>
## [2.0.0-rc3] - 2020-01-31

### Added

- `ITimelineEvent.Context` extension field [#30](https://github.com/jet/FsCodec/pull/30)

<a name="2.0.0-rc2"></a>
## [2.0.0-rc2] - 2020-01-30

### Added

- `StreamName`, with associated helper module [#31](https://github.com/jet/FsCodec/pull/31)

<a name="2.0.0-rc1"></a>
## [2.0.0-rc1] - 2020-01-14

### Added

- Tutorial and Documentation re `IEventCodec` [#29](https://github.com/jet/FsCodec/pull/29)

### Changed

- renamed `IUnionEncoder` to `IEventCodec` [#29](https://github.com/jet/FsCodec/pull/29)
- adjusted return types for `FsCodec.Core.EventData.Create` and `.TimelineEvent.Create` to relevant interfaces [#29](https://github.com/jet/FsCodec/pull/29)
- Update `Microsoft.SourceLink.GitHub`, `Microsoft.Framework.ReferenceAssemblies` to `1.0.0`

<a name="1.2.1"></a>
## [1.2.1] - 2019-11-08

### Fixed

- Flipped misaligned argument order in `FsCode.Codec.Create(tryDecode,encode,mapCausation)` [#28](https://github.com/jet/FsCodec/pull/28)

<a name="1.2.0"></a>
## [1.2.0] - 2019-09-26

### Added

- `FsCodec.Box.Codec.Create`: an API equivalent substitute for `FsCodec.NewtonsoftJson.Codec.Create` for use in unit and integration tests [#25](https://github.com/jet/FsCodec/pull/25)

### Changed

- Generalized `Codec.Create` to no longer presume `Data` and `Metadata` should always be `byte[]` [#24](https://github.com/jet/FsCodec/pull/24)

### Removed

- Removed accidentally pasted `setting` and `allowNullaryCases` in `Codec.Create`  [#23](https://github.com/jet/FsCodec/pull/23)

<a name="1.1.0"></a>
## [1.1.0] - 2019-09-19

### Added

- Polished overloads of `Codec.Create` and `NewtonsoftJson.Codec.Create` to be more navigable, and usable from C# [#23](https://github.com/jet/FsCodec/pull/23)

<a name="1.0.0"></a>
## [1.0.0] - 2019-09-17

### Added

- Defined `CorrelationId` and `CausationId` properties for `IEventData` [#21](https://github.com/jet/FsCodec/pull/21)
- Added `context : 'Context option` param to `IUnionEncoder.Encode`, enabling `down` to enrich events with `correlationId` and `causationId` values without reference to external state [#21](https://github.com/jet/FsCodec/pull/21)

### Changed

- Removed comparison support from `EventData` [#19](https://github.com/jet/FsCodec/pull/19)
- Changed `IndexedEventData` ctor to `.Create` and aligned with `EventData` [#19](https://github.com/jet/FsCodec/pull/19)
- Renamed `IEvent` to `IEventData` (to avoid clashes with `FSharp.Control.IEvent`) [#20](https://github.com/jet/FsCodec/pull/20)
- Renamed `IIndexedEvent` to `ITimelineEvent` (to avoid clashes with `FSharp.Control.IEvent`) [#20](https://github.com/jet/FsCodec/pull/20)
- Renamed `IndexedEventData` to `TimelineEvent` [#20](https://github.com/jet/FsCodec/pull/20)
- Updated `TypeShape` dependency to `8.0.0`
- Updated `MinVer` internal dependency to `2.0.0`

<a name="1.0.0-rc2"></a>
## [1.0.0-rc2] - 2019-09-07

### Added

- `tests/FsCodec.NewtonsoftJson.Tests/examples.fsx` counterpart to the `README.md`
- Exposed `TypeSafeEnum`
- `IndexedEventData` type to replace usage of impromptu objects
- overload with `up`/`down` arguments on `FsCodec.NewtonsoftJson.Codec.Create` facilitating surfacing index, metadata, and other such information in the event as surfaced to the programming model functions [#17](https://github.com/jet/FsCodec/pull/17)

### Changed

- `IUnionEncoder.TryDecode` now operates on `IIndexedEvent` (which moves to `FsCodec` from `FsCodec.Core`) instead of `IEvent`

### Removed

- `FsCodec.NewtonsoftJson.Codec.Create` overload with `genMetadata` and `genTimestamp` arguments (equivalent functionality can be achieved via `up`/`down` arguments) [#17](https://github.com/jet/FsCodec/pull/17)

### Fixed

- Pushed `TypeShape`'s `PackageReference` down into `FsCodec.NewtonsoftJson`

<a name="1.0.0-rc1"></a>
## [1.0.0-rc1] - 2019-08-30

Initial release based on merge of [Jet.JsonNet.Converters v0](https://github.com/jet/FsCodec/tree/v0) and the codecs formerly known as `Equinox.Codec` from [Equinox](https://github.com/jet/equinox) [#15](https://github.com/jet/FsCodec/pull/15)

<a name="0.2.2."></a>
## Jet.JsonNet.Converters [0.2.2]

Final release of Jet.JsonNet.Converters archived on [v0 branch](https://github.com/jet/FsCodec/tree/v0)

[Unreleased]: https://github.com/jet/FsCodec/compare/3.0.0-rc.13...HEAD
[3.0.0-rc.14]: https://github.com/jet/FsCodec/compare/3.0.0-rc.13...3.0.0-rc.14
[3.0.0-rc.13]: https://github.com/jet/FsCodec/compare/3.0.0-rc.12...3.0.0-rc.13
[3.0.0-rc.12]: https://github.com/jet/FsCodec/compare/3.0.0-rc.11...3.0.0-rc.12
[3.0.0-rc.11]: https://github.com/jet/FsCodec/compare/3.0.0-rc.10...3.0.0-rc.11
[3.0.0-rc.10]: https://github.com/jet/FsCodec/compare/3.0.0-rc.9...3.0.0-rc.10
[3.0.0-rc.9]: https://github.com/jet/FsCodec/compare/3.0.0-rc.8...3.0.0-rc.9
[3.0.0-rc.8]: https://github.com/jet/FsCodec/compare/3.0.0-rc.7...3.0.0-rc.8
[3.0.0-rc.7]: https://github.com/jet/FsCodec/compare/3.0.0-rc.6...3.0.0-rc.7
[3.0.0-rc.6]: https://github.com/jet/FsCodec/compare/3.0.0-rc.5...3.0.0-rc.6
[3.0.0-rc.5]: https://github.com/jet/FsCodec/compare/3.0.0-rc.4...3.0.0-rc.5
[3.0.0-rc.4]: https://github.com/jet/FsCodec/compare/3.0.0-rc.3...3.0.0-rc.4
[3.0.0-rc.3]: https://github.com/jet/FsCodec/compare/3.0.0-rc.2...3.0.0-rc.3
[3.0.0-rc.2]: https://github.com/jet/FsCodec/compare/3.0.0-rc.1...3.0.0-rc.2
[3.0.0-rc.1]: https://github.com/jet/FsCodec/compare/2.3.2...3.0.0-rc.1
[2.3.2]: https://github.com/jet/FsCodec/compare/2.3.1...2.3.2
[2.3.1]: https://github.com/jet/FsCodec/compare/2.3.0...2.3.1
[2.3.0]: https://github.com/jet/FsCodec/compare/2.3.0-rc.2...2.3.0
[2.3.0-rc.2]: https://github.com/jet/FsCodec/compare/2.3.0-rc.1...2.3.0-rc.2
[2.3.0-rc.1]: https://github.com/jet/FsCodec/compare/2.2.2...2.3.0-rc.1
[2.2.2]: https://github.com/jet/FsCodec/compare/2.2.1...2.2.2
[2.2.1]: https://github.com/jet/FsCodec/compare/2.2.0...2.2.1
[2.2.0]: https://github.com/jet/FsCodec/compare/2.1.1...2.2.0
[2.1.1]: https://github.com/jet/FsCodec/compare/2.1.0...2.1.1
[2.1.0]: https://github.com/jet/FsCodec/compare/2.0.1...2.1.0
[2.0.1]: https://github.com/jet/FsCodec/compare/2.0.0...2.0.1
[2.0.0]: https://github.com/jet/FsCodec/compare/2.0.0-rc3...2.0.0
[2.0.0-rc3]: https://github.com/jet/FsCodec/compare/2.0.0-rc2...2.0.0-rc3
[2.0.0-rc2]: https://github.com/jet/FsCodec/compare/2.0.0-rc1...2.0.0-rc2
[2.0.0-rc1]: https://github.com/jet/FsCodec/compare/1.2.1...2.0.0-rc1
[1.2.1]: https://github.com/jet/FsCodec/compare/1.2.0...1.2.1
[1.2.0]: https://github.com/jet/FsCodec/compare/1.1.0...1.2.0
[1.1.0]: https://github.com/jet/FsCodec/compare/1.0.0...1.1.0
[1.0.0]: https://github.com/jet/FsCodec/compare/1.0.0-rc2...1.0.0
[1.0.0-rc2]: https://github.com/jet/FsCodec/compare/1.0.0-rc1...1.0.0-rc2
[1.0.0-rc1]: https://github.com/jet/FsCodec/compare/0.2.2...1.0.0-rc1
[0.2.2]: https://github.com/jet/FsCodec/compare/0eb459b3aca873a40492d6a6c19cab4111d8f53e...0.2.2
