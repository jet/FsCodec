namespace FsCodec.NewtonsoftJson

/// <summary>Implements conversion to/from <c>string</c> for a <c>FsCodec.StringId</c>-derived type.</summary>
[<AbstractClass>]
type StringIdConverter<'T when 'T :> FsCodec.StringId<'T> >(parse: string -> 'T) =
    inherit JsonIsomorphism<'T, string>()
    override _.Pickle value = value.ToString()
    override _.UnPickle input = parse input
