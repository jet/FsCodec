namespace FsCodec.NewtonsoftJson

open Newtonsoft.Json
open System.Runtime.InteropServices

/// Serializes to/from strings using the settings arising from a call to <c>Settings.Create()</c>
type Serdes private () =

    static let defaultSettings = lazy Settings.Create()
    static let indentSettings = lazy Settings.Create(indent = true)

    /// Serializes given value to a JSON string.
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Use indentation when serializing JSON. Defaults to false.
            [<Optional; DefaultParameterValue null>]?indent : bool) : string =
        let settings = (if defaultArg indent false then indentSettings else defaultSettings).Value
        JsonConvert.SerializeObject(value, settings)

    /// Serializes given value to a JSON string with custom settings
    static member Serialize<'T>
        (   /// Value to serialize.
            value : 'T,
            /// Settings to use (use other overload to use Settings.Create() profile)
            settings : JsonSerializerSettings) : string =
        JsonConvert.SerializeObject(value, settings)

    /// Deserializes value of given type from JSON string.
    static member Deserialize<'T>
        (   /// Json string to deserialize.
            json : string,
            /// Settings to use (defaults to Settings.Create() profile)
            [<Optional; DefaultParameterValue null>]?settings : JsonSerializerSettings) : 'T =
        let settings = match settings with None -> defaultSettings.Value | Some x -> x
        JsonConvert.DeserializeObject<'T>(json, settings)
