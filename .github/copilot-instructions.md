# FsCodec Repository Instructions

## Overview

FsCodec is an extensible F# event codec library for .NET that provides minimal interfaces for serialization and deserialization in event-sourcing systems. It's used with frameworks like Equinox and Propulsion for defining versionable event contracts using ubiquitous serializers.

## Key Technologies

- **Language:** F# (F# 4.5+) targeting `netstandard2.1`
- **.NET SDK:** 10.0.100 (specified in `global.json`)
- **Test Framework:** xunit v3 with Unquote for assertions
- **JSON Serializers:** System.Text.Json (6.0.10+) and Newtonsoft.Json (13.0.3+)
- **Build System:** MSBuild via dotnet CLI

## Project Structure

```
src/
  FsCodec/                    - Core interfaces with no dependencies
  FsCodec.Box/                - Box codec using TypeShape
  FsCodec.NewtonsoftJson/     - Newtonsoft.Json implementation
  FsCodec.SystemTextJson/     - System.Text.Json implementation
tests/
  FsCodec.Tests/              - Core unit tests
  FsCodec.NewtonsoftJson.Tests/
  FsCodec.SystemTextJson.Tests/
```

Configuration files: `Directory.Build.props`, `Directory.Build.targets`, `global.json`, `.editorconfig`

## Building the Project

**Always clean before building** to avoid caching issues:

```bash
dotnet clean
dotnet pack build.proj --configuration Release
```

Individual package builds:
```bash
dotnet pack src/FsCodec --configuration Release
dotnet pack src/FsCodec.Box --configuration Release
dotnet pack src/FsCodec.NewtonsoftJson --configuration Release
dotnet pack src/FsCodec.SystemTextJson --configuration Release
```

Build outputs go to `bin/` directory with version numbers from MinVer (git tag-based semantic versioning).

## Running Tests

**Always run tests from solution level** to ensure all projects are built correctly:

```bash
dotnet test --solution ./FsCodec.sln
```

For CI-compatible test results:
```bash
dotnet test --solution ./FsCodec.sln --report-xunit-trx
```

Test result files: `tests/**/*.trx`

**Test framework:** Uses xunit v3 with Microsoft.Testing.Platform runner (specified in `global.json`).

## Code Style and Validation

### EditorConfig Rules
- **Indentation:** 4 spaces (no tabs)
- **Line endings:** LF (Unix-style)
- **Trim trailing whitespace:** Yes
- **Final newline:** Required

The `.editorconfig` file contains F#-specific formatting rules that must be followed.

### Compiler Settings
- All warnings treated as errors (`TreatWarningsAsErrors=true`)
- Warning level 5 (highest)
- XML documentation auto-generated for all public APIs

### Package Validation
- Uses `Microsoft.DotNet.PackageValidation` v1.0.0-preview
- Baseline version: 3.0.0
- Validates breaking changes and API compatibility

## CI/CD Pipeline

The `azure-pipelines.yml` defines builds for Windows, Linux, and macOS:
1. Restores dependencies
2. Runs all tests with xunit-trx reporting
3. Packs NuGet packages
4. Publishes test results and build artifacts

## Important Notes

- **Versioning:** Automated via MinVer based on git tags; do not manually set version numbers
- **Dependencies:** FsCodec core has zero dependencies by design; keep it that way
- **F# Style:** Use F# idioms (e.g., discriminated unions, option types, computation expressions)
- **Testing:** Each implementation package has corresponding test project; maintain this parallel structure
- **Breaking Changes:** Package validation enforces API compatibility against baseline v3.0.0

## Common Commands Reference

```bash
# Restore dependencies
dotnet restore

# Build all
dotnet pack build.proj --configuration Release

# Run all tests
dotnet test --solution ./FsCodec.sln

# Clean build artifacts
dotnet clean

# Check solution integrity
dotnet build FsCodec.sln
```

## Troubleshooting

- If build fails with caching issues, run `dotnet clean` first
- If tests fail to discover, ensure `global.json` specifies the test runner correctly
- If package validation fails, check baseline version compatibility in `Directory.Build.props`
- Always ensure .NET SDK 10.0 or later is installed (`dotnet --version`)
