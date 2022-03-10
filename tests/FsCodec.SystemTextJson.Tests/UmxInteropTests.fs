/// There's not much to see here - as UMX is a compile-time thing, it should work perfectly with System.Text.Json
module FsCodec.SystemTextJson.Tests.UmxInteropTests

open FsCodec.SystemTextJson
open FSharp.UMX
open Swensen.Unquote
open System
open System.Text.Json
open Xunit

// Borrow the converter from the suite that has validated its' core behaviors
type GuidConverter = PicklerTests.GuidConverter

type [<Measure>] myGuid
type MyGuid = Guid<myGuid>
type WithEmbeddedMyGuid =
    { a: string

      [<Serialization.JsonConverter(typeof<GuidConverter>)>]
      b: MyGuid }

type Configs() as this =
    inherit TheoryData<string, JsonSerializerOptions>()
    do  this.Add("\"00000000-0000-0000-0000-000000000000\"", Options.Default)
        this.Add("\"00000000000000000000000000000000\"",     Options.Create(GuidConverter()))

let [<Theory; ClassData(typeof<Configs>)>]
    ``UMX'd Guid interops with GuidConverter and roundtrips``
    (expectedSer, options : JsonSerializerOptions) =

    let value = Guid.Empty

    let serdes = Serdes options
    let result = serdes.Serialize value
    test <@ expectedSer = result @>
    let des = serdes.Deserialize result
    test <@ value = des @>
