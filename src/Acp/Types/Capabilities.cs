using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Acp.Types;

/// <summary>
/// Authentication method (initialize 响应中的 authMethods 项)
/// </summary>
public class AuthMethod
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Implementation info
/// </summary>
public class Implementation
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    public static Implementation Create(string name, string version) => new() { Name = name, Version = version };
}

/// <summary>
/// Client capabilities
/// </summary>
public class ClientCapabilities
{
    [JsonPropertyName("fs")]
    public FsCapabilities? Fs { get; init; }
    
    [JsonPropertyName("terminal")]
    public bool Terminal { get; init; }
    
    [JsonPropertyName("prompt")]
    public PromptCapabilities? Prompt { get; init; }
}

/// <summary>
/// Filesystem capabilities
/// </summary>
public class FsCapabilities
{
    [JsonPropertyName("readTextFile")]
    public bool ReadTextFile { get; init; }
    
    [JsonPropertyName("writeTextFile")]
    public bool WriteTextFile { get; init; }
}

/// <summary>
/// Prompt capabilities
/// </summary>
public class PromptCapabilities
{
    [JsonPropertyName("audio")]
    public bool Audio { get; init; }
    
    [JsonPropertyName("image")]
    public bool Image { get; init; }
    
    [JsonPropertyName("embeddedContext")]
    public bool EmbeddedContext { get; init; }
}

/// <summary>
/// MCP transport capabilities (e.g. http, sse)
/// </summary>
public class McpCapabilities
{
    [JsonPropertyName("http")]
    public bool Http { get; init; }

    [JsonPropertyName("sse")]
    public bool Sse { get; init; }
}

/// <summary>
/// Agent capabilities
/// </summary>
public class AgentCapabilities
{
    [JsonPropertyName("loadSession")]
    public bool LoadSession { get; init; }

    [JsonPropertyName("mcpCapabilities")]
    public McpCapabilities? McpCapabilities { get; init; }

    [JsonPropertyName("promptCapabilities")]
    public PromptCapabilities? PromptCapabilities { get; init; }

    [JsonPropertyName("sessionCapabilities")]
    public SessionCapabilities? SessionCapabilities { get; init; }
}

/// <summary>
/// Session capabilities
/// </summary>
public class SessionCapabilities
{
    [JsonPropertyName("supports")]
    public List<string>? Supports { get; init; }
}

/// <summary>
/// Environment variable
/// </summary>
public class EnvVariable
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("value")]
    public string Value { get; init; } = "";
}

/// <summary>
/// ACP permission option kind (https://agentclientprotocol.com/protocol/tool-calls#permission-options).
/// </summary>
public static class PermissionKind
{
    public const string AllowOnce = "allow_once";
    public const string AllowAlways = "allow_always";
    public const string RejectOnce = "reject_once";
    public const string RejectAlways = "reject_always";
}

/// <summary>
/// Permission option (ACP: optionId, name, kind)
/// </summary>
public class PermissionOption
{
    [JsonPropertyName("optionId")]
    public string Id { get; init; } = "";

    [JsonPropertyName("name")]
    public string Label { get; init; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Session information (ACP session/list 等返回)
/// </summary>
public class SessionInfo
{
    [JsonPropertyName("sessionId")]
    public string Id { get; init; } = "";

    [JsonPropertyName("cwd")]
    public string Cwd { get; init; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; init; }

    [JsonPropertyName("capabilities")]
    public SessionCapabilities? Capabilities { get; init; }
}
