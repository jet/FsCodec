namespace FsCodec.SystemTextJson

/// <summary>Implements conversion to/from <c>string</c> for a <c>FsCodec.StringId</c>-derived type.</summary>
[<AbstractClass>]
type StringIdConverter<'T when 'T :> FsCodec.StringId<'T> >(parse: string -> 'T) =
    inherit System.Text.Json.Serialization.JsonConverter<'T>()
    override _.Write(writer, value, _options) = value.ToString() |> writer.WriteStringValue
    override _.Read(reader, _type, _options) = reader.GetString() |> parse

/// <summary>Implements conversion to/from <c>string</c> for a <c>FsCodec.StringId</c>-derived type.<br/>
/// Opts into use of the underlying token as a valid property name when tth type is used as a Key in a <c>IDictionary</c>.</summary>
[<AbstractClass>]
type StringIdOrDictionaryKeyConverter<'T when 'T :> FsCodec.StringId<'T> >(parse: string -> 'T) =
    inherit System.Text.Json.Serialization.JsonConverter<'T>()
    override _.Write(writer, value, _options) = value.ToString() |> writer.WriteStringValue
    override _.WriteAsPropertyName(writer, value, _options) = value.ToString() |> writer.WritePropertyName
    override _.Read(reader, _type, _options) = reader.GetString() |> parse
    override _.ReadAsPropertyName(reader, _type, _options) = reader.GetString() |> parse
