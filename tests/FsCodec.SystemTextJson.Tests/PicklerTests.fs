module FsCodec.SystemTextJson.Tests.PicklerTests

open FsCodec.SystemTextJson
open Swensen.Unquote
open System
open System.Text.Json
open Xunit

// NB Feel free to ignore this opinion and copy the 4 lines into your own globals - the pinning test will remain here
/// <summary>
///     Renders Guids without dashes.
/// </summary>
/// <remarks>
///     Can work correctly as a global converter, as some codebases do for historical reasons
///     Could arguably be usable as base class for various converters, including the above.
///     However, both of these usage patterns and variants thereof are not recommended for new types.
///     In general, the philosophy is that, beyond the Pickler base types, an identity type should consist of explicit
///       code as much as possible, and global converters really have to earn their keep - magic starts with -100 points.
/// </remarks>
type GuidConverter() =
    inherit JsonIsomorphism<Guid, string>()
    override _.Pickle g = g.ToString "N"
    override _.UnPickle g = Guid.Parse g

type WithEmbeddedGuid = { a: string; [<Serialization.JsonConverter(typeof<GuidConverter>)>] b: Guid }

type Configs() as this =
    inherit TheoryData<JsonSerializerOptions>()
    do  this.Add(Options.CreateDefault()) // validate it works with minimal converters
        this.Add(Options.Default) // Flush out clashes with standard converter set
        this.Add(Options.Create()) // Flush out clashes with standard converter set
        this.Add(Options.Create(GuidConverter())) // and a global registration does not conflict

let [<Theory; ClassData(typeof<Configs>)>] ``Tagging with GuidConverter roundtrips`` (options : JsonSerializerOptions) =
    let value = { a = "testing"; b = Guid.Empty }
    let serdes = Serdes options
    let result = serdes.Serialize value

    test <@ """{"a":"testing","b":"00000000000000000000000000000000"}""" = result @>

    let des = serdes.Deserialize result
    test <@ value = des @>

let serdes = Serdes(Options.Default)

let [<Fact>] ``Global GuidConverter roundtrips`` () =
    let value = Guid.Empty

    let defaultHandlingHasDashes = serdes.Serialize value

    let serdesWithConverter = Options.Create(GuidConverter()) |> Serdes
    let resNoDashes = serdesWithConverter.Serialize value

    test <@ "\"00000000-0000-0000-0000-000000000000\"" = defaultHandlingHasDashes
            && "\"00000000000000000000000000000000\"" = resNoDashes @>

    // Non-dashed is not accepted by default handling in STJ (Newtonsoft does accept it)
    raises<exn> <@ serdes.Deserialize<Guid> resNoDashes @>

    // With the converter, things roundtrip either way
    for result in [defaultHandlingHasDashes; resNoDashes] do
        let des = serdesWithConverter.Deserialize result
        test <@ value = des @>
