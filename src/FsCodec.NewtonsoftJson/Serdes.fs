namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System.IO
open System.Text

/// Serializes to/from strings using the supplied JsonSerializerSettings
type Serdes(options : JsonSerializerSettings) =
    // Why are we creating a serializer here instead of using JsonConvert.Serialize?
    // Because, under the hood, JsonConvert creates a serializer instance for *each* call
    // Source:
    // https://github.com/JamesNK/Newtonsoft.Json/blob/4dc9af66e07dea321ad101bfb379326127251a80/Src/Newtonsoft.Json/JsonConvert.cs#L817
    // It's consistent with the stream implementation
    let serializer = JsonSerializer.Create(options)

    /// <summary>The <c>JsonSerializerSettings</c> used by this instance.</summary>
    member _.Options : JsonSerializerSettings = options

    /// Serializes given value to a JSON string.
    member _.Serialize<'T>(value : 'T) : string =
        use sw = new StringWriter()
        use writer = new JsonTextWriter(sw)
        serializer.Serialize(writer, value)
        sw.ToString()

    /// Deserializes value of given type from JSON string.
    member _.Deserialize<'T>(json : string) : 'T =
        use reader = new JsonTextReader(new StringReader(json))
        serializer.Deserialize<'T>(reader)

    /// Serializes and writes given value to a stream.
    member _.SerializeToStream<'T>(value : 'T, utf8Stream : Stream) =
        use writer = new JsonTextWriter(new StreamWriter(utf8Stream, Encoding.UTF8), CloseOutput = false)
        serializer.Serialize(writer, value)

    /// Deserializes by reading from a stream.
    member x.DeserializeFromStream<'T>(utf8Stream : Stream) =
        use reader = new JsonTextReader(new StreamReader(utf8Stream, Encoding.UTF8))
        serializer.Deserialize<'T>(reader)
