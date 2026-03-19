using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acp.Types;

/// <summary>
/// JSON-RPC request ID，支持数字、字符串和 null 值。
/// 使用工厂方法创建实例，访问器会在错误状态时抛出异常。
/// </summary>
[JsonConverter(typeof(RequestIdConverter))]
public readonly struct RequestId : IEquatable<RequestId>
{
    /// <summary>空 ID（null）</summary>
    public static RequestId Null => default;

    /// <summary>从长整型创建 ID</summary>
    public static RequestId FromInt64(long value) => new(value);

    /// <summary>从字符串创建 ID</summary>
    public static RequestId FromString(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));

    private readonly long? _longValue;
    private readonly string? _stringValue;
    private readonly bool _isNull;

    private RequestId(long value) { _longValue = value; _isNull = false; }
    private RequestId(string value) { _stringValue = value; _isNull = false; }

    /// <summary>是否为 null</summary>
    public bool IsNull => _isNull && !_longValue.HasValue && _stringValue == null;

    /// <summary>是否为数字类型</summary>
    public bool IsLong => _longValue.HasValue;

    /// <summary>是否为字符串类型</summary>
    public bool IsString => _stringValue != null;

    /// <summary>
    /// 获取数字值。如果不是数字类型，抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <exception cref="InvalidOperationException">当 ID 不是数字类型时抛出</exception>
    public long LongValue => _longValue ?? throw new InvalidOperationException("RequestId is not a long value. Check IsLong before accessing LongValue.");

    /// <summary>
    /// 获取字符串值。如果不是字符串类型，抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <exception cref="InvalidOperationException">当 ID 不是字符串类型时抛出</exception>
    public string StringValue => _stringValue ?? throw new InvalidOperationException("RequestId is not a string value. Check IsString before accessing StringValue.");

    /// <inheritdoc />
    public override string ToString() => _longValue?.ToString() ?? _stringValue ?? "null";

    /// <inheritdoc />
    public bool Equals(RequestId other)
    {
        if (IsNull && other.IsNull) return true;
        if (IsLong && other.IsLong) return LongValue == other.LongValue;
        if (IsString && other.IsString) return StringValue == other.StringValue;
        return false;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RequestId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _longValue?.GetHashCode() ?? _stringValue?.GetHashCode() ?? 0;

    /// <summary>相等比较运算符</summary>
    public static bool operator ==(RequestId left, RequestId right) => left.Equals(right);

    /// <summary>不等比较运算符</summary>
    public static bool operator !=(RequestId left, RequestId right) => !left.Equals(right);
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
