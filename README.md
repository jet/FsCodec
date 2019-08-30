# FsCodec [![Build Status](https://dev.azure.com/jet-opensource/opensource/_apis/build/status/jet.fscodec?branchName=master)](https://dev.azure.com/jet-opensource/opensource/_build/latest?definitionId=18?branchName=master) [![release](https://img.shields.io/github/release/jet/fscodec.svg)](https://github.com/jet/fscodec/releases) [![NuGet](https://img.shields.io/nuget/vpre/fscodec.svg?logo=nuget)](https://www.nuget.org/packages/fscodec/) [![license](https://img.shields.io/github/license/jet/fscodec.svg)](LICENSE) ![code size](https://img.shields.io/github/languages/code-size/jet/fscodec.svg) [![docs status](https://img.shields.io/badge/DOCUMENTATION-WIP-important.svg?style=popout)](README.md)

This small but finely tuned set of `Newtonsoft.Json` Converters provide for Simple yet versionable serialization strategies without boilerplate code, ugly renderings or nasty surprises.

## Goals

- The converters are employed in diverse systems across Jet, both for [de]coding Events within Event-sourced streams, and for HTTP requests/responses. As such, format changes need to be interoperable.
- As the name suggests, the focus is on handling F# types.

## Non-goals

- Less [converters] is more - [has a converter _really_ proved itself broadly applicable](https://en.wikipedia.org/wiki/Rule_of_three_(computer_programming)) ?
- this is not the final complete set of converters; Json.net is purposefully extensible and limited only by your imagination, for better or worse.
- If `Newtonsoft.Json` can or should be made to do something, it should - this library is for extensions that absolutely positively can't go into Json.net itself.

# Features

## Concrete Converter implementations

See `.Tests` for rendering formats.

- `OptionConverter` - represents F#'s `Option<'t>` as a value or `null`
- `UnionConverter` - represents F# discriminated unions as a single Json object with named fields directly within the object (`Newtonsoft.Json.Converters.DiscriminatedUnionConverter` encodes the fields as an array without names, which has some pros, but also cons) :pray: [@amjdd](https://github.com/amjjd)
- `TypeSafeEnumConverter` - represents discriminated union (without any state), as a string (`Newtonsoft.Json.Converters.StringEnumConverter` permits values outside the declared values) :pray: [@amjjd](https://github.com/amjjd)

## Abstract base Converters

See `.Tests` for usage examples.

- `JsonPickler` - removes boilerplate from simple converters :pray: [@EirikTsarpalis](https://github.com/eiriktsarpalis) 
- `JsonIsomorphism` - allows one to cleanly map a type's internal representation to something that Json.net can already cleanly handle :pray: [@EirikTsarpalis](https://github.com/eiriktsarpalis)

# Dependencies

The core library extends [`Newtonsoft.Json`](https://github.com/JamesNK/Newtonsoft.Json) and is intended to work based on `netstandard2.0`.

The tests add a reliance on [`FSCheck.xUnit`](https://github.com/fscheck/FsCheck), [`xUnit.net`](https://github.com/xunit/xunit), and [`Unquote`](https://github.com/SwensenSoftware/unquote).

Naturally, the library also has a hard dependency on the `FSharp.Core` standard library (Json.net's `Newtonsoft.Json.Converters.DiscriminatedUnionConverter` has a softer dependency via reflection; going that extra mile here is unwarranted for now, given the implementation is in F#).

# CONTRIBUTION notes

In general, the intention is to keep this set of converters minimal and interoperable, e.g., many candidates are deliberately being excluded from this set; _its definitely a non-goal for this to become a compendium of every possible converter_. **So, especially in this repo, the bar for adding converters will be exceedingly high and hence any contribution should definitely be preceded by a discussion.**

Please raise GitHub issues for any questions so others can benefit from the discussion.

# Building

```powershell
# verify the integrity of the repo wrt being able to build/pack/test
./dotnet build ./build.proj
```