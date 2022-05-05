namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json

/// Serializes to/from strings using the supplied Settings
type Serdes(options : JsonSerializerSettings) =

    /// <summary>The <c>JsonSerializerSettings</c> used by this instance.</summary>
    member _.Options : JsonSerializerSettings = options

    /// Serializes given value to a JSON string.
    member _.Serialize<'T>(value : 'T) =
        JsonConvert.SerializeObject(value, options)

    /// Deserializes value of given type from JSON string.
    member x.Deserialize<'T>(json : string) : 'T =
        JsonConvert.DeserializeObject<'T>(json, options)
