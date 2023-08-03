namespace FsCodec 

open FSharp.UMX
type private String = System.String

/// Represents the second half of a canonical StreamName, i.e., the streamId in "{categoryName}-{streamId}"
type StreamId = string<streamId>
and [<Measure>] streamId
/// Low-level helpers for composing and rendering StreamId values; prefer the ones in the Equinox namespace
module StreamId =
    let [<Literal>] private separator = "_"

    /// Create a StreamId, trusting the input to be well-formed (see the gen* functions for composing with validation)
    let ofRaw (raw: string): StreamId = UMX.tag raw
    /// Validates and generates a StreamId from an application level fragment. Throws if any of the fragments embed a `_`, are `null`, or are empty
    let ofFragment (fragment: string): StreamId =
        // arguably rejection of `_` chars is a step too far, but this accommodates for more easily dealing with namespacing dictated by unforeseen needs
        StreamName.Internal.validateElement fragment
        ofRaw fragment
    /// Combines streamId fragments. Throws if any of the fragments embed a `_`, are `null`, or are empty
    let ofFragments (fragments: string[]): StreamId =
        fragments |> Array.iter StreamName.Internal.validateElement 
        String.Join(separator, fragments) |> ofRaw
    /// Render as a string for external use
    let toString: StreamId -> string = UMX.untag
    /// Render as a canonical "{categoryName}-{streamId}" StreamName. Throws if the categoryName embeds `-` chars.
    let renderStreamName categoryName (x: StreamId): StreamName = toString x |> StreamName.create categoryName

    /// Generate a StreamId from a single application-level id, given a rendering function that maps to a non empty fragment without embedded `_` chars
    let gen f a = ofFragment (f a)
    /// Generate a StreamId from a tuple of application-level ids, given two rendering functions that map to a non empty fragment without embedded `_` chars
    let gen2 f g (a, b) = ofFragments [| f a; g b |]
    /// Generate a StreamId from a triple of application-level ids, given three rendering functions that map to a non empty fragment without embedded `_` chars
    let gen3 f g h (a, b, c) = ofFragments [| f a; g b; h c |]
    /// Generate a StreamId from a 4-tuple of application-level ids, given four rendering functions that map to a non empty fragment without embedded `_` chars
    let gen4 f g h i (a, b, c, d) = ofFragments [| f a; g b; h c; i d |]

