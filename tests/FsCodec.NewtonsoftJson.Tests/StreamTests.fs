#if SYSTEM_TEXT_JSON
module FsCodec.SytemTextJson.Tests.StreamTests
open FsCodec.SystemTextJson
#else
module FsCodec.NewtonsoftJson.Tests.StreamTests
open FsCodec.NewtonsoftJson
#endif

open Swensen.Unquote
open System.IO
open Xunit

type Rec = { a : int; b : string; c : string }

let [<Fact>] ``Can serialize/deserialize to stream`` () =
    let value = { a = 10; b = "10"; c = "" }
    use stream = new MemoryStream()
    Serdes.Default.SerializeToStream(value, stream)
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    let value' = Serdes.Default.DeserializeFromStream(stream)
    test <@ value = value' @>
