namespace FsCodec.SystemTextJson

open System.Runtime.InteropServices
open System.Text.Json

/// Serializes to/from strings using the supplied Options
type Serdes(options : JsonSerializerOptions) =

    /// <summary>The <c>JsonSerializerOptions</c> used by this instance.</summary>
    member _.Options : JsonSerializerOptions = options

    /// Serializes given value to a JSON string.
    member _.Serialize<'T>(value : 'T) =
        JsonSerializer.Serialize<'T>(value, options)

    /// Deserializes value of given type from JSON string.
    member x.Deserialize<'T>(json : string) : 'T =
        JsonSerializer.Deserialize<'T>(json, options)

    /// Serializes given value to a JSON string.
    [<System.Obsolete "Please use non-static Serdes instead">]
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Use indentation when serializing JSON. Defaults to false.
            [<Optional; DefaultParameterValue false>] ?indent : bool) : string =
        let options = (if indent = Some true then Options.Create(indent = true) else Options.Create())
        JsonSerializer.Serialize<'T>(value, options)

    /// Serializes given value to a JSON string with custom options
    [<System.Obsolete "Please use non-static Serdes instead">]
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Options to use (use other overload to use Options.Create() profile)
            options : JsonSerializerOptions) : string =
        JsonSerializer.Serialize<'T>(value, options)

    /// Deserializes value of given type from JSON string.
    [<System.Obsolete "Please use non-static Serdes instead">]
    static member Deserialize<'T>
        (   /// Json string to deserialize.
            json : string,
            /// Options to use (defaults to Options.Create() profile)
            [<Optional; DefaultParameterValue null>] ?options : JsonSerializerOptions) : 'T =
        let settings = options |> Option.defaultWith Options.Create
        JsonSerializer.Deserialize<'T>(json, settings)
