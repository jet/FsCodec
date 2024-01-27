namespace FsCodec.SystemTextJson

open System.Text.Json.Serialization

type UnionOrTypeSafeEnumConverterFactory(typeSafeEnum, union) =
    inherit JsonConverterFactory()

    static let hasConverterAttribute: System.Type -> bool = memoize _.IsDefined(typeof<JsonConverterAttribute>, true)

    override _.CanConvert t =
        not (t.IsGenericType && let g = t.GetGenericTypeDefinition() in g = typedefof<option<_>> || g = typedefof<list<_>>)
        && FsCodec.Union.isUnion t
        && not (hasConverterAttribute t)
        && ((typeSafeEnum && union)
            || typeSafeEnum = FsCodec.Union.isNullary t)

    override _.CreateConverter(t, _options) =
        let openConverterType = if FsCodec.Union.isNullary t then typedefof<TypeSafeEnumConverter<_>> else typedefof<UnionConverter<_>>
        openConverterType.MakeGenericType(t).GetConstructors().[0].Invoke[||] :?> _
