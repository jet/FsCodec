# Copilot Instructions for FsCodec

## Project overview

FsCodec is a minimal F# library family for event payload serialization contracts in event-sourced systems.
It defines core abstractions (`IEventData`, `ITimelineEvent`, `IEventCodec`) in `src/FsCodec`, plus serializer-specific codec packages:
- `src/FsCodec.Box` — TypeShape-based core codec plumbing and in-memory/object codec
- `src/FsCodec.NewtonsoftJson` — Newtonsoft.Json implementation
- `src/FsCodec.SystemTextJson` — System.Text.Json implementation

The Newtonsoft and STJ packages expose a symmetric public API surface (`Codec`, `Options`, `Serdes`).

## Build and test

- .NET SDK version is pinned in `global.json` (currently `10.0.100` with `rollForward: latestMajor`)
- Build and pack: `dotnet build build.proj`
- Run tests: `dotnet test --solution ./FsCodec.sln`
- Pack NuGet packages: `dotnet pack build.proj`
- Tests use Microsoft Testing Platform (xUnit v3, `OutputType=Exe`)

## Coding conventions

- **F# style**: 4-space indentation, LF line endings, trim trailing whitespace (see `.editorconfig`)
- **Warnings as errors**: `TreatWarningsAsErrors=true` in `Directory.Build.props` — do not relax or suppress warnings globally
- **Event bodies**: favor `ReadOnlyMemory<byte>`; use `byte[]` adapters (`ByteArray.AsByteArray`) only for interop paths
- **Serialization contract boundary**: use `Options.Create`/`Serdes`; avoid ad-hoc serializer calls
- **Converter registration**: prefer explicit type-level `[<JsonConverter>]` attributes over broad global converter lists
- **Stream naming**: stream names follow `{category}-{streamId}` format; streamId fragments use `_` separators (see `StreamName.fs`, `StreamId.fs`)
- **Compression**: handled as an adapter concern (`Encoder.Compressed`/`Uncompressed`) over codecs, not in domain event logic

## Architecture guidelines

- **API symmetry**: preserve parallel API shapes between Newtonsoft and STJ packages unless intentionally diverging
- **Wire compatibility**: prefer additive extensions over changing established wire formats — wire compatibility is a first-order constraint
- **Minimal scope**: the repo avoids becoming a "converter kitchen sink"; keep changes focused and interoperability-oriented
- **Build layering**: in Debug, projects use `ProjectReference`; in Release, they switch to NuGet `PackageReference` ranges (see each `.fsproj`)

## Serializer behavior differences

- **Newtonsoft** (`Options.fs`): prepends `OptionConverter`, sets `DateTimeZoneHandling` to `Utc`, disables `DateParseHandling` (set to `None`)
- **STJ** (`Options.fs`): defaults to `unsafeRelaxedJsonEscaping = true`; optional `autoTypeSafeEnumToJsonString`, `autoUnionToJsonObject`, `rejectNullStrings`

## Testing

- Test projects live under `tests/` as executable xUnit v3 MTP projects
- `FsCodec.SystemTextJson.Tests` shares some test sources with `FsCodec.NewtonsoftJson.Tests` and uses the `SYSTEM_TEXT_JSON` compilation constant for STJ-specific sections
- End-to-end codec examples are in `tests/FsCodec.*.Tests/Examples.fsx`
- The canonical `JsonIsomorphism` pattern for strongly-typed IDs is in `tests/FsCodec.NewtonsoftJson.Tests/Fixtures.fs`
