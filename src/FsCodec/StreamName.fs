// StreamName type and module; Manages creation and parsing of well-formed Stream Names
namespace FsCodec

open FSharp.UMX

/// Lightly-wrapped well-formed Stream Name adhering to one of two forms:
/// 1. <code>{category}-{aggregateId}</code>
/// 2. <code>{category}-{id1}_{id2}_...{idN}</code>
// see https://github.com/fsprojects/FSharp.UMX
type StreamName = string<streamName>
and [<Measure>] streamName

/// Creates, Parses and Matches Stream Names in one of two forms:
/// 1. {category}-{aggregateId}
/// 2. {category}-{id1}_{id2}_...{idN}
module StreamName =

    let private dash = [|'-'|] // Separates {category}-{aggregateId}
    let private underscore = [|'_'|] // separates {category}-{subId1_subId2_subId3_..._subIdN}

    (* Creators: Building from constituent parts
       Guards against malformed category, aggregateId and/or aggregateIdElements with exceptions *)

    /// Recommended way to specify a stream identifier; a category identifier and an aggregate identity
    /// category is separated from id by `-`
    let create (category : string) aggregateId : StreamName =
        if category.IndexOf '-' <> -1 then invalidArg "category" "may not contain embedded '-' symbols"
        UMX.tag (sprintf "%s-%s" category aggregateId)

    /// Composes a StreamName from a category and > 1 name elements.
    /// category is separated from the aggregateId by '-'; elements are separated from each other by '_'
    let compose (category : string) (aggregateIdElements : string seq) : StreamName =
        let buf = System.Text.StringBuilder 128
        let mutable first = true
        for x in aggregateIdElements do
            if first then () else buf.Append '_' |> ignore
            first <- false
            if System.String.IsNullOrEmpty x then invalidArg "subElements" "may not contain null or empty components"
            if x.IndexOf '_' <> -1 then invalidArg "subElements" "may not contain embedded '_' symbols"
            buf.Append x |> ignore
        create category (buf.ToString())

    /// Validates and maps a trusted Stream Name consisting of a Category and an Id separated by a '-` (dash)
    /// Throws <code>InvalidArgumentException</code> if it does not adhere to that form
    let parse (rawStreamName : string) : StreamName =
        if rawStreamName.IndexOf('-') = -1 then
            invalidArg "streamName" (sprintf "Stream Name '%s' must contain a '-' separator" rawStreamName)
        UMX.tag rawStreamName

    (* Parsing: Raw Stream name Validation functions/pattern that handle malformed cases without throwing *)

    /// Attempts to split a Stream Name in the form {category}-{id} into its two elements.
    /// The {id} segment is permitted to include embedded '-' (dash) characters
    /// Returns <code>None</code> if it does not adhere to that form.
    let trySplitCategoryAndId (rawStreamName : string) : (string * string) option =
        match rawStreamName.Split(dash, 2) with
        | [| cat; id |] -> Some (cat, id)
        | _ -> None

    /// Attempts to split a Stream Name in the form {category}-{id} into its two elements.
    /// The {id} segment is permitted to include embedded '-' (dash) characters
    /// Yields <code>NotCategorized</code> if it does not adhere to that form.
    let (|Categorized|NotCategorized|) (rawStreamName : string) : Choice<string * string, unit> =
        match trySplitCategoryAndId rawStreamName with
        | Some catAndId -> Categorized catAndId
        | None -> NotCategorized

    (* Rendering *)

    /// Strip off the strong typing (It's recommended to pattern match as below in the general case)
    let toString (streamName : StreamName) : string =
        UMX.untag streamName

    (* Splitting: functions/Active patterns for (i.e. generated via `parse`, `create` or `compose`) well-formed Stream Names
       Will throw if presented with malformed strings [generated via alternate means] *)

    /// Splits a well-formed Stream Name of the form {category}-{id} into its two elements.
    /// Throws <code>InvalidArgumentException</code> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).
    /// <remarks>Inverse of <code>create</code>
    let splitCategoryAndId (streamName : StreamName) : string * string =
        let rawName = toString streamName
        match trySplitCategoryAndId rawName with
        | Some catAndId -> catAndId
        | None -> invalidArg (sprintf "Stream Name '%s' must contain a '-' separator" rawName) "streamName"

    /// Splits a well-formed Stream Name of the form {category}-{id} into its two elements
    /// Throws <c>InvalidArgumentException</c> if the stream name is not well-formed
    /// <remarks>Inverse of <code>create</code>
    let (|CategoryAndId|) : StreamName -> (string * string) = splitCategoryAndId

    /// Splits a `_`-separated set of id elements (as formed by `compose`) into its (one or more) constituent elements.
    /// <remarks>Inverse of what <code>compose</code> does to the subElements
    let (|IdElements|) (aggregateId : string) : string[] =
        aggregateId.Split underscore

    /// Splits a well-formed Stream Name of the form {category}-{id1}_{id2}_{idN} into a pair of category and ids
    /// Throws <code>InvalidArgumentException</code> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).
    /// <remarks>Inverse of <code>create</code>
    let splitCategoryAndIds (streamName : StreamName) : string * string[] =
        let rawName = toString streamName
        match trySplitCategoryAndId rawName with
        | Some (cat, IdElements ids) -> (cat, ids)
        | None -> invalidArg (sprintf "Stream Name '%s' did not contain exactly one '-' separator" rawName) "streamName"

    /// Splits a well-formed Stream Name of the form {category}-{id} into the two elements
    /// Throws <c>InvalidArgumentException</c> if the stream name is not well-formed
    /// <remarks>Inverse of <code>create</code>
    let (|CategoryAndIds|) : StreamName -> (string * string[]) = splitCategoryAndIds
