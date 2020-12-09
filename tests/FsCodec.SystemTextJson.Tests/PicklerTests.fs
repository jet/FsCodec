module FsCodec.SystemTextJson.Tests.PicklerTests

open FsCodec.SystemTextJson
open FsCodec.SystemTextJson.Converters
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
    override __.Pickle g = g.ToString "N"
    override __.UnPickle g = Guid.Parse g

type WithEmbeddedGuid = { a: string; [<Serialization.JsonConverter(typeof<GuidConverter>)>] b: Guid }

type Configs() as this =
    inherit TheoryData<JsonSerializerOptions>()
    do  this.Add(Options.CreateDefault()) // validate it works with minimal converters
        this.Add(Options.Create()) // Flush out clashes with standard converter set
        this.Add(Options.Create(GuidConverter())) // and a global registration does not conflict

let [<Theory; ClassData(typeof<Configs>)>] ``Tagging with GuidConverter roundtrips`` (options : JsonSerializerOptions) =
    let value = { a = "testing"; b = Guid.Empty }

    let result = Serdes.Serialize(value, options)

    test <@ """{"a":"testing","b":"00000000000000000000000000000000"}""" = result @>

    let des = Serdes.Deserialize(result, options)
    test <@ value = des @>

let [<Fact>] ``Global GuidConverter roundtrips`` () =
    let value = Guid.Empty

    let defaultHandlingHasDashes = Serdes.Serialize value

    let optionsWithConverter = Options.Create(GuidConverter())
    let resNoDashes = Serdes.Serialize(value, optionsWithConverter)

    test <@ "\"00000000-0000-0000-0000-000000000000\"" = defaultHandlingHasDashes
            && "\"00000000000000000000000000000000\"" = resNoDashes @>

    // Non-dashed is not accepted by default handling in STJ (Newtonsoft does accept it)
    raises<exn> <@ Serdes.Deserialize<Guid> resNoDashes @>

    // With the converter, things roundtrip either way
    for result in [defaultHandlingHasDashes; resNoDashes] do
        let des = Serdes.Deserialize(result, optionsWithConverter)
        test <@ value= des @>
