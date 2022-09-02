namespace FsCodec.SystemTextJson

open System.IO
open System.Text.Json

/// Serializes to/from strings using the supplied Options
type Serdes(options : JsonSerializerOptions) =
    
    /// <summary>The <c>JsonSerializerOptions</c> used by this instance.</summary>
    member _.Options : JsonSerializerOptions = options

    /// Serializes given value to a JSON string.
    member _.Serialize<'T>(value : 'T) =
        JsonSerializer.Serialize<'T>(value, options)

    /// Deserializes value of given type from JSON string.
    member _.Deserialize<'T>(json : string) : 'T =
        JsonSerializer.Deserialize<'T>(json, options)

    /// Serializes and writes given value to a stream.
    member _.SerializeToStream<'T>(value : 'T, utf8Stream : Stream) =
        JsonSerializer.Serialize<'T>(utf8Stream, value, options)

    /// Deserializes by reading from a stream.
    member _.DeserializeFromStream<'T>(utf8Stream : Stream) =
        JsonSerializer.Deserialize<'T>(utf8Stream, options)

