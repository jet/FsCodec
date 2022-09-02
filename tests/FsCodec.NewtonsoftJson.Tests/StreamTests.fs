#if SYSTEM_TEXT_JSON
module FsCodec.SytemTextJson.Tests.StreamTests
open FsCodec.SystemTextJson
#else
module FsCodec.NewtonsoftJson.Tests.StreamTests
open FsCodec.NewtonsoftJson
#endif

open Xunit
open System.IO
open Swensen.Unquote

let serdes = Serdes Options.Default

type Rec = { a : int; b : string; c : string }

let [<Fact>] ``Can serialize/deserialize to stream`` () =
    let value = { a = 10; b = "10"; c = null }
    use stream = new MemoryStream()    
    serdes.SerializeToStream(value, stream)
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    let value' = serdes.DeserializeFromStream(stream)
    <@ value = value' @>
