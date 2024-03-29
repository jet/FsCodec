// StreamName type and module; Manages creation and parsing of well-formed Stream Names
namespace FsCodec

open FSharp.UMX

/// <summary>Lightly-wrapped well-formed Stream Name adhering to one of two forms:<br/>
/// 1. <c>{category}-{streamId}</c><br/>
/// 2. <c>{category}-{id1}_{id2}_...{idN}</c><br/>
/// See <a href="https://github.com/fsprojects/FSharp.UMX" /></summary>
type StreamName = string<streamName>
and [<Measure>] streamName

/// <summary>Creates, Parses and Matches Stream Names in one of two forms:<br/?
/// 1. <c>{category}-{streamId}</c><br.>
/// 2. <c>{category}-{id1}_{id2}_...{idN}</c></summary>
module StreamName =

    /// Strip off the strong typing (In general, it's recommended to pattern match instead)
    /// NOTE As a UMX type, Object.ToString will render equivalent to this
    let inline toString (x: StreamName): string = UMX.untag x

    // Validation helpers, etc.
    module Category =

        let [<Literal>] Separator = '-' // separates {category}-{streamId}
        let [<Literal>] SeparatorStr = "-"
        let internal separator = [| Separator |]

        /// Throws if a candidate category includes a '-', is null, or is empty
        let inline validate (raw: string) =
            if raw |> System.String.IsNullOrEmpty then invalidArg "raw" "Category must not be null or empty"
            if raw.IndexOf Separator <> -1 then invalidArg "raw" "Category must not contain embedded '-' symbols"

        /// Extracts the category portion of the StreamName
        let ofStreamName (x: StreamName) =
            let raw = toString x
            raw.Substring(0, raw.IndexOf Separator)

    /// Extracts the category portion of a StreamName
    let (|Category|): StreamName -> string = Category.ofStreamName

    module Internal =

        /// <summary>Attempts to split a Stream Name in the form <c>{category}-{streamId}</c> into its two elements.<br/>
        /// The <c>{streamId}</c> segment is permitted to include embedded <c>'-'</c> (dash) characters.<br/>
        /// Returns <c>None</c> if it does not adhere to that form.</summary>
        let tryParse (raw: string): struct (string * StreamId) voption =
            match raw.Split(Category.separator, 2) with
            | [| cat; id |] -> ValueSome struct (cat, StreamId.Elements.trust id)
            | _ -> ValueNone

        /// <summary>Attempts to split a Stream Name in the form <c>{category}-{streamId}</c> into its two elements..<br/>
        /// The <c>{streamId}</c> segment is permitted to include embedded '-' (dash) characters.<br/>
        /// Yields <c>NotCategorized</c> if it does not adhere to that form.</summary>
        let (|Categorized|NotCategorized|) (raw: string): Choice<struct (string * StreamId), unit> =
            match tryParse raw with
            | ValueSome catAndId -> Categorized catAndId
            | ValueNone -> NotCategorized

        let throwInvalid raw = invalidArg "raw" (sprintf "Stream Name '%s' must contain a '-' separator" raw)

        /// Create a StreamName, trusting the input to be well-formed
        let trust (raw: string): StreamName = UMX.tag raw

    /// <summary>Validates and maps a Stream Name consisting of a Category and an StreamId separated by a '-' (dash).<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to that form.</summary>
    let parse (raw: string): StreamName =
        if raw.IndexOf Category.Separator = -1 then Internal.throwInvalid raw
        raw |> Internal.trust

    /// <summary>Creates a StreamName in the canonical form; a category identifier and an streamId representing the aggregate's identity
    /// category is separated from id by <c>`-`</c></summary>
    let create (category: string) (streamId: StreamId): StreamName =
        Category.validate category
        System.String.Concat(category, Category.SeparatorStr, StreamId.toString streamId) |> Internal.trust

    /// <summary>Composes a StreamName from a category and >= 0 name elements..<br/>
    /// category is separated from the streamId by '-'; elements are separated from each other by '_'.<br/>
    /// Throws <c>InvalidArgumentException</c> if category embeds '-' symbols, or elements embed '_' symbols.</summary>
    let compose (categoryName: string) (streamIdElements: string[]): StreamName =
        create categoryName (StreamId.Elements.compose streamIdElements)

    /// <summary>Splits a well-formed Stream Name of the form <c>{category}-{streamId}</c> into its two elements.<br/>
    /// Throws <c>InvalidArgumentException</c> if it does not adhere to the well known format (i.e. if it was not produced by `parse`).</summary>
    /// <remarks>Inverse of <c>create</c></remarks>
    let split (streamName: StreamName): struct (string * StreamId) =
        let rawName = toString streamName
        match Internal.tryParse rawName with
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
