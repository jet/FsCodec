# AGENTS.md

## Purpose and scope
- FsCodec is a small F# library family for event payload contracts in event-sourced systems: core abstractions in `src/FsCodec`, plus serializer-specific codec packages in `src/FsCodec.NewtonsoftJson` and `src/FsCodec.SystemTextJson`.
- Keep changes minimal and interoperability-focused; the repo explicitly avoids becoming a "converter kitchen sink" (see `README.md` contribution guidance).

## Repo map (what depends on what)
- `src/FsCodec`: base contracts (`IEventData`, `ITimelineEvent`, `IEventCodec`) and mapping helpers (`EventData.MapBodies`, `TimelineEvent.MapBodies`, `EventCodec.MapBodies`) in `src/FsCodec/FsCodec.fs`.
- `src/FsCodec.Box`: TypeShape-based core codec plumbing (`src/FsCodec.Box/CoreCodec.fs`) and in-memory/object codec (`src/FsCodec.Box/Codec.fs`).
- `src/FsCodec.NewtonsoftJson` and `src/FsCodec.SystemTextJson`: same public shape (`Codec`, `Options`, `Serdes`) with serializer-specific implementations.
- Build-time layering is intentional: in Debug, projects use `ProjectReference`; in Release, they switch to NuGet `PackageReference` ranges (see each `*.fsproj`).

## Critical workflows
- SDK/tooling: `global.json` pins .NET SDK `10.0.100`; tests use Microsoft Testing Platform.
- CI-equivalent test command (from `azure-pipelines.yml`):
  `dotnet test --solution ./FsCodec.sln --report-xunit-trx`
- CI-equivalent pack command:
  `dotnet pack build.proj`
- Local integrity/build shortcut documented in `README.md`:
  `dotnet build build.proj`
- Packaging/version metadata is computed via MinVer + `Directory.Build.targets` (`BUILD_PR`, `BUILD_ID` impact package/file version).

## Project-specific coding patterns
- Favor `ReadOnlyMemory<byte>` event bodies; only use `byte[]` adapters (`ByteArray.AsByteArray`) for interop/porting paths.
- Use `Options.Create`/`Serdes` as the contract boundary; avoid ad-hoc serializer calls.
- Prefer explicit type-level converter attributes over broad global converter registration (examples in `tests/FsCodec.*.Tests/Examples.fsx`).
- Stream identity conventions are strict: stream names are `{category}-{streamId}`, streamId fragments use `_` separators (`src/FsCodec/StreamName.fs`, `src/FsCodec/StreamId.fs`).
- Compression is an adapter concern (`FsCodec.Encoder.Compressed` / `Uncompressed`) over codecs, not domain event logic (`src/FsCodec/Encoding.fs`).

## Serializer behavior differences to preserve
- Newtonsoft profile (`src/FsCodec.NewtonsoftJson/Options.fs`) prepends `OptionConverter` and disables DateParse-to-DateTime behavior.
- STJ profile (`src/FsCodec.SystemTextJson/Options.fs`) defaults to `unsafeRelaxedJsonEscaping = true` and can opt into `autoTypeSafeEnumToJsonString`, `autoUnionToJsonObject`, `rejectNullStrings`.
- STJ union/enum auto-conversion behavior and edge cases are validated in `tests/FsCodec.SystemTextJson.Tests/AutoUnionTests.fs`.

## Testing conventions and examples
- Test projects are executable xUnit v3 MTP projects (`OutputType=Exe`), with shared fixtures between serializer suites.
- Use `tests/FsCodec.NewtonsoftJson.Tests/Fixtures.fs` for the canonical `JsonIsomorphism` pattern for strongly-typed IDs.
- Use `tests/FsCodec.SystemTextJson.Tests/Examples.fsx` for end-to-end event flow examples (`StreamName`, active patterns, codec decode paths).

## Guardrails for AI edits
- Preserve API symmetry between Newtonsoft and STJ packages unless intentionally diverging.
- Do not relax warnings/errors globally (`Directory.Build.props` has `TreatWarningsAsErrors=true`).
- Prefer additive extensions over changing established wire formats; wire compatibility is a first-order constraint for this repo.
