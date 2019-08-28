namespace Newtonsoft.Json.Converters.FSharp.Tests

open Newtonsoft.Json.Converters.FSharp
open Newtonsoft.Json
open Swensen.Unquote
open System
open System.Runtime.Serialization
open Xunit

/// Endows any type that inherits this class with standard .NET comparison semantics using a supplied token identifier
[<AbstractClass>]
type Comparable<'TComp, 'Token when 'TComp :> Comparable<'TComp, 'Token> and 'Token : comparison>(token : 'Token) =
    member private __.Token = token // I can haz protected?
    override x.Equals y = match y with :? Comparable<'TComp, 'Token> as y -> x.Token = y.Token | _ -> false
    override __.GetHashCode() = hash token
    interface IComparable with
        member x.CompareTo y =
            match y with
            | :? Comparable<'TComp, 'Token> as y -> compare x.Token y.Token
            | _ -> invalidArg "y" "invalid comparand"

/// SkuId strongly typed id
[<Sealed; JsonConverter(typeof<SkuIdJsonConverter>); AutoSerializable(false); StructuredFormatDisplay("{Value}")>]
// (Internally a string for most efficient copying semantics)
type SkuId private (id : string) =
    inherit Comparable<SkuId, string>(id)
    [<IgnoreDataMember>] // Prevent swashbuckle inferring there's a "value" field
    member __.Value = id
    override __.ToString () = id
    new (guid: Guid) = SkuId (guid.ToString("N"))
    // NB tests (specifically, empty) lean on having a ctor of this shape
    new() = SkuId(Guid.NewGuid())
    // NB for validation [and XSS] purposes we prove it translatable to a Guid
    static member Parse(input: string) = SkuId (Guid.Parse input)
/// Represent as a Guid.ToString("N") output externally
and private SkuIdJsonConverter() =
    inherit JsonIsomorphism<SkuId, string>()
    /// Renders as per Guid.ToString("N")
    override __.Pickle value = value.Value
    /// Input must be a Guid.Parseable value
    override __.UnPickle input = SkuId.Parse input

/// CartId strongly typed id
[<Sealed; JsonConverter(typeof<CartIdJsonConverter>); AutoSerializable(false); StructuredFormatDisplay("{Value}")>]
// (Internally a string for most efficient copying semantics)
type CartId private (id : string) =
    inherit Comparable<CartId, string>(id)
    [<IgnoreDataMember>] // Prevent swashbuckle inferring there's a "value" field
    member __.Value = id
    override __.ToString () = id
    // NB tests lean on having a ctor of this shape
    new (guid: Guid) = CartId (guid.ToString("N"))
    // NB for validation [and XSS] purposes we must prove it translatable to a Guid
    static member Parse(input: string) = CartId (Guid.Parse input)
/// Represent as a Guid.ToString("N") output externally
and private CartIdJsonConverter() =
    inherit JsonIsomorphism<CartId, string>()
    /// Renders as per Guid.ToString("N")
    override __.Pickle value = value.Value
    /// Input must be a Guid.Parseable value
    override __.UnPickle input = CartId.Parse input

module JsonIsomorphismTests =

    // NB Feel free to ignore this opinion and copy the 4 lines into your own globals - the pinning test will remain here
    /// <summary>
    ///     Renders all Guids without dashes.
    /// </summary>
    /// <remarks>
    ///     Can work correctly as a global converter, as some codebases do for historical reasons
    ///     Could arguably be usable as base class for various converters, including the above.
    ///     However, the above pattern and variants thereof are recommended for new types.
    ///     In general, the philosophy is that, beyond the Pickler base types, an identiy type should consist of explicit
    ///       code as much as possible, and global converters really have to earn their keep - magic starts with -100 points.
    /// </remarks>
    type GuidConverter() =
        inherit JsonIsomorphism<Guid, string>()
        override __.Pickle g = g.ToString "N"
        override __.UnPickle g = Guid.Parse g

    type WithEmbeddedGuid = { a: string; [<JsonConverter(typeof<GuidConverter>)>] b: Guid }

    let [<Fact>] ``Tagging with GuidConverter`` () =
        let value = { a = "testing"; b = Guid.Empty }

        let result = JsonConvert.SerializeObject value

        test <@ """{"a":"testing","b":"00000000000000000000000000000000"}""" = result @>

    let [<Fact>] ``Global GuidConverter`` () =
        let value = Guid.Empty

        let resDashes = JsonConvert.SerializeObject(value, Settings.Create())
        let resNoDashes = JsonConvert.SerializeObject(value, Settings.Create(GuidConverter()))

        test <@ "\"00000000-0000-0000-0000-000000000000\"" = resDashes
                && "\"00000000000000000000000000000000\"" = resNoDashes @>