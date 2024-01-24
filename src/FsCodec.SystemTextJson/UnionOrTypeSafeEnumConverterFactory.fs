namespace FsCodec.SystemTextJson

open System
open System.Linq.Expressions
open System.Text.Json.Serialization

type internal ConverterActivator = delegate of unit -> JsonConverter

type UnionOrTypeSafeEnumConverterFactory(typeSafeEnum, union) =
    inherit JsonConverterFactory()

    static let hasConverterAttribute = memoize (fun (t: Type) -> t.IsDefined(typeof<JsonConverterAttribute>, true))

    override _.CanConvert(t: Type) =
        not (t.IsGenericType && let gtd = t.GetGenericTypeDefinition() in gtd = typedefof<option<_>> || gtd = typedefof<list<_>>)
        && FsCodec.Union.isUnion t
        && not (hasConverterAttribute t)
        && ((typeSafeEnum && union)
            || typeSafeEnum = FsCodec.Union.isNullary t)

    override _.CreateConverter(t, _options) =
        let openConverterType = if FsCodec.Union.isNullary t then typedefof<TypeSafeEnumConverter<_>> else typedefof<UnionConverter<_>>
        let constructor = openConverterType.MakeGenericType(t).GetConstructors() |> Array.head
        let newExpression = Expression.New(constructor)
        let lambda = Expression.Lambda(typeof<ConverterActivator>, newExpression)

        let activator = lambda.Compile() :?> ConverterActivator
        activator.Invoke()
