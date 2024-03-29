// Represents the second half of a canonical StreamName, i.e., the streamId in "{categoryName}-{streamId}"
// Low-level helpers for composing and rendering StreamId values; prefer the ones in the Equinox namespace
namespace FsCodec

open FSharp.UMX
open System

/// Represents the second half of a canonical StreamName, i.e., the streamId in "{categoryName}-{streamId}"
type StreamId = string<streamId>
and [<Measure>] streamId

/// Helpers for composing and rendering StreamId values
module StreamId =

    /// Any string can be a StreamId; parse/dec/Elements.split will judge whether it adheres to a valid form
    let create: string -> StreamId = UMX.tag

    /// Render as a string for external use
    let toString: StreamId -> string = UMX.untag

    module Element =

        let [<Literal>] Separator = '_' // separates {subId1_subId2_..._subIdN}

        /// Throws if a candidate id element includes a '_', is null, or is empty
        let inline validate (raw: string) =
            if raw |> String.IsNullOrEmpty then invalidArg "raw" "Element must not be null or empty"
            if raw.IndexOf Separator <> -1 then invalidArg "raw" "Element may not contain embedded '_' symbols"

    module Elements =

        let [<Literal>] Separator = "_"

        /// Create a StreamId, trusting the input to be well-formed (see the gen* functions for composing with validation)
        let trust (raw: string): StreamId = UMX.tag raw

        /// Creates from exactly one fragment. Throws if the fragment embeds a `_`, are `null`, or is empty
        let parseExactlyOne (rawFragment: string): StreamId =
            Element.validate rawFragment
            trust rawFragment

        /// Combines streamId fragments. Throws if any of the fragments embed a `_`, are `null`, or are empty
        let compose (rawFragments: string[]): StreamId =
            rawFragments |> Array.iter Element.validate
            String.Join(Separator, rawFragments) |> trust

        let private separator = [| Element.Separator |]
        /// Splits a streamId into its constituent fragments
        let split (x: StreamId): string[] =
            (toString x).Split separator
        /// Splits a streamId into its constituent fragments
        let (|Split|): StreamId -> string[] = split

    /// Helpers to generate StreamIds given a number of individual id to string mapper functions
    [<AbstractClass; Sealed>]
    type Gen private () =

        /// Generate a StreamId from a single application-level id, given a rendering function that maps to a non empty fragment without embedded `_` chars
        static member Map(f: 'a -> string) = Func<'a, StreamId>(fun id -> f id |> Elements.parseExactlyOne)
        /// Generate a StreamId from a tuple of application-level ids, given 2 rendering functions that map to a non empty fragment without embedded `_` chars
        static member Map(f, f2) = Func<'a, 'b, StreamId>(fun id1 id2 -> Elements.compose [| f id1; f2 id2 |])
        /// Generate a StreamId from a triple of application-level ids, given 3 rendering functions that map to a non empty fragment without embedded `_` chars
        static member Map(f1, f2, f3) = Func<'a, 'b, 'c, StreamId>(fun id1 id2 id3 -> Elements.compose [| f1 id1; f2 id2; f3 id3 |])
        /// Generate a StreamId from a 4-tuple of application-level ids, given 4 rendering functions that map to a non empty fragment without embedded `_` chars
        static member Map(f1, f2, f3, f4) = Func<'a, 'b, 'c, 'd, StreamId>(fun id1 id2 id3 id4 -> Elements.compose [| f1 id1; f2 id2; f3 id3; f4 id4 |])

    /// Generate a StreamId from a single application-level id, given a rendering function that maps to a non empty fragment without embedded `_` chars
    let gen (f: 'a -> string): 'a -> StreamId = Gen.Map(f).Invoke
    /// Generate a StreamId from a tuple of application-level ids, given two rendering functions that map to a non empty fragment without embedded `_` chars
    let gen2 f1 f2 struct (a: 'a, b: 'b): StreamId = Gen.Map(f1, f2).Invoke(a, b)
    /// Generate a StreamId from a triple of application-level ids, given three rendering functions that map to a non empty fragment without embedded `_` chars
    let gen3 f1 f2 f3 struct (a: 'a, b: 'b, c: 'c): StreamId = Gen.Map(f1, f2, f3).Invoke(a, b, c)
    /// Generate a StreamId from a 4-tuple of application-level ids, given four rendering functions that map to a non empty fragment without embedded `_` chars
    let gen4 f1 f2 f3 f4 struct (a: 'a, b: 'b, c: 'c, d: 'd): StreamId = Gen.Map(f1, f2, f3, f4).Invoke(a, b, c, d)

    /// Validates and extracts the StreamId into a single fragment value
    /// Throws if the item embeds a `_`, is `null`, or is empty
    let parseExactlyOne (x: StreamId): string = toString x |> Elements.parseExactlyOne |> toString
    /// Validates and extracts the StreamId into a single fragment value
    /// Throws if the item embeds a `_`, is `null`, or is empty
    let (|Parse1|) (x: StreamId): string = parseExactlyOne x

    /// Splits a StreamId into the specified number of fragments.
    /// Throws if the value does not adhere to the expected fragment count.
    let parse count (x: StreamId): string[] =
        let xs = Elements.split x
        if xs.Length <> count then
            invalidArg "x" (sprintf "StreamId '{%s}' must have {%d} elements, but had {%d}." (toString x) count xs.Length)
        xs
    /// Splits a StreamId into an expected number of fragments.
    /// Throws if the value does not adhere to the expected fragment count.
    let (|Parse|) count: StreamId -> string[] = parse count

    /// Extracts a single fragment from the StreamId. Throws if the value is composed of more than one item.
    let dec f (x: StreamId) =                   parseExactlyOne x |> f
    /// Extracts 2 fragments from the StreamId. Throws if the value does not adhere to that expected form.
    let dec2 f1 f2 (x: StreamId) =              let xs = parse 2 x in struct (f1 xs[0], f2 xs[1])
    /// Extracts 3 fragments from the StreamId. Throws if the value does not adhere to that expected form.
    let dec3 f1 f2 f3 (x: StreamId) =           let xs = parse 3 x in struct (f1 xs[0], f2 xs[1], f3 xs[2])
    /// Extracts 4 fragments from the StreamId. Throws if the value does not adhere to that expected form.
    let dec4 f1 f2 f3 f4 (x: StreamId) =        let xs = parse 4 x in struct (f1 xs[0], f2 xs[1], f3 xs[2], f4 xs[3])
