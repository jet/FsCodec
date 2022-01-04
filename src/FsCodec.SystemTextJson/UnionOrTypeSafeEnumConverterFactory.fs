namespace FsCodec.SystemTextJson

open System
open System.Linq.Expressions
open System.Text.Json.Serialization

type internal ConverterActivator = delegate of unit -> JsonConverter

type UnionOrTypeSafeEnumConverterFactory() =
    inherit JsonConverterFactory()

    override _.CanConvert(t : Type) =
        let res = Union.isUnion t
        if Union.typeHasJsonConverterAttribute t then failwith "needs conjunction"
        //&& not (Union.typeHasJsonConverterAttribute t)
        res

    override _.CreateConverter(typ, _options) =
        let openConverterType = if Union.hasOnlyNullaryCases typ then typedefof<TypeSafeEnumConverter<_>> else typedefof<UnionConverter<_>>
        let constructor = openConverterType.MakeGenericType(typ).GetConstructors() |> Array.head
        let newExpression = Expression.New(constructor)
        let lambda = Expression.Lambda(typeof<ConverterActivator>, newExpression)

        let activator = lambda.Compile() :?> ConverterActivator
        activator.Invoke()
