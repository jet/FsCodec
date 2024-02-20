namespace FsCodec.SystemTextJson

type RejectNullStringConverter() =
    inherit System.Text.Json.Serialization.JsonConverter<string>()

    let  [<Literal>] message = "Expected string, got null. When allowNullStrings is false you must explicitly type optional strings as 'string option'"

    override _.HandleNull = true
    override _.CanConvert t = t = typeof<string>

    override this.Read(reader, _typeToConvert, _options) =
        let value = reader.GetString()
        if value = null then nullArg message
        value

    override this.Write(writer, value, _options) =
        if value = null then nullArg message
        writer.WriteStringValue(value)
