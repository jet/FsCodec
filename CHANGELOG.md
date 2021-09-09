# Changelog

The repo is versioned based on [SemVer 2.0](https://semver.org/spec/v2.0.0.html) using the tiny-but-mighty [MinVer](https://github.com/adamralph/minver) from [@adamralph](https://github.com/adamralph). [See here](https://github.com/adamralph/minver#how-it-works) for more information on how it works.

All notable changes to this project will be documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

The `Unreleased` section name is replaced by the expected version of next release. A stable version's log contains all changes between that version and the previous stable version (can duplicate the prereleases logs).

## [Unreleased]

### Added
### Changed

- `FsCodec.SystemTextJson` - updated to target released `System.Text.Json` v `5.0.2` binaries [#66](https://github.com/jet/FsCodec/pull/66)

### Removed
### Fixed

<a name="2.2.0"></a>
## [2.2.0] - 2021-05-05

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

[Unreleased]: https://github.com/jet/FsCodec/compare/2.2.0...HEAD
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
