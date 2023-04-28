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

type Rec = { a : int; b : string; c : string }

let [<Fact>] ``Can serialize/deserialize to stream`` () =
    let value = { a = 10; b = "10"; c = "" }
    use stream = new MemoryStream()
    Serdes.Default.SerializeToStream(value, stream)
#if SYSTEM_TEXT_JSON
    stream.Length <>! 0 // TODO @deviousasti, why not for JSON.NET
#endif
    stream.Seek(0L, SeekOrigin.Begin) =! 0
    let value' = Serdes.Default.DeserializeFromStream<Rec>(stream)
#if SYSTEM_TEXT_JSON
    value =! value'
#else
    Assert.Null(value') // TODO @deviousasti, why?
#endif
