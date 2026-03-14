using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acp.Types;

/// <summary>
/// Session update base. 与 Python SDK 对齐，标准类型全部支持，未知类型通过 <see cref="SessionUpdateConverter"/> 反序列化为 <see cref="UnknownSessionUpdate"/>。
/// </summary>
[JsonConverter(typeof(SessionUpdateConverter))]
public abstract class SessionUpdate
{
    [JsonIgnore]
    public string Type { get; init; } = "";
}

/// <summary>
/// 未识别的 session update（扩展或未来协议），不抛错，原始数据在 ExtensionData 中。
/// </summary>
public class UnknownSessionUpdate : SessionUpdate
{
    [JsonPropertyName("sessionUpdate")]
    public string? SessionUpdateKind { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// User message chunk（与 Python ContentChunk 一致：单块 content）
/// </summary>
public class UserMessageChunk : SessionUpdate
{
    [JsonPropertyName("content")]
    public ContentBlock? Content { get; init; }

    public UserMessageChunk() { Type = "user_message_chunk"; }
}

/// <summary>
/// Agent message chunk. 与 Python SDK / ACP wire 一致：与 AgentThoughtChunk 同属 ContentChunk，使用顶层 "content"（单块），非 "message"。
/// </summary>
public class AgentMessageChunk : SessionUpdate
{
    [JsonPropertyName("content")]
    public ContentBlock? Content { get; init; }

    /// <summary>兼容旧用法：将单块 content 包装为 Message.Content 列表。</summary>
    [JsonIgnore]
    public Message Message => new Message
    {
        Content = Content != null ? new List<ContentBlock> { Content } : new List<ContentBlock>()
    };

    public AgentMessageChunk() { Type = "agent_message_chunk"; }
}

/// <summary>
/// Message
/// </summary>
public class Message
{
    [JsonPropertyName("content")]
    public List<ContentBlock> Content { get; init; } = new();

    [JsonPropertyName("role")]
    public string Role { get; init; } = "agent";
}

/// <summary>
/// Agent thought chunk. 与 Python SDK / ACP wire 一致：使用 "content"（单块 ContentBlock），非 "thought" 字符串。
/// </summary>
public class AgentThoughtChunk : SessionUpdate
{
    [JsonPropertyName("content")]
    public ContentBlock? Content { get; init; }

    /// <summary>从 content 中取文本（当 content 为 TextContentBlock 时），兼容旧用法。</summary>
    [JsonIgnore]
    public string Thought => (Content as TextContentBlock)?.Text ?? "";

    public AgentThoughtChunk() { Type = "agent_thought_chunk"; }
}

/// <summary>
/// Tool call start (ACP: sessionUpdate "tool_call", 含 title/kind/status)
/// </summary>
public class ToolCallStart : SessionUpdate
{
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "other";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "pending";

    [JsonPropertyName("toolName")]
    public string ToolName { get; init; } = "";

    [JsonPropertyName("input")]
    public Dictionary<string, object?>? Input { get; init; }

    public ToolCallStart() { Type = "tool_call"; }

    public static ToolCallStart Create(string toolCallId, string toolName, Dictionary<string, object?>? input = null)
        => new() { ToolCallId = toolCallId, ToolName = toolName, Input = input };
}

/// <summary>
/// Tool call progress（ACP: sessionUpdate "tool_call_update"，status + 可选 content）
/// </summary>
public class ToolCallProgress : SessionUpdate
{
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("content")]
    public List<ToolCallContentItem>? Content { get; init; }

    [JsonPropertyName("toolName")]
    public string ToolName { get; init; } = "";

    [JsonPropertyName("input")]
    public Dictionary<string, object?>? Input { get; init; }

    public ToolCallProgress() { Type = "tool_call_update"; }
}

/// <summary>
/// Tool call 产出内容项（type: content 含 content 块，或 diff/terminal 等）
/// </summary>
public class ToolCallContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "content";

    [JsonPropertyName("content")]
    public ContentBlock? Content { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("oldText")]
    public string? OldText { get; init; }

    [JsonPropertyName("newText")]
    public string? NewText { get; init; }

    [JsonPropertyName("terminalId")]
    public string? TerminalId { get; init; }
}

/// <summary>
/// Plan entry（与 Python PlanEntry 一致）
/// </summary>
public class PlanEntry
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
}

/// <summary>
/// Agent plan update（与 Python plan 一致）
/// </summary>
public class AgentPlanUpdate : SessionUpdate
{
    [JsonPropertyName("entries")]
    public List<PlanEntry> Entries { get; init; } = new();

    public AgentPlanUpdate() { Type = "plan"; }
}

/// <summary>
/// Current mode update
/// </summary>
public class CurrentModeUpdate : SessionUpdate
{
    [JsonPropertyName("currentModeId")]
    public string CurrentModeId { get; init; } = "";

    public CurrentModeUpdate() { Type = "current_mode_update"; }
}

/// <summary>
/// Session config option（简化，与 Python SessionConfigOption 兼容）
/// </summary>
public class SessionConfigOption
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

/// <summary>
/// Config option update
/// </summary>
public class ConfigOptionUpdate : SessionUpdate
{
    [JsonPropertyName("configOptions")]
    public List<SessionConfigOption> ConfigOptions { get; init; } = new();

    public ConfigOptionUpdate() { Type = "config_option_update"; }
}

/// <summary>
/// Session info update
/// </summary>
public class SessionInfoUpdate : SessionUpdate
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; init; }

    public SessionInfoUpdate() { Type = "session_info_update"; }
}

/// <summary>
/// Usage update
/// </summary>
public class UsageUpdate : SessionUpdate
{
    [JsonPropertyName("size")]
    public int Size { get; init; }

    [JsonPropertyName("used")]
    public int Used { get; init; }

    [JsonPropertyName("cost")]
    public object? Cost { get; init; }

    public UsageUpdate() { Type = "usage_update"; }
}

/// <summary>
/// Tool call update（非 session update，用于请求/响应结构）
/// </summary>
public class ToolCallUpdate
{
    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; init; } = "";

    [JsonPropertyName("toolName")]
    public string ToolName { get; init; } = "";

    [JsonPropertyName("input")]
    public Dictionary<string, object?>? Input { get; init; }
}

/// <summary>
/// 单条可用命令（与 ACP available_commands_update 一致）
/// </summary>
public class AvailableCommand
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
}

/// <summary>
/// Session update: 可用命令列表更新
/// </summary>
public class AvailableCommandsUpdate : SessionUpdate
{
    [JsonPropertyName("availableCommands")]
    public List<AvailableCommand> AvailableCommands { get; init; } = new();

    public AvailableCommandsUpdate() { Type = "available_commands_update"; }
}
