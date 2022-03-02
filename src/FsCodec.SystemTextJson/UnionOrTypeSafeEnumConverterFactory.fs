namespace FsCodec.SystemTextJson

open System
open System.Linq.Expressions
open System.Text.Json.Serialization

type internal ConverterActivator = delegate of unit -> JsonConverter

type UnionOrTypeSafeEnumConverterFactory() =
    inherit JsonConverterFactory()

    let isIntrinsic (t : Type) =
        t.IsGenericType
        && (t.GetGenericTypeDefinition() = typedefof<option<_>>
            || t.GetGenericTypeDefinition() = typedefof<list<_>>)

    override _.CanConvert(t : Type) =
        Union.isUnion t
        && not (isIntrinsic t)

    override _.CreateConverter(typ, _options) =
        let openConverterType = if Union.hasOnlyNullaryCases typ then typedefof<TypeSafeEnumConverter<_>> else typedefof<UnionConverter<_>>
        let constructor = openConverterType.MakeGenericType(typ).GetConstructors() |> Array.head
        let newExpression = Expression.New(constructor)
        let lambda = Expression.Lambda(typeof<ConverterActivator>, newExpression)

        let activator = lambda.Compile() :?> ConverterActivator
        activator.Invoke()
