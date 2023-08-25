namespace FsCodec.SystemTextJson

open System
open System.Linq.Expressions
open System.Text.Json.Serialization

type internal ConverterActivator = delegate of unit -> JsonConverter

type UnionOrTypeSafeEnumConverterFactory(typeSafeEnum, union) =
    inherit JsonConverterFactory()

    let isIntrinsic (t: Type) =
        t.IsGenericType
        && (let gtd = t.GetGenericTypeDefinition() in gtd = typedefof<option<_>> || gtd = typedefof<list<_>>)

    override _.CanConvert(t : Type) =
        not (isIntrinsic t)
        && Union.isUnion t
        && not (Union.unionHasJsonConverterAttribute t)
        && ((typeSafeEnum && union)
            || typeSafeEnum = Union.hasOnlyNullaryCases t)

    override _.CreateConverter(typ, _options) =
        let openConverterType = if Union.hasOnlyNullaryCases typ then typedefof<TypeSafeEnumConverter<_>> else typedefof<UnionConverter<_>>
        let constructor = openConverterType.MakeGenericType(typ).GetConstructors() |> Array.head
        let newExpression = Expression.New(constructor)
        let lambda = Expression.Lambda(typeof<ConverterActivator>, newExpression)

        let activator = lambda.Compile() :?> ConverterActivator
        activator.Invoke()
