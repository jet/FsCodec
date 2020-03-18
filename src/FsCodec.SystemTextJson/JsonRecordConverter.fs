﻿namespace FsCodec.SystemTextJson.Converters

open FsCodec.SystemTextJson.Core
open FSharp.Reflection
open System
open System.Collections.Generic
open System.Linq.Expressions
open System.Text.Json
open System.Text.Json.Serialization

type JsonRecordConverterActivator = delegate of JsonSerializerOptions -> JsonConverter

type IRecordFieldConverter =
    abstract member Initialize: converter: JsonConverter -> unit
    abstract member Read: reader: byref<Utf8JsonReader> * typ: Type * options: JsonSerializerOptions -> obj
    abstract member Write: writer: Utf8JsonWriter * value: obj * options: JsonSerializerOptions -> unit

type RecordFieldConverter<'F> () =
    let mutable converter = Unchecked.defaultof<JsonConverter<'F>>

    interface IRecordFieldConverter with
        member __.Initialize (c) =
            converter <- c :?> JsonConverter<'F>

        member __.Read (reader, typ, options) =
            converter.Read(&reader, typ, options) :> obj

        member __.Write (writer, value, options) =
            converter.Write(writer, value :?> 'F, options)

[<NoComparison>]
type RecordField = {
    name: string
    fieldType: Type
    index: int
    isIgnored: bool
    converter: IRecordFieldConverter option
}

type JsonRecordConverter<'T> (options: JsonSerializerOptions) =
    inherit JsonConverter<'T> ()

    let recordType = typeof<'T>

    let constructor = FSharpValue.PreComputeRecordConstructor(recordType, true)
    let getFieldValues = FSharpValue.PreComputeRecordReader(typeof<'T>, true)

    let fields =
        FSharpType.GetRecordFields(recordType, true)
        |> Array.mapi (fun idx f ->
            {
                name =
                    f.GetCustomAttributes(typedefof<JsonPropertyNameAttribute>, true)
                    |> Array.tryHead
                    |> Option.map (fun attr -> (attr :?> JsonPropertyNameAttribute).Name)
                    |> Option.defaultWith (fun () ->
                        if options.PropertyNamingPolicy |> isNull
                            then f.Name
                            else options.PropertyNamingPolicy.ConvertName f.Name)

                fieldType = f.PropertyType
                index = idx
                isIgnored = f.GetCustomAttributes(typeof<JsonIgnoreAttribute>, true) |> Array.isEmpty |> not
                converter =
                    f.GetCustomAttributes(typeof<JsonConverterAttribute>, true)
                    |> Array.tryHead
                    |> Option.map (fun attr -> attr :?> JsonConverterAttribute)
                    |> Option.bind (fun attr ->
                        let baseConverter =
                            match attr.CreateConverter(f.PropertyType) with
                            | null ->
                                match Activator.CreateInstance(attr.ConverterType) with
                                | :? JsonConverter as x -> x
                                | _ -> failwithf "Field %s is decorated with a JsonConverter attribute that does not implement a CreateConverter method or refer to a valid 'ConverterType'." f.Name
                            | created -> created
                        if baseConverter.CanConvert(f.PropertyType) then
                            let converterType = typedefof<RecordFieldConverter<_>>.MakeGenericType(f.PropertyType)
                            let converter = Activator.CreateInstance(converterType) :?> IRecordFieldConverter
                            converter.Initialize(baseConverter)
                            Some converter
                        else
                            None
                    )
            })

    let fieldsByName =
        fields
        |> Array.map (fun f -> f.name, f)
#if NETSTANDARD2_1
        |> Array.map KeyValuePair.Create
        |> (fun kvp -> Dictionary(kvp, StringComparer.OrdinalIgnoreCase))
#else
        |> Array.map KeyValuePair
        |> (fun kvp -> kvp.ToDictionary((fun item -> item.Key), (fun item -> item.Value), StringComparer.OrdinalIgnoreCase))
#endif

    let tryGetFieldByName name =
        match fieldsByName.TryGetValue(name) with
        | true, field -> Some field
        | _ -> None

    override __.Read (reader, typ, options) =
        reader.ValidateTokenType(JsonTokenType.StartObject)

        let fields = Array.zeroCreate <| fields.Length

        while reader.Read() && reader.TokenType <> JsonTokenType.EndObject do
            reader.ValidateTokenType(JsonTokenType.PropertyName)

            match tryGetFieldByName <| reader.GetString() with
            | Some field ->
                fields.[field.index] <-
                    match field.converter with
                    | Some converter ->
                        reader.Read() |> ignore
                        converter.Read(&reader, field.fieldType, options)
                    | None ->
                        JsonSerializer.Deserialize(&reader, field.fieldType, options)
            | _ ->
                reader.Skip()

        constructor fields :?> 'T

    override __.Write (writer, record, options) =
        writer.WriteStartObject()

        let fieldValues = getFieldValues record

        (fields, fieldValues)
        ||> Array.iter2 (fun field value ->
            match value with
            | :? JsonElement as je when je.ValueKind = JsonValueKind.Undefined -> ()
            | _ ->
                if not field.isIgnored && not (options.IgnoreNullValues && isNull value) then
                    writer.WritePropertyName(field.name)

                    match field.converter with
                    | Some converter -> converter.Write(writer, value, options)
                    | None -> JsonSerializer.Serialize(writer, value, options))

        writer.WriteEndObject()

type JsonRecordConverter () =
    inherit JsonConverterFactory()

    override __.CanConvert typ =
        FSharpType.IsRecord (typ, true)

    override __.CreateConverter (typ, options) =
        let constructor = typedefof<JsonRecordConverter<_>>.MakeGenericType(typ).GetConstructor(typeof<JsonSerializerOptions> |> Array.singleton)
        let optionsParameter = Expression.Parameter(typeof<JsonSerializerOptions>, "options")

        let newExpression = Expression.New(constructor, optionsParameter)
        let lambda = Expression.Lambda(typeof<JsonRecordConverterActivator>, newExpression, optionsParameter)

        let activator = lambda.Compile() :?> JsonRecordConverterActivator
        activator.Invoke(options)
