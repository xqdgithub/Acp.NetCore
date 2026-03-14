using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acp.Types;

/// <summary>
/// JSON-RPC request ID
/// </summary>
[JsonConverter(typeof(RequestIdConverter))]
public readonly struct RequestId
{
    public static RequestId Null => default;
    public static RequestId FromInt64(long value) => new(value);
    public static RequestId FromString(string value) => new(value);

    private readonly long? _longValue;
    private readonly string? _stringValue;
    private readonly bool _isNull;

    private RequestId(long value) => _longValue = value;
    private RequestId(string value) => _stringValue = value;
    private RequestId(bool isNull) => _isNull = isNull;

    public bool IsNull => _isNull;
    public bool IsLong => _longValue.HasValue;
    public bool IsString => _stringValue != null;
    public long LongValue => _longValue ?? 0;
    public string StringValue => _stringValue ?? "";

    public override string ToString() => _longValue?.ToString() ?? _stringValue ?? "null";
}

internal class RequestIdConverter : JsonConverter<RequestId>
{
    public override RequestId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => RequestId.Null,
            JsonTokenType.Number => RequestId.FromInt64(reader.GetInt64()),
            JsonTokenType.String => RequestId.FromString(reader.GetString()!),
            _ => throw new JsonException($"Invalid request ID type: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, RequestId value, JsonSerializerOptions options)
    {
        if (value.IsNull) writer.WriteNullValue();
        else if (value.IsLong) writer.WriteNumberValue(value.LongValue);
        else writer.WriteStringValue(value.StringValue);
    }
}
