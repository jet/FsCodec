namespace FsCodec.SystemTextJson

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
