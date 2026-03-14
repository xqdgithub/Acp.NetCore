using System.Collections.Generic;
using System.Linq;
using Acp.Types;

namespace Acp.Helpers;

/// <summary>
/// Helper methods for creating content blocks
/// </summary>
public static class ContentBlocks
{
    /// <summary>
    /// Create a text content block
    /// </summary>
    public static TextContentBlock Text(string text) => new(text);
    
    /// <summary>
    /// Create an image content block from base64 data
    /// </summary>
    public static ImageContentBlock ImageFromBase64(string data, string mimeType) 
        => new() { Source = ImageSource.FromBase64(data, mimeType) };
    
    /// <summary>
    /// Create an image content block from URL
    /// </summary>
    public static ImageContentBlock ImageFromUrl(string url)
        => new() { Source = ImageSource.FromUrl(url) };
    
    /// <summary>
    /// Create an audio content block from base64 data
    /// </summary>
    public static AudioContentBlock AudioFromBase64(string data, string mimeType)
        => new() { Source = new AudioSource { Type = "base64", Data = data, MimeType = mimeType } };
    
    /// <summary>
    /// Create a resource content block
    /// </summary>
    public static ResourceContentBlock Resource(string uri, string? mimeType = null, string? text = null)
        => new() { Resource = new ResourceReference { Uri = uri, MimeType = mimeType, Text = text } };
}

/// <summary>
/// Helper methods for creating tool calls
/// </summary>
public static class ToolCalls
{
    /// <summary>
    /// Create a tool call start notification
    /// </summary>
    public static ToolCallStart Start(string toolCallId, string toolName, Dictionary<string, object?>? input = null)
        => ToolCallStart.Create(toolCallId, toolName, input);
    
    /// <summary>
    /// Create a tool call update
    /// </summary>
    public static ToolCallUpdate Update(string toolCallId, string toolName, Dictionary<string, object?>? input = null)
        => new() { ToolCallId = toolCallId, ToolName = toolName, Input = input };
}

/// <summary>
/// Helper methods for creating permissions
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Create a permission option
    /// </summary>
    public static PermissionOption Option(string id, string label, string? description = null)
        => new() { Id = id, Label = label, Description = description };
}

/// <summary>
/// Helper methods for creating environment variables
/// </summary>
public static class Environment
{
    /// <summary>
    /// Create an environment variable
    /// </summary>
    public static EnvVariable Var(string name, string value)
        => new() { Name = name, Value = value };
}
