// StreamName type and module; Manages creation and parsing of well-formed Stream Names
namespace FsCodec

open FSharp.UMX

/// <summary>Lightly-wrapped well-formed Stream Name adhering to one of two forms:<br/>
/// 1. <c>{category}-{streamId}</c>
/// 2. <c>{category}-{id1}_{id2}_...{idN}</c><br/>
/// See <a href="https://github.com/fsprojects/FSharp.UMX" /></summary>
type StreamName = string<streamName>
and [<Measure>] streamName

/// Creates, Parses and Matches Stream Names in one of two forms:
/// 1. {category}-{streamId}
/// 2. {category}-{id1}_{id2}_...{idN}
module StreamName =

    /// Strip off the strong typing (It's recommended to pattern match as below in the general case)
    let inline toString (x: StreamName) : string =
        UMX.untag x

    // Validation helpers, etc.
    module Category =

        let [<Literal>] Separator = '_' // separates {category}-{streamId}
        let separator = [|'-'|] // Separates {category}-{streamId}

        /// Throws if a candidate category includes a '-', is null, or is empty
        let inline validate (raw: string) =
            if raw |> System.String.IsNullOrEmpty then invalidArg "raw" "Category must not be null or empty"
            if raw.IndexOf Separator <> -1 then invalidArg "raw" "Category must not contain embedded '-' symbols"
        /// Extracts the category portion of the StreamName
        let ofStreamName (x: StreamName) =
            let raw = toString x
            raw.Substring(0, raw.IndexOf Separator)

    module Internal =

        let [<Literal>] SeparatorStr = "_"
        let throwInvalid raw = invalidArg "raw" (sprintf "Stream Name '%s' must contain a '-' separator" raw)
        /// Create a StreamName, trusting the input to be well-formed
        let trust (raw: string): StreamName = UMX.tag raw
        /// Render in canonical {categoryName}-{streamId} format. Throws if categoryName contains embedded `-` symbols
        let render rawCategoryName (streamId: StreamId) =
            Category.validate rawCategoryName
            System.String.Concat(rawCategoryName, Category.separator, streamId)
        /// Low level helper used to gate ingestion from a canonical form, guarding against malformed streamNames
        let ofCategoryAndStreamId struct (rawCategoryName: string, streamId: StreamId): StreamName =
            render rawCategoryName streamId |> trust
        /// Composes a StreamName from a category and >= 1 name elements.
        /// category is separated from the streamId by '-'; elements are separated from each other by '_'
        let compose (category: string) (rawStreamIdElements: string[]): StreamName =
            ofCategoryAndStreamId (category, StreamId.Elements.join rawStreamIdElements)

    /// Creates a StreamName in the canonical form; a category identifier and an streamId representing the aggregate's identity
    /// category is separated from id by `-`
    let create (category: string) (streamId: StreamId): StreamName =
        Internal.ofCategoryAndStreamId (category, streamId)

    /// <summary>Validates and maps a Stream Name consisting of a Category and an StreamId separated by a '-' (dash).<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to that form.</summary>
    let parse (raw: string): StreamName =
        if raw.IndexOf Category.Separator = -1 then Internal.throwInvalid raw
        Internal.trust raw

    /// <summary>Attempts to split a Stream Name in the form <c>{category}-{streamId}</c> into its two elements.
    /// The <c>{streamId}</c> segment is permitted to include embedded '-' (dash) characters
    /// Returns <c>None</c> if it does not adhere to that form.</summary>
    let trySplit (raw: string): struct (string * StreamId) voption =
        match raw.Split(Category.separator, 2) with
        | [| cat; id |] -> ValueSome struct (cat, StreamId.Elements.trust id)
        | _ -> ValueNone

    /// <summary>Attempts to split a Stream Name in the form <c>{category}-{streamId}</c> into its two elements.
    /// The <c>{streamId}</c> segment is permitted to include embedded '-' (dash) characters
    /// Yields <c>NotCategorized</c> if it does not adhere to that form.</summary>
    let (|Categorized|NotCategorized|) (raw: string): Choice<struct (string * StreamId), unit> =
        match trySplit raw with
        | ValueSome catAndId -> Categorized catAndId
        | ValueNone -> NotCategorized

    /// Extracts the category portion of the StreamName
    let category (x: StreamName) = Category.ofStreamName x
    /// Extracts the category portion of a StreamName
    let (|Category|) = category

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{streamId}</c> into its two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let split (streamName: StreamName): struct (string * StreamId) =
        let rawName = toString streamName
        match trySplit rawName with
        | ValueSome catAndId -> catAndId
        | ValueNone -> Internal.throwInvalid rawName // Yes, it _should_ never happen

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{streamId}</c> into its two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if the stream name is not well-formed.</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let (|Split|): StreamName -> struct (string * StreamId) = split

    /// Yields the StreamId, if the Category matches the specified one
    let tryFind categoryName (x: StreamName): StreamId voption =
        match split x with
        | cat, id when cat = categoryName -> id |> ValueSome
        | _ -> ValueNone
