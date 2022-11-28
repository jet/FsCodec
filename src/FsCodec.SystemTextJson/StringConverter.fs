namespace FsCodec.SystemTextJson

open System.Text.Json.Serialization

type StringConverter() =
    inherit JsonConverter<string>()

    override _.HandleNull = true
    override _.CanConvert(t) =
        t = typedefof<string>

    override this.Read(reader, typeToConvert, options) =
        let value = reader.GetString()
        if value = null then failwith "Expected string, got null."
        value

    override this.Write(writer, value, options) = writer.WriteStringValue(value)
