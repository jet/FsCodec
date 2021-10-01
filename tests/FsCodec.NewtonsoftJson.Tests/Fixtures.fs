module FsCodec.NewtonsoftJson.Tests.Fixtures

open FsCodec.NewtonsoftJson // JsonIsomorphism
open Newtonsoft.Json // JsonConverter
open System
open System.Runtime.Serialization

/// Endows any type that inherits this class with standard .NET comparison semantics using a supplied token identifier
[<AbstractClass>]
type Comparable<'TComp, 'Token when 'TComp :> Comparable<'TComp, 'Token> and 'Token : comparison>(token : 'Token) =
    member private _.Token = token // I can haz protected?
    override x.Equals y = match y with :? Comparable<'TComp, 'Token> as y -> x.Token = y.Token | _ -> false
    override _.GetHashCode() = hash token
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
    member _.Value = id
    override _.ToString () = id
    new (guid: Guid) = SkuId (guid.ToString("N"))
    // NB tests (specifically, empty) lean on having a ctor of this shape
    new() = SkuId(Guid.NewGuid())
    // NB for validation [and XSS] purposes we prove it translatable to a Guid
    static member Parse(input: string) = SkuId (Guid.Parse input)
/// Represent as a Guid.ToString("N") output externally
and private SkuIdJsonConverter() =
    inherit JsonIsomorphism<SkuId, string>()
    /// Renders as per Guid.ToString("N")
    override _.Pickle value = value.Value
    /// Input must be a Guid.Parseable value
    override _.UnPickle input = SkuId.Parse input

/// CartId strongly typed id
[<Sealed; JsonConverter(typeof<CartIdJsonConverter>); AutoSerializable(false); StructuredFormatDisplay("{Value}")>]
// (Internally a string for most efficient copying semantics)
type CartId private (id : string) =
    inherit Comparable<CartId, string>(id)
    [<IgnoreDataMember>] // Prevent swashbuckle inferring there's a "value" field
    member _.Value = id
    override _.ToString () = id
    // NB tests lean on having a ctor of this shape
    new (guid: Guid) = CartId (guid.ToString("N"))
    // NB for validation [and XSS] purposes we must prove it translatable to a Guid
    static member Parse(input: string) = CartId (Guid.Parse input)
/// Represent as a Guid.ToString("N") output externally
and private CartIdJsonConverter() =
    inherit JsonIsomorphism<CartId, string>()
    /// Renders as per Guid.ToString("N")
    override _.Pickle value = value.Value
    /// Input must be a Guid.Parseable value
    override _.UnPickle input = CartId.Parse input
