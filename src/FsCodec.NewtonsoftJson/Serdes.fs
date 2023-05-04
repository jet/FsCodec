namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System
open System.IO

/// Reuse interim buffers when coding/encoding
// https://stackoverflow.com/questions/55812343/newtonsoft-json-net-jsontextreader-garbage-collector-intensive
module private CharBuffersPool =
    let private inner = System.Buffers.ArrayPool<char>.Shared
    let instance =
        { new IArrayPool<char> with
            member _.Rent minLen = inner.Rent minLen
            member _.Return x = inner.Return x }

// http://www.philosophicalgeek.com/2015/02/06/announcing-microsoft-io-recycablememorystream/
module private Utf8BytesEncoder =
    let private streamManager = Microsoft.IO.RecyclableMemoryStreamManager()
    let rentStream () = streamManager.GetStream("bytesEncoder")
    let wrapAsStream (utf8json : ReadOnlyMemory<byte>) =
        // This is the most efficient way of approaching this without using Spans etc.
        // RecyclableMemoryStreamManager does not have any wins to provide us
        new MemoryStream(utf8json.ToArray(), writable = false)
    let makeJsonReader(ms : MemoryStream) =
        new JsonTextReader(new StreamReader(ms), ArrayPool = CharBuffersPool.instance)
    let private utf8NoBom = System.Text.UTF8Encoding(false, true)
    let makeJsonWriter ms =
        // We need to `leaveOpen` in order to allow .Dispose of the `.rentStream`'d to return it
        let sw = new StreamWriter(ms, utf8NoBom, 1024, leaveOpen = true) // same middle args as StreamWriter default ctor
        new JsonTextWriter(sw, ArrayPool = CharBuffersPool.instance)

/// Serializes to/from strings using the supplied JsonSerializerSettings
type Serdes(options : JsonSerializerSettings) =

    // Cache serializer instance to avoid JsonConvert helpers creating one per call; see
    // https://github.com/JamesNK/Newtonsoft.Json/blob/4dc9af66e07dea321ad101bfb379326127251a80/Src/Newtonsoft.Json/JsonConvert.cs#L817
    let serializer = JsonSerializer.Create(options)

    static let def = lazy Serdes Options.Default
    /// Cached shortcut for Serdes Options.Default
    static member Default : Serdes = def.Value

    /// <summary>The <c>JsonSerializerSettings</c> used by this instance.</summary>
    member _.Options : JsonSerializerSettings = options

    /// Serializes given value to a JSON string.
    member _.Serialize<'T>(value : 'T) : string =
        use sw = new StringWriter(System.Globalization.CultureInfo.InvariantCulture)
        use writer = new JsonTextWriter(sw)
        serializer.Serialize(writer, value)
        sw.ToString()

    /// Serializes given value to a Byte Array, suitable for wrapping as a <c>ReadOnlyMemory</c>.
    member _.SerializeToUtf8(value : 'T) : byte[] =
        use ms = Utf8BytesEncoder.rentStream ()
        (   use jsonWriter = Utf8BytesEncoder.makeJsonWriter ms
            serializer.Serialize(jsonWriter, value, typeof<'T>))
        // TOCONSIDER as noted in the comments on RecyclableMemoryStream.ToArray, ideally we'd be continuing the rental and passing out a Span
        ms.ToArray()

    /// Deserializes value of given type from JSON string.
    member _.Deserialize<'T>(json : string) : 'T =
        use reader = new JsonTextReader(new StringReader(json))
        serializer.Deserialize<'T>(reader)

    /// Deserializes value of given type from a UTF8 JSON Buffer.
    member _.Deserialize<'T>(utf8json : ReadOnlyMemory<byte>) : 'T =
        use ms = Utf8BytesEncoder.wrapAsStream utf8json
        use jsonReader = Utf8BytesEncoder.makeJsonReader ms
        serializer.Deserialize<'T>(jsonReader)

    /// Serializes and writes given value to a stream.
    member _.SerializeToStream<'T>(value : 'T, utf8Stream : Stream) =
        // We're setting CloseOutput = false, because that's the default behavior in STJ
        // but also mostly because it's rude to close without asking
        use streamWriter = new StreamWriter(utf8Stream, System.Text.Encoding.UTF8, 128, leaveOpen = true)
        use writer = new JsonTextWriter(streamWriter, CloseOutput = false)
        serializer.Serialize(writer, value)
        streamWriter.Flush()

    /// Deserializes by reading from a stream.
    member _.DeserializeFromStream<'T>(utf8Stream : Stream) =
        use reader = new JsonTextReader(new StreamReader(utf8Stream, System.Text.Encoding.UTF8))
        serializer.Deserialize<'T>(reader)
