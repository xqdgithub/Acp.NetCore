using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acp.Types;

namespace Acp.Messages;

/// <summary>
/// Initialize request (ACP: clientCapabilities, clientInfo)
/// </summary>
public class InitializeRequest
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; }

    [JsonPropertyName("clientCapabilities")]
    public ClientCapabilities? ClientCapabilities { get; init; }

    [JsonPropertyName("clientInfo")]
    public Implementation? ClientInfo { get; init; }

    public const string Method = "initialize";
}

/// <summary>
/// Initialize response (ACP: agentCapabilities, agentInfo, authMethods)
/// </summary>
public class InitializeResponse
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; }

    [JsonPropertyName("agentCapabilities")]
    public AgentCapabilities AgentCapabilities { get; init; } = new();

    [JsonPropertyName("agentInfo")]
    public Implementation AgentInfo { get; init; } = new();

    [JsonPropertyName("authMethods")]
    public List<AuthMethod>? AuthMethods { get; init; }
}

/// <summary>
/// New session request (ACP: cwd, mcpServers).
/// </summary>
public class NewSessionRequest
{
    [JsonPropertyName("cwd")]
    public string Cwd { get; init; } = "";

    [JsonPropertyName("mcpServers")]
    public List<McpServerConfig>? McpServers { get; init; }

    public const string Method = "session/new";
}

/// <summary>
/// New session response
/// </summary>
public class NewSessionResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
}

/// <summary>
/// Load session request (ACP: sessionId, cwd, mcpServers)。需先确认 agentCapabilities.loadSession 为 true。
/// </summary>
public class LoadSessionRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";

    [JsonPropertyName("cwd")]
    public string Cwd { get; init; } = "";

    [JsonPropertyName("mcpServers")]
    public List<McpServerConfig>? McpServers { get; init; }

    public const string Method = "session/load";
}

/// <summary>
/// Load session response（与 Python/ACP 一致：sessionId、models、modes、_meta）。
/// ACP 标准：session/load 在回放完所有 session/update 后可为 result: null；扩展实现可返回本对象。
/// </summary>
public class LoadSessionResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";

    [JsonPropertyName("models")]
    public SessionModelState? Models { get; init; }

    [JsonPropertyName("modes")]
    public SessionModeState? Modes { get; init; }

    [JsonPropertyName("configOptions")]
    public List<SessionConfigOption>? ConfigOptions { get; init; }

    /// <summary>扩展元数据（如 _meta.opencode），ACP 保留字段。</summary>
    [JsonPropertyName("_meta")]
    public Dictionary<string, JsonElement>? FieldMeta { get; init; }
}

/// <summary>
/// List sessions request
/// </summary>
public class ListSessionsRequest
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; init; }
    
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }
    
    public const string Method = "session/list";
}

/// <summary>
/// List sessions response
/// </summary>
public class ListSessionsResponse
{
    [JsonPropertyName("sessions")]
    public List<SessionInfo>? Sessions { get; init; }
    
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }
}

/// <summary>
/// Set session mode request
/// </summary>
public class SetSessionModeRequest
{
    [JsonPropertyName("modeId")]
    public string ModeId { get; init; } = "";
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    public const string Method = "session/set_mode";
}

/// <summary>
/// Set session mode response
/// </summary>
public class SetSessionModeResponse
{
    [JsonPropertyName("modeId")]
    public string ModeId { get; init; } = "";
}

/// <summary>
/// Set session model request
/// </summary>
public class SetSessionModelRequest
{
    [JsonPropertyName("modelId")]
    public string ModelId { get; init; } = "";
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    public const string Method = "session/set_model";
}

/// <summary>
/// Set session model response
/// </summary>
public class SetSessionModelResponse
{
    [JsonPropertyName("modelId")]
    public string ModelId { get; init; } = "";
}

/// <summary>
/// Set session config option request
/// </summary>
public class SetSessionConfigOptionRequest
{
    [JsonPropertyName("configId")]
    public string ConfigId { get; init; } = "";
    
    [JsonPropertyName("value")]
    public string Value { get; init; } = "";
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    public const string Method = "session/set_config_option";
}

/// <summary>
/// Set session config option response
/// </summary>
public class SetSessionConfigOptionResponse
{
    [JsonPropertyName("configId")]
    public string ConfigId { get; init; } = "";
}

/// <summary>
/// Authenticate request
/// </summary>
public class AuthenticateRequest
{
    [JsonPropertyName("methodId")]
    public string MethodId { get; init; } = "";
    
    public const string Method = "authenticate";
}

/// <summary>
/// Authenticate response
/// </summary>
public class AuthenticateResponse
{
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }
}

/// <summary>
/// Prompt request
/// </summary>
public class PromptRequest
{
    [JsonPropertyName("prompt")]
    public List<ContentBlock>? Prompt { get; init; }
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    public const string Method = "session/prompt";
}

/// <summary>
/// Prompt response (ACP: stopReason + content)
/// </summary>
public class PromptResponse
{
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("content")]
    public List<ContentBlock> Content { get; init; } = new();
}

/// <summary>
/// ACP prompt turn 结束原因（session/prompt 的 result.stopReason）
/// </summary>
public static class StopReasons
{
    public const string EndTurn = "end_turn";
    public const string MaxTokens = "max_tokens";
    public const string MaxRequests = "max_requests";
    public const string Refused = "refused";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Fork session request
/// </summary>
public class ForkSessionRequest
{
    [JsonPropertyName("cwd")]
    public string Cwd { get; init; } = "";
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("mcpServers")]
    public List<McpServerConfig>? McpServers { get; init; }

    public const string Method = "session/fork";
}

/// <summary>
/// Fork session response
/// </summary>
public class ForkSessionResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
}

/// <summary>
/// Resume session request
/// </summary>
public class ResumeSessionRequest
{
    [JsonPropertyName("cwd")]
    public string Cwd { get; init; } = "";
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("mcpServers")]
    public List<McpServerConfig>? McpServers { get; init; }

    public const string Method = "session/resume";
}

/// <summary>
/// Resume session response
/// </summary>
public class ResumeSessionResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
}

/// <summary>
/// Cancel notification (ACP: session/cancel).
/// 发送后 Client 应将当前 turn 下未完成的 tool call 标记为 cancelled，并对所有未决的 session/request_permission 以 outcome cancelled 回复。
/// </summary>
public class CancelNotification
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";

    public const string Method = "session/cancel";
}
