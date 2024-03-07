namespace FsCodec.SystemTextJson

open System
open System.Text.Json
open System.Text.Json.Serialization

type RejectNullConverter<'T>() =
    inherit System.Text.Json.Serialization.JsonConverter<'T>()

    let defaultConverter = JsonSerializerOptions.Default.GetConverter(typeof<'T>) :?> JsonConverter<'T>
    let msg () = sprintf "Expected value, got null. When rejectNull is true you must explicitly wrap optional %s values in an 'option'" typeof<'T>.Name

    override _.HandleNull = true

    override _.Read(reader, typeToConvert, options) =
        if reader.TokenType = JsonTokenType.Null then msg () |> nullArg else
        // PROBLEM: Fails with NRE when RejectNullConverter delegates to Default list<int> Converter
        // System.NullReferenceException
        //  at System.Text.Json.Serialization.JsonCollectionConverter`2.OnTryRead(Utf8JsonReader& reader, Type typeToConvert, JsonSerializerOptions options, ReadStack& state, TCollection& value)
        // https://github.com/dotnet/runtime/issues/50205 via https://github.com/jet/FsCodec/pull/112#issuecomment-1907633798
        defaultConverter.Read(&reader, typeToConvert, options)
        // Pretty sure the above is the correct approach (and this unsurprisingly loops, blowing the stack)
        // JsonSerializer.Deserialize(&reader, typeToConvert, options) :?> 'T

    override _.Write(writer, value, options) =
        if value |> box |> isNull then msg () |> nullArg
        defaultConverter.Write(writer, value, options)
        // JsonSerializer.Serialize<'T>(writer, value, options)

type RejectNullConverterFactory(predicate) =
    inherit JsonConverterFactory()
    static let isListOrSet (t: Type) = t.IsGenericType && let g = t.GetGenericTypeDefinition() in g = typedefof<Set<_>> || g = typedefof<list<_>>
    new() = RejectNullConverterFactory(isListOrSet)

    override _.CanConvert(t: Type) = predicate t
    override _.CreateConverter(t, _options) = typedefof<RejectNullConverter<_>>.MakeGenericType(t).GetConstructors().[0].Invoke[||] :?> _
