// StreamName type and module; Manages creation and parsing of well-formed Stream Names
namespace FsCodec

open FSharp.UMX

/// <summary>Lightly-wrapped well-formed Stream Name adhering to one of two forms:<br/>
/// 1. <c>{category}-{streamId}</c>
/// 2. <c>{category}-{id1}_{id2}_...{idN}</c><br/>
/// See <a href="https://github.com/fsprojects/FSharp.UMX" /></summary>
type StreamName = string<streamName>
and [<Measure>] streamName

type CategoryAndStreamId = (struct (string * string))

/// Creates, Parses and Matches Stream Names in one of two forms:
/// 1. {category}-{streamId}
/// 2. {category}-{id1}_{id2}_...{idN}
module StreamName =

    // Validation helpers, etc.
    module Internal =

        /// Throws if a candidate category includes a '-', is null, or is empty
        let inline validateCategory (rawCategory : string) =
            if rawCategory |> System.String.IsNullOrEmpty then invalidArg "rawCategory" "may not be null or empty"
            if rawCategory.IndexOf '-' <> -1 then invalidArg "rawCategory" "may not contain embedded '-' symbols"

        /// Throws if a candidate id element includes a '_', is null, or is empty
        let inline validateElement (rawElement : string) =
            if rawElement |> System.String.IsNullOrEmpty then invalidArg "rawElement" "may not contain null or empty components"
            if rawElement.IndexOf '_' <> -1 then invalidArg "rawElement" "may not contain embedded '_' symbols"

        /// Low level helper used to gate ingestion from a canonical form, guarding against malformed streamNames
        let inline ofCategoryAndStreamId struct (category : string, streamId : string) : string =
            validateCategory category
            System.String.Concat(category, "-", streamId)

        /// Generates a StreamId from name elements; elements are separated from each other by '_'
        let createStreamId (elements : string seq) : string =
            for x in elements do validateElement x
            System.String.Join("_", elements)

    (* Creators: Building from constituent parts
       Guards against malformed category, streamId and/or streamId elements with exceptions *)

    /// Recommended way to specify a stream identifier; a category identifier and an streamId representing the aggregate's identity
    /// category is separated from id by `-`
    let create (category : string) (streamId : string) : StreamName =
        Internal.ofCategoryAndStreamId (category, streamId) |> UMX.tag

    /// Composes a StreamName from a category and > 1 name elements.
    /// category is separated from the streamId by '-'; elements are separated from each other by '_'
    let compose (category : string) (streamIdElements : string seq) : StreamName =
        create category (Internal.createStreamId streamIdElements)

    (* Parsing: Raw Stream name Validation functions/pattern that handle malformed cases without throwing *)

    /// <summary>Validates and maps a trusted Stream Name consisting of a Category and an Id separated by a '-' (dash).<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to that form.</summary>
    let parse (rawStreamName : string) : StreamName =
        if rawStreamName.IndexOf '-' = -1 then
            invalidArg "rawStreamName" (sprintf "Stream Name '%s' must contain a '-' separator" rawStreamName)
        UMX.tag rawStreamName

    let private dash = [|'-'|] // Separates {category}-{streamId}

    /// <summary>Attempts to split a Stream Name in the form <c>{category}-{streamId}</c> into its two elements.
    /// The <c>{streamId}</c> segment is permitted to include embedded '-' (dash) characters
    /// Returns <c>None</c> if it does not adhere to that form.</summary>
    let trySplitCategoryAndStreamId (rawStreamName : string) : struct (string * string) voption =
        match rawStreamName.Split(dash, 2) with
        | [| cat; id |] -> ValueSome struct (cat, id)
        | _ -> ValueNone

    /// <summary>Attempts to split a Stream Name in the form <c>{category}-{streamId}</c> into its two elements.
    /// The <c>{streamId}</c> segment is permitted to include embedded '-' (dash) characters
    /// Yields <c>NotCategorized</c> if it does not adhere to that form.</summary>
    let (|Categorized|NotCategorized|) (rawStreamName : string) : Choice<struct (string * string), unit> =
        match trySplitCategoryAndStreamId rawStreamName with
        | ValueSome catAndId -> Categorized catAndId
        | ValueNone -> NotCategorized

    (* Rendering *)

    /// Strip off the strong typing (It's recommended to pattern match as below in the general case)
    let inline toString (streamName : StreamName) : string =
        UMX.untag streamName

    (* Splitting: functions/Active patterns for (i.e. generated via `parse`, `create` or `compose`) well-formed Stream Names
       Will throw if presented with malformed strings [generated via alternate means] *)

    /// Extracts the category portion of the StreamName
    let category (x : StreamName) =
        let raw = toString x
        raw.Substring(0, raw.IndexOf '-')
    /// Extracts the category portion of a StreamName
    let (|Category|) = category

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{streamId}</c> into its two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let splitCategoryAndStreamId (streamName : StreamName) : struct (string * string) =
        let rawName = toString streamName
        match trySplitCategoryAndStreamId rawName with
        | ValueSome catAndId -> catAndId
        | ValueNone -> invalidArg "streamName" (sprintf "Stream Name '%s' must contain a '-' separator" rawName)

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{streamId}</c> into its two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if the stream name is not well-formed.</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let (|CategoryAndId|) : StreamName -> struct (string * string) = splitCategoryAndStreamId

    let private underscore = [|'_'|] // separates {category}-{subId1_subId2_..._subIdN}

    /// <summary>Splits a `_`-separated set of id elements (as formed by `compose`) into its (one or more) constituent elements.</summary>
    /// <remarks>Inverse of what <code>compose</code> does to the subElements</remarks>
    let (|IdElements|) (streamId : string) : string array =
        streamId.Split underscore

    /// <summary>Splits a well-formed Stream Name of the form {category}-{id1}_{id2}_{idN} into a pair of category and ids.<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let splitCategoryAndIds (streamName : StreamName) : struct (string * string array) =
        let rawName = toString streamName
        match trySplitCategoryAndStreamId rawName with
        | ValueSome (cat, IdElements ids) -> (cat, ids)
        | ValueNone -> invalidArg "streamName" (sprintf "Stream Name '%s' must contain a '-' separator" rawName)

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{streamId}</c> into the two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if the stream name is not well-formed</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let (|CategoryAndIds|) : StreamName -> struct (string * string array) = splitCategoryAndIds
