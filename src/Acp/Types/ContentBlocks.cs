using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acp.Types;

/// <summary>
/// Content block base type. 多态反序列化以支持 session/update 中的 content。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentBlock), "text")]
[JsonDerivedType(typeof(ImageContentBlock), "image")]
[JsonDerivedType(typeof(AudioContentBlock), "audio")]
[JsonDerivedType(typeof(ResourceContentBlock), "resource")]
[JsonDerivedType(typeof(ResourceLinkContentBlock), "resource_link")]
public abstract class ContentBlock
{
    /// <summary>类型标识，供代码使用；JSON 中的 type 由多态 discriminator 占用。</summary>
    [JsonIgnore]
    public string Type { get; init; } = "";
}

/// <summary>
/// Text content block
/// </summary>
public class TextContentBlock : ContentBlock
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    public TextContentBlock() { Type = "text"; }
    public TextContentBlock(string text) { Type = "text"; Text = text; }
    
    public static TextContentBlock Create(string text) => new(text);
}

/// <summary>
/// Image content block。支持 ACP 顶层 mimeType/data 与 MCP 的 source 两种格式。
/// </summary>
[JsonConverter(typeof(ImageContentBlockConverter))]
public class ImageContentBlock : ContentBlock
{
    [JsonPropertyName("source")]
    public ImageSource Source { get; init; } = new();

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    public ImageContentBlock() { Type = "image"; }

    /// <summary>优先从 Source 取，否则从顶层 MimeType/Data 取。</summary>
    public string? EffectiveMimeType => Source?.MimeType ?? MimeType;
    public string? EffectiveData => Source?.Data ?? Data;
}

internal sealed class ImageContentBlockConverter : JsonConverter<ImageContentBlock>
{
    public override ImageContentBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        ImageSource? source = null;
        string? mimeType = null;
        string? data = null;
        if (root.TryGetProperty("source", out var sourceEl))
            source = JsonSerializer.Deserialize<ImageSource>(sourceEl, options);
        if (root.TryGetProperty("mimeType", out var mt))
            mimeType = mt.GetString();
        if (root.TryGetProperty("data", out var d))
            data = d.GetString();
        if (source == null && (mimeType != null || data != null))
            source = new ImageSource { MimeType = mimeType, Data = data, Type = "base64" };
        return new ImageContentBlock
        {
            Source = source ?? new ImageSource(),
            MimeType = mimeType ?? source?.MimeType,
            Data = data ?? source?.Data
        };
    }

    public override void Write(Utf8JsonWriter writer, ImageContentBlock value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "image");
        var mime = value.EffectiveMimeType;
        var data = value.EffectiveData;
        if (!string.IsNullOrEmpty(mime))
            writer.WriteString("mimeType", mime);
        if (!string.IsNullOrEmpty(data))
            writer.WriteString("data", data);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Image source
/// </summary>
public class ImageSource
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "base64";
    
    [JsonPropertyName("data")]
    public string? Data { get; init; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
    
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    public static ImageSource FromBase64(string data, string mimeType) => new() { Type = "base64", Data = data, MimeType = mimeType };
    public static ImageSource FromUrl(string url) => new() { Type = "url", Url = url };
}

/// <summary>
/// Audio content block
/// </summary>
public class AudioContentBlock : ContentBlock
{
    [JsonPropertyName("source")]
    public AudioSource Source { get; init; } = new();

    public AudioContentBlock() { Type = "audio"; }
}

/// <summary>
/// Audio source
/// </summary>
public class AudioSource
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "base64";
    
    [JsonPropertyName("data")]
    public string? Data { get; init; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
}

/// <summary>
/// Resource content block
/// </summary>
public class ResourceContentBlock : ContentBlock
{
    [JsonPropertyName("resource")]
    public ResourceReference Resource { get; init; } = new();

    public ResourceContentBlock() { Type = "resource"; }
}

/// <summary>
/// Resource reference (text: uri + text + mimeType; blob: uri + data base64 + mimeType)
/// </summary>
public class ResourceReference
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = "";

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }
}

/// <summary>
/// Resource link content block (ACP: 引用资源，Agent 可访问；uri, name, mimeType, title?, description?, size?)
/// </summary>
public class ResourceLinkContentBlock : ContentBlock
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    public ResourceLinkContentBlock() { Type = "resource_link"; }
}
