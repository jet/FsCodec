namespace FsCodec.SystemTextJson

open System
open System.Linq.Expressions
open System.Text.Json
open System.Text.Json.Serialization

type RejectNullConverter<'T>() =
    inherit System.Text.Json.Serialization.JsonConverter<'T>()

    static let defaultConverter = JsonSerializerOptions.Default.GetConverter(typeof<'T>) :?> JsonConverter<'T>
    let msg () = sprintf "Expected value, got null. When rejectNull is true you must explicitly wrap optional %s values in an 'option'" typeof<'T>.Name

    override _.HandleNull = true

    override _.Read(reader, typeToConvert, options) =
        if reader.TokenType = JsonTokenType.Null then msg () |> nullArg else
        defaultConverter.Read(&reader, typeToConvert, options)
        // Pretty sure the above is the correct approach (and this unsurprisingly loops, blowing the stack)
        // JsonSerializer.Deserialize(&reader, typeToConvert, options) :?> 'T

    override _.Write(writer, value, options) =
        if value |> box |> isNull then msg () |> nullArg
        defaultConverter.Write(writer, value, options)
        // JsonSerializer.Serialize<'T>(writer, value, options)

type RejectNullConverterFactory(predicate) =
    inherit JsonConverterFactory()
    new() =
        RejectNullConverterFactory(fun (t: Type) ->
            t.IsGenericType
            && let gtd = t.GetGenericTypeDefinition() in gtd = typedefof<Set<_>> || gtd = typedefof<list<_>>)
    override _.CanConvert(t: Type) = predicate t

    override _.CreateConverter(t, _options) =
        let openConverterType = typedefof<RejectNullConverter<_>>
        let constructor = openConverterType.MakeGenericType(t).GetConstructors() |> Array.head
        let newExpression = Expression.New(constructor)
        let lambda = Expression.Lambda(typeof<ConverterActivator>, newExpression)

        let activator = lambda.Compile() :?> ConverterActivator
        activator.Invoke()
