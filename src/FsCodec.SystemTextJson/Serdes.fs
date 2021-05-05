namespace FsCodec.SystemTextJson

open System.Runtime.InteropServices
open System.Text.Json

/// Serializes to/from strings using the Options arising from a call to <c>Options.Create()</c>
type Serdes private () =

    static let defaultOptions = lazy Options.Create()
    static let indentOptions = lazy Options.Create(indent = true)

    /// Yields the settings used by <c>Serdes</c> when no <c>options</c> are supplied.
    static member DefaultOptions : JsonSerializerOptions = defaultOptions.Value

    /// Serializes given value to a JSON string.
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Use indentation when serializing JSON. Defaults to false.
            [<Optional; DefaultParameterValue null>] ?indent : bool) : string =
        let options = (if defaultArg indent false then indentOptions else defaultOptions).Value
        Serdes.Serialize(value, options)

    /// Serializes given value to a JSON string with custom options
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Options to use (use other overload to use Options.Create() profile)
            options : JsonSerializerOptions) : string =
        JsonSerializer.Serialize(value, options)

    /// Deserializes value of given type from JSON string.
    static member Deserialize<'T>
        (   /// Json string to deserialize.
            json : string,
            /// Options to use (defaults to Options.Create() profile)
            [<Optional; DefaultParameterValue null>] ?options : JsonSerializerOptions) : 'T =
        let settings = match options with None -> defaultOptions.Value | Some x -> x
        JsonSerializer.Deserialize<'T>(json, settings)
