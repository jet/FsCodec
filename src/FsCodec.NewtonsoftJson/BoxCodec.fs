/// Fork of FsCodec.NewtonsoftJson.Codec intended to provide equivalent calls and functionality, without actually serializing/deserializing as JSON
/// This is a useful facility for in-memory stores such as Equinox's MemoryStore as it enables you to
/// - efficiently test behaviors from an event sourced decision processing perspective (e.g. with Property Based Tests)
/// - without paying a serialization cost and/or having to deal with sanitization of generated data in order to make it roundtrippable through same
namespace FsCodec.Box

open System
open System.Runtime.InteropServices

/// Provides Codecs that extract the Event bodies from a Union, using the conventions implied by using <c>TypeShape.UnionContract.UnionContractEncoder</c>
/// If you need full control and/or have have your own codecs, see <c>FsCodec.Codec.Create</c> instead
/// See <a href=""https://github.com/eiriktsarpalis/TypeShape/blob/master/tests/TypeShape.Tests/UnionContractTests.fs"></a> for example usage.
type Codec private () =

    /// Generate a <code>IUnionEncoder</code> Codec that roundtrips events by holding the boxed form of the Event body.
    /// Uses <c>up</c> and <c>down</c> functions to facilitate upconversion/downconversion
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Union</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union,'Contract,'Meta,'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the <c>'Union</c> representation (typically a Discriminated Union) that is to be presented to the programming model.
            up : FsCodec.ITimelineEvent<obj> * 'Contract -> 'Union,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive a <c>meta</c> object that will be held alongside the data (if it's not <c>None</c>)
            ///   together with its <c>correlationId</c>, <c>causationId</c> and an Event Creation <c>timestamp</c> (defaults to <c>UtcNow</c>).
            down : 'Context option * 'Union -> 'Contract * 'Meta option * string * string * DateTimeOffset option,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union,obj,'Context> =
        let boxEncoder : TypeShape.UnionContract.IEncoder<obj> = new TypeShape.UnionContract.BoxEncoder() :> _
        let dataCodec =
            TypeShape.UnionContract.UnionContractEncoder.Create<'Contract,obj>(
                boxEncoder,
                requireRecordFields=true,
                allowNullaryCases=not (defaultArg rejectNullaryCases false))
        { new FsCodec.IUnionEncoder<'Union,obj,'Context> with
            member __.Encode(context,u) =
                let (c, meta : 'Meta option, correlationId, causationId, timestamp : DateTimeOffset option) = down (context,u)
                let enc = dataCodec.Encode c
                let meta = meta |> Option.map boxEncoder.Encode<'Meta>
                FsCodec.Core.EventData.Create(enc.CaseName, enc.Payload, defaultArg meta null, correlationId, causationId, ?timestamp=timestamp) :> _
            member __.TryDecode encoded =
                let cOption = dataCodec.TryDecode { CaseName = encoded.EventType; Payload = encoded.Data }
                match cOption with None -> None | Some contract -> let union = up (encoded,contract) in Some union }

    /// Generate a <code>IUnionEncoder</code> Codec that roundtrips events by holding the boxed form of the Event body.
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Union</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union,'Contract,'Meta,'Context when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.
            up : FsCodec.ITimelineEvent<obj> * 'Contract -> 'Union,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            down : 'Union -> 'Contract * 'Meta option * DateTimeOffset option,
            /// Uses the 'Context passed to the Encode call and the 'Meta emitted by <c>down</c> to a) the final metadata b) the <c>correlationId</c> and c) the correlationId
            mapCausation : 'Context option * 'Meta option -> 'Meta option * string * string,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union,obj,'Context> =
        let down (context,union) =
            let c, m, t = down union
            let m', correlationId, causationId = mapCausation (context,m)
            c, m', correlationId, causationId, t
        Codec.Create(up=up, down=down, ?rejectNullaryCases=rejectNullaryCases)

    /// Generate a <code>IUnionEncoder</code> Codec that roundtrips events by holding the boxed form of the Event body.
    /// Uses <c>up</c> and <c>down</c> and <c>mapCausation</c> functions to facilitate upconversion/downconversion and correlation/causationId mapping
    ///   and/or surfacing metadata to the Programming Model by including it in the emitted <c>'Union</c>
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>Contract</c> must be tagged with </c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union,'Contract,'Meta when 'Contract :> TypeShape.UnionContract.IUnionContract>
        (   /// Maps from the TypeShape <c>UnionConverter</c> <c>'Contract</c> case the Event has been mapped to (with the raw event data as context)
            /// to the representation (typically a Discriminated Union) that is to be presented to the programming model.
            up : FsCodec.ITimelineEvent<obj> * 'Contract -> 'Union,
            /// Maps a fresh Event resulting from a Decision in the Domain representation type down to the TypeShape <c>UnionConverter</c> <c>'Contract</c>
            /// The function is also expected to derive
            ///   a <c>meta</c> object that will be serialized with the same settings (if it's not <c>None</c>)
            ///   and an Event Creation <c>timestamp</c>.
            down : 'Union -> 'Contract * 'Meta option * DateTimeOffset option,
            /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union,obj,obj> =
        let mapCausation (_context : obj, m : ' Meta option) = m,null,null
        Codec.Create(up=up, down=down, mapCausation=mapCausation, ?rejectNullaryCases=rejectNullaryCases)

    /// Generate a <code>IUnionEncoder</code> Codec that roundtrips events by holding the boxed form of the Event body.
    /// The Event Type Names are inferred based on either explicit <c>DataMember(Name=</c> Attributes, or (if unspecified) the Discriminated Union Case Name
    /// <c>'Union</c> must be tagged with <c>interface TypeShape.UnionContract.IUnionContract</c> to signify this scheme applies.
    static member Create<'Union when 'Union :> TypeShape.UnionContract.IUnionContract>
        (   /// Enables one to fail encoder generation if union contains nullary cases. Defaults to <c>false</c>, i.e. permitting them
            [<Optional;DefaultParameterValue(null)>]?rejectNullaryCases)
        : FsCodec.IUnionEncoder<'Union, obj, obj> =
        let up : FsCodec.ITimelineEvent<_> * 'Union -> 'Union = snd
        let down (u : 'Union) = u, None, None
        Codec.Create(up=up, down=down, ?rejectNullaryCases=rejectNullaryCases)