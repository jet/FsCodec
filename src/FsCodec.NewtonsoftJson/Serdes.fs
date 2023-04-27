namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System.IO

/// Serializes to/from strings using the supplied JsonSerializerSettings
type Serdes(options : JsonSerializerSettings) =

    // Cache serializer instance to avoid JsonConvert helpers creating one per call; see
    // https://github.com/JamesNK/Newtonsoft.Json/blob/4dc9af66e07dea321ad101bfb379326127251a80/Src/Newtonsoft.Json/JsonConvert.cs#L817
    let serializer = JsonSerializer.Create(options)

    /// <summary>The <c>JsonSerializerSettings</c> used by this instance.</summary>
    member _.Options : JsonSerializerSettings = options

    /// Serializes given value to a JSON string.
    member _.Serialize<'T>(value : 'T) : string =
        use sw = new StringWriter(System.Globalization.CultureInfo.InvariantCulture)
        use writer = new JsonTextWriter(sw)
        serializer.Serialize(writer, value)
        sw.ToString()

    /// Deserializes value of given type from JSON string.
    member _.Deserialize<'T>(json : string) : 'T =
        use reader = new JsonTextReader(new StringReader(json))
        serializer.Deserialize<'T>(reader)

    /// Deserializes value of given type from a UTF8 JSON Span.
    member _.Deserialize<'T>(utf8json : System.ReadOnlyMemory<byte>) : 'T =
        use stream = new MemoryStream(utf8json.ToArray(), writable = false) // see Utf8BytesEncoder.wrapAsStream
        use reader = new JsonTextReader(new StreamReader(stream, System.Text.Encoding.UTF8))
        serializer.Deserialize<'T>(reader)

    /// Serializes and writes given value to a stream.
    member _.SerializeToStream<'T>(value : 'T, utf8Stream : Stream) =
        // We're setting CloseOutput = false, because that's the default behavior in STJ
        // but also mostly because it's rude to close without asking
        use writer = new JsonTextWriter(new StreamWriter(utf8Stream, System.Text.Encoding.UTF8), CloseOutput = false)
        serializer.Serialize(writer, value)

    /// Deserializes by reading from a stream.
    member _.DeserializeFromStream<'T>(utf8Stream : Stream) =
        use reader = new JsonTextReader(new StreamReader(utf8Stream, System.Text.Encoding.UTF8))
        serializer.Deserialize<'T>(reader)
