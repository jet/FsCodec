namespace FsCodec.SystemTextJson

open System.IO
open System.Text.Json

/// Serializes to/from strings using the supplied Options
type Serdes(options: JsonSerializerOptions) =

    static let def = lazy Serdes Options.Default
    /// Cached shortcut for Serdes Options.Default
    static member Default: Serdes = def.Value

    /// <summary>The <c>JsonSerializerOptions</c> used by this instance.</summary>
    member _.Options: JsonSerializerOptions = options

    /// Serializes given value to a JSON string.
    member _.Serialize<'T>(value: 'T): string =
        JsonSerializer.Serialize<'T>(value, options)

    /// Serializes and writes given value to a stream.
    member _.SerializeToElement<'T>(value: 'T): JsonElement =
        JsonSerializer.SerializeToElement<'T>(value, options)

    /// <summary>Serializes given value to a Byte Array, suitable for wrapping as a <c>ReadOnlyMemory</c>.</summary>
    /// <remarks>NOTE: <c>FsCodec.SystemTextJson.Options.Default</c> defaults to <c>unsafeRelaxedJsonEscaping = false</c></remarks>
    member _.SerializeToUtf8<'T>(value: 'T): byte[] =
        JsonSerializer.SerializeToUtf8Bytes(value, options)

    /// Serializes and writes given value to a stream.
    /// <remarks>NOTE: <c>FsCodec.SystemTextJson.Options.Default</c> defaults to <c>unsafeRelaxedJsonEscaping = false</c>.</remarks>
    member _.SerializeToStream<'T>(value: 'T, utf8Stream: Stream): unit =
        JsonSerializer.Serialize<'T>(utf8Stream, value, options)

    /// Deserializes value of given type from JSON string.
    member _.Deserialize<'T>(json: string): 'T =
        JsonSerializer.Deserialize<'T>(json, options)

    /// Deserializes value of given type from a JsonElement.
    member _.Deserialize<'T>(e: JsonElement): 'T =
        JsonSerializer.Deserialize<'T>(e, options)

    /// Deserializes value of given type from a UTF8 JSON Span.
    member _.Deserialize<'T>(span: System.ReadOnlySpan<byte>): 'T =
        JsonSerializer.Deserialize<'T>(span, options)

    /// Deserializes value of given type from a UTF8 JSON Buffer.
    member x.Deserialize<'T>(utf8json: System.ReadOnlyMemory<byte>): 'T =
        x.Deserialize<'T>(utf8json.Span)

    /// Deserializes value of given type from a (potentially compressed) Encoded JsonElement-based value
    member x.Deserialize<'T>(encoded: Encoded): 'T =
        x.Deserialize<'T>(Encoding.ToJsonElement encoded)

    /// Deserializes value of given type from a (potentially compressed) Encoded value
    member x.Deserialize<'T>(utf8Encoded: FsCodec.Encoded): 'T =
        x.Deserialize<'T>(FsCodec.Encoding.ToBlob utf8Encoded)

    /// Deserializes by reading from a stream.
    member _.DeserializeFromStream<'T>(utf8Stream: Stream): 'T =
        JsonSerializer.Deserialize<'T>(utf8Stream, options)
