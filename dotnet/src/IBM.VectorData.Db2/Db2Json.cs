// Copyright (c) IBM Corporation. All rights reserved.

using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IBM.VectorData.Db2;

/// <summary>
/// Serializes string array properties, which are stored as JSON arrays in <c>VARCHAR</c> columns.
/// </summary>
/// <remarks>
/// Relaxed escaping keeps the stored text canonical: only quotes, backslashes and control characters are escaped.
/// The array <c>Contains</c> filter translation searches for the serialized element in that text, so values must be
/// serialized identically on write and on filter. HTML-safe escaping has no purpose for database storage.
/// </remarks>
internal static class Db2Json
{
    private static readonly Db2JsonSerializerContext s_context = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    internal static string Serialize(string value) => JsonSerializer.Serialize(value, s_context.String);

    internal static string Serialize(string[] value) => JsonSerializer.Serialize(value, s_context.StringArray);

    internal static string Serialize(List<string> value) => JsonSerializer.Serialize(value, s_context.ListString);

    internal static string[]? DeserializeStringArray(string json) => JsonSerializer.Deserialize(json, s_context.StringArray);

    internal static List<string>? DeserializeStringList(string json) => JsonSerializer.Deserialize(json, s_context.ListString);
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class Db2JsonSerializerContext : JsonSerializerContext;
