namespace FsCodec.SystemTextJson

open System
open System.Linq.Expressions
open System.Text.Json
open System.Text.Json.Serialization

type OptionConverterActivator = delegate of unit -> JsonConverter

type JsonOptionConverter<'T> () =
    inherit JsonConverter<Option<'T>> ()

    override __.Read(reader, _typ, options) =
        match reader.TokenType with
        | JsonTokenType.Null -> None
        | _ -> JsonSerializer.Deserialize<'T>(&reader, options) |> Some

    override __.Write(writer, value, options) =
        match value with
        | None -> writer.WriteNullValue()
        | Some v -> JsonSerializer.Serialize<'T>(writer, v, options)

type JsonOptionConverter () =
    inherit JsonConverterFactory()

    override __.CanConvert(t : Type) =
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override __.CreateConverter (typ, _options) =
        let valueType = typ.GetGenericArguments() |> Array.head
        let constructor = typedefof<JsonOptionConverter<_>>.MakeGenericType(valueType).GetConstructors() |> Array.head
        let newExpression = Expression.New(constructor)
        let lambda = Expression.Lambda(typeof<OptionConverterActivator>, newExpression)

        let activator = lambda.Compile() :?> OptionConverterActivator
        activator.Invoke()
