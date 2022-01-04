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
        let constructor =
             match typ with
             | Union.NotUnion -> invalidOp (sprintf "%s is not a union type" typ.FullName)
             | Union.TypeSafeEnum -> typedefof<TypeSafeEnumConverter<_>>.MakeGenericType(typ).GetConstructors() |> Array.head
             | Union.Other _ -> typedefof<UnionConverter<_>>.MakeGenericType(typ).GetConstructors() |> Array.head
        let newExpression = Expression.New(constructor)
        let lambda = Expression.Lambda(typeof<ConverterActivator>, newExpression)

        let activator = lambda.Compile() :?> ConverterActivator
        activator.Invoke()
