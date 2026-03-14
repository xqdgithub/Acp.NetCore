using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acp.Types;

/// <summary>
/// MCP 服务配置基类（ACP：stdio 无 type，http/sse 含 type）
/// </summary>
[JsonConverter(typeof(McpServerConfigConverter))]
public abstract class McpServerConfig
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

/// <summary>
/// Stdio 传输（ACP: name, command, args, env，无 type 字段）
/// </summary>
public class StdioMcpServer : McpServerConfig
{
    [JsonPropertyName("command")]
    public string Command { get; init; } = "";

    [JsonPropertyName("args")]
    public List<string>? Args { get; init; }

    [JsonPropertyName("env")]
    public List<EnvVariable>? Env { get; init; }
}

/// <summary>
/// HTTP 传输（ACP: type "http", name, url, headers）
/// </summary>
public class HttpMcpServer : McpServerConfig
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("headers")]
    public List<HttpHeader>? Headers { get; init; }
}

/// <summary>
/// SSE 传输（ACP: type "sse", name, url, headers）
/// </summary>
public class SseMcpServer : McpServerConfig
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("headers")]
    public List<HttpHeader>? Headers { get; init; }
}

/// <summary>
/// HTTP 头（name / value）
/// </summary>
public class HttpHeader
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("value")]
    public string Value { get; init; } = "";
}

internal sealed class McpServerConfigConverter : JsonConverter<McpServerConfig>
{
    public override McpServerConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        return type switch
        {
            "http" => JsonSerializer.Deserialize<HttpMcpServer>(root.GetRawText(), options),
            "sse" => JsonSerializer.Deserialize<SseMcpServer>(root.GetRawText(), options),
            _ => JsonSerializer.Deserialize<StdioMcpServer>(root.GetRawText(), options)
        };
    }

    public override void Write(Utf8JsonWriter writer, McpServerConfig value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case HttpMcpServer http:
                writer.WriteStartObject();
                writer.WriteString("type", "http");
                writer.WriteString("name", http.Name);
                writer.WriteString("url", http.Url);
                if (http.Headers != null)
                {
                    writer.WritePropertyName("headers");
                    JsonSerializer.Serialize(writer, http.Headers, options);
                }
                writer.WriteEndObject();
                break;
            case SseMcpServer sse:
                writer.WriteStartObject();
                writer.WriteString("type", "sse");
                writer.WriteString("name", sse.Name);
                writer.WriteString("url", sse.Url);
                if (sse.Headers != null)
                {
                    writer.WritePropertyName("headers");
                    JsonSerializer.Serialize(writer, sse.Headers, options);
                }
                writer.WriteEndObject();
                break;
            default:
                var stdio = (StdioMcpServer)value;
                writer.WriteStartObject();
                writer.WriteString("name", stdio.Name);
                writer.WriteString("command", stdio.Command);
                if (stdio.Args != null) { writer.WritePropertyName("args"); JsonSerializer.Serialize(writer, stdio.Args, options); }
                if (stdio.Env != null) { writer.WritePropertyName("env"); JsonSerializer.Serialize(writer, stdio.Env, options); }
                writer.WriteEndObject();
                break;
        }
    }
}
