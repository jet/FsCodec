// StreamName type and module; Manages creation and parsing of well-formed Stream Names
namespace FsCodec

open FSharp.UMX

/// <summary>Lightly-wrapped well-formed Stream Name adhering to one of two forms:<br/>
/// 1. <c>{category}-{aggregateId}</c>
/// 2. <c>{category}-{id1}_{id2}_...{idN}</c><br/>
/// See <a href="https://github.com/fsprojects/FSharp.UMX" /></summary>
type StreamName = string<streamName>
and [<Measure>] streamName

/// Creates, Parses and Matches Stream Names in one of two forms:
/// 1. {category}-{aggregateId}
/// 2. {category}-{id1}_{id2}_...{idN}
module StreamName =

    (* Validation helpers *)

    /// Throws if a candidate category includes a '-', is null, or is empty
    let inline validateCategory (rawCategory : string) =
        if rawCategory |> System.String.IsNullOrEmpty then invalidArg "rawCategory" "may not be null or empty"
        if rawCategory.IndexOf '-' <> -1 then invalidArg "rawCategory" "may not contain embedded '-' symbols"

    /// Throws if a candidate id element includes a '_', is null, or is empty
    let inline validateElement (rawElement : string) =
        if rawElement |> System.String.IsNullOrEmpty then invalidArg "rawElement" "may not contain null or empty components"
        if rawElement.IndexOf '_' <> -1 then invalidArg "rawElement" "may not contain embedded '_' symbols"

    (* Creators: Building from constituent parts
       Guards against malformed category, aggregateId and/or aggregateId elements with exceptions *)

    /// Generates AggregateId from name elements; elements are separated from each other by '_'
    let createAggregateId (elements : string seq) : string =
        for x in elements do validateElement x
        System.String.Join("_", elements)

    /// Recommended way to specify a stream identifier; a category identifier and an aggregate identity
    /// category is separated from id by `-`
    let createRaw struct (category : string, aggregateId : string) : string =
        validateCategory category
        System.String.Concat(category, "-", aggregateId)

    /// Recommended way to specify a stream identifier; a category identifier and an aggregate identity
    /// category is separated from id by `-`
    let create (category : string) (aggregateId : string) : StreamName =
        createRaw (category, aggregateId) |> UMX.tag

    /// Composes a StreamName from a category and > 1 name elements.
    /// category is separated from the aggregateId by '-'; elements are separated from each other by '_'
    let compose (category : string) (aggregateIdElements : string seq) : StreamName =
        create category (createAggregateId aggregateIdElements)

    /// <summary>Validates and maps a trusted Stream Name consisting of a Category and an Id separated by a '-' (dash).<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to that form.</summary>
    let parse (rawStreamName : string) : StreamName =
        if rawStreamName.IndexOf '-' = -1 then
            invalidArg "rawStreamName" (sprintf "Stream Name '%s' must contain a '-' separator" rawStreamName)
        UMX.tag rawStreamName

    (* Parsing: Raw Stream name Validation functions/pattern that handle malformed cases without throwing *)

    let private dash = [|'-'|] // Separates {category}-{aggregateId}

    /// <summary>Attempts to split a Stream Name in the form <c>{category}-{aggregateId}</c> into its two elements.
    /// The <c>{aggregateId}</c> segment is permitted to include embedded '-' (dash) characters
    /// Returns <c>None</c> if it does not adhere to that form.</summary>
    let trySplitCategoryAndId (rawStreamName : string) : struct (string * string) voption =
        match rawStreamName.Split(dash, 2) with
        | [| cat; id |] -> ValueSome struct (cat, id)
        | _ -> ValueNone

    /// <summary>Attempts to split a Stream Name in the form <c>{category}-{aggregateId}</c> into its two elements.
    /// The <c>{aggregateId}</c> segment is permitted to include embedded '-' (dash) characters
    /// Yields <c>NotCategorized</c> if it does not adhere to that form.</summary>
    let (|Categorized|NotCategorized|) (rawStreamName : string) : Choice<struct (string * string), unit> =
        match trySplitCategoryAndId rawStreamName with
        | ValueSome catAndId -> Categorized catAndId
        | ValueNone -> NotCategorized

    (* Rendering *)

    /// Strip off the strong typing (It's recommended to pattern match as below in the general case)
    let inline toString (streamName : StreamName) : string =
        UMX.untag streamName

    (* Splitting: functions/Active patterns for (i.e. generated via `parse`, `create` or `compose`) well-formed Stream Names
       Will throw if presented with malformed strings [generated via alternate means] *)

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{aggregateId}</c> into its two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let splitCategoryAndId (streamName : StreamName) : struct (string * string) =
        let rawName = toString streamName
        match trySplitCategoryAndId rawName with
        | ValueSome catAndId -> catAndId
        | ValueNone -> invalidArg "streamName" (sprintf "Stream Name '%s' must contain a '-' separator" rawName)

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{aggregateId}</c> into its two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if the stream name is not well-formed.</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let (|CategoryAndId|) : StreamName -> struct (string * string) = splitCategoryAndId

    let private underscore = [|'_'|] // separates {category}-{subId1_subId2_subId3_..._subIdN}

    /// <summary>Splits a `_`-separated set of id elements (as formed by `compose`) into its (one or more) constituent elements.</summary>
    /// <remarks>Inverse of what <code>compose</code> does to the subElements</remarks>
    let (|IdElements|) (aggregateId : string) : string array =
        aggregateId.Split underscore

    /// <summary>Splits a well-formed Stream Name of the form {category}-{id1}_{id2}_{idN} into a pair of category and ids.<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let splitCategoryAndIds (streamName : StreamName) : struct (string * string array) =
        let rawName = toString streamName
        match trySplitCategoryAndId rawName with
        | ValueSome (cat, IdElements ids) -> (cat, ids)
        | ValueNone -> invalidArg "streamName" (sprintf "Stream Name '%s' must contain a '-' separator" rawName)

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{aggregateId}</c> into the two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if the stream name is not well-formed</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let (|CategoryAndIds|) : StreamName -> struct (string * string array) = splitCategoryAndIds
