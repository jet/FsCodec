namespace FsCodec.SystemTextJson

open System.Text.Json.Serialization

module private Error =
    [<Literal>]
    let private message = "Expected string, got null. When allowNullStrings is false you must explicitly type optional strings as 'string option'"

type RejectNullStringConverter() =
    inherit JsonConverter<string>()

    override _.HandleNull = true
    override _.CanConvert(t) = t = typeof<string>

    override this.Read(reader, _typeToConvert, _options) =
        let value = reader.GetString()
        if value = null then nullArg Error.message
        value

    override this.Write(writer, value, _options) =
        if value = null then nullArg Error.message
        writer.WriteStringValue(value)
