namespace FsCodec.SystemTextJson

open System.Text.Json.Serialization

type RejectNullStringConverter() =
    inherit JsonConverter<string>()

    override _.HandleNull = true
    override _.CanConvert(t) = t = typeof<string>

    override this.Read(reader, _typeToConvert, _options) =
        let value = reader.GetString()
        if value = null then nullArg "Expected string, got null. When allowNullStrings is false you must explicitly type optional strings as 'string option'"
        value

    override this.Write(writer, value, _options) =
        if value = null then nullArg "Expected string, got null. When allowNullStrings is false you must explicitly type optional strings as 'string option'"
        writer.WriteStringValue(value)
