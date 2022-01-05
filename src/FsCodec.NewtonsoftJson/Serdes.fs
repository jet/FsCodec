namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System.Runtime.InteropServices

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

    /// Serializes given value to a JSON string.
    [<System.Obsolete "Please use non-static Serdes instead">]
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Use indentation when serializing JSON. Defaults to false.
            [<Optional; DefaultParameterValue false>] ?indent : bool) : string =
        let options = (if indent = Some true then Settings.Create(indent = true) else Settings.Create())
        JsonConvert.SerializeObject(value, options)

    /// Serializes given value to a JSON string with custom options
    [<System.Obsolete "Please use non-static Serdes instead">]
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Settings to use (use other overload to use Settings.Create() profile)
            settings : JsonSerializerSettings) : string =
        JsonConvert.SerializeObject(value, settings)

    /// Deserializes value of given type from JSON string.
    [<System.Obsolete "Please use non-static Serdes instead">]
    static member Deserialize<'T>
        (   /// Json string to deserialize.
            json : string,
            /// Settings to use (defaults to Settings.Create() profile)
            [<Optional; DefaultParameterValue null>] ?settings : JsonSerializerSettings) : 'T =
        let settings = match settings with Some x -> x | None -> Settings.Create()
        JsonConvert.DeserializeObject<'T>(json, settings)
