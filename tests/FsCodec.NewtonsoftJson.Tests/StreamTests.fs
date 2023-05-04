#if SYSTEM_TEXT_JSON
module FsCodec.SystemTextJson.Tests.StreamTests
open FsCodec.SystemTextJson
#else
module FsCodec.NewtonsoftJson.Tests.StreamTests
open FsCodec.NewtonsoftJson
#endif

open Swensen.Unquote
open System.IO
open Xunit

let serdes = Serdes.Default

type Rec = { a : int; b : string; c : string }
let [<Fact>] ``Can serialize/deserialize to stream`` () =
    let value = { a = 10; b = "10"; c = "" }
    use stream = new MemoryStream()
    serdes.SerializeToStream(value, stream)
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    let value' = serdes.DeserializeFromStream(stream)
    test <@ value = value' @>
