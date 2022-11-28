namespace FsCodec.SystemTextJson

open System.Text.Json
open System.Text.Json.Serialization

type StringConverter() =
    inherit JsonConverter<string>()

    override _.HandleNull = true
    override _.CanConvert(t) = t = typeof<string>

    override this.Read(reader, _typeToConvert, _options) =
        let value = reader.GetString()
        if value = null then failwith "Expected string, got null."
        value

    override this.Write(writer, value, _options) =
        if value = null then failwith "Expected string, got null."
        writer.WriteStringValue(value)
