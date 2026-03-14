using System.Collections.Generic;
using System.Text.Json.Serialization;
using Acp.Types;

namespace Acp.Messages;

/// <summary>
/// Session update notification
/// </summary>
public class SessionUpdateNotification
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("update")]
    public SessionUpdate? Update { get; init; }
    
    public const string Method = "session/update";
}

/// <summary>
/// Read text file request (ACP: sessionId, path, line, limit)
/// </summary>
public class ReadTextFileRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("line")]
    public int? Line { get; init; }

    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    public const string Method = "fs/read_text_file";
}

/// <summary>
/// Read text file response
/// </summary>
public class ReadTextFileResponse
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = "";
}

/// <summary>
/// Write text file request (ACP: sessionId, path, content)
/// </summary>
public class WriteTextFileRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("content")]
    public string Content { get; init; } = "";

    public const string Method = "fs/write_text_file";
}

/// <summary>
/// Write text file response。ACP 标准允许成功时 result: null；本类型为扩展（applied 表示是否写入成功）。
/// </summary>
public class WriteTextFileResponse
{
    [JsonPropertyName("applied")]
    public bool Applied { get; init; }
}

/// <summary>
/// Request permission request
/// </summary>
public class RequestPermissionRequest
{
    [JsonPropertyName("options")]
    public List<PermissionOption>? Options { get; init; }
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("toolCall")]
    public ToolCallUpdate? ToolCall { get; init; }
    
    public const string Method = "session/request_permission";
}

/// <summary>
/// ACP session/request_permission 的 result.outcome 嵌套结构（outcome + optionId?）
/// </summary>
public class PermissionOutcome
{
    [JsonPropertyName("outcome")]
    public string Outcome { get; init; } = "";

    [JsonPropertyName("optionId")]
    public string? OptionId { get; init; }
}

/// <summary>
/// session/request_permission 的 outcome 取值
/// </summary>
public static class PermissionOutcomes
{
    public const string Selected = "selected";
    public const string Cancelled = "cancelled";

    /// <summary>构造“已选择”回复（用于允许/拒绝某选项）</summary>
    public static RequestPermissionResponse SelectedResponse(string? optionId) => new()
    {
        Outcome = new PermissionOutcome { Outcome = Selected, OptionId = optionId }
    };

    /// <summary>构造“已取消”回复（prompt turn 被取消时对未决 request_permission 的回复）</summary>
    public static RequestPermissionResponse CancelledResponse() => new()
    {
        Outcome = new PermissionOutcome { Outcome = Cancelled }
    };
}

/// <summary>
/// Request permission response（ACP: result 仅含 outcome 对象）
/// </summary>
public class RequestPermissionResponse
{
    [JsonPropertyName("outcome")]
    public PermissionOutcome? Outcome { get; init; }
}

/// <summary>
/// Create terminal request
/// </summary>
public class CreateTerminalRequest
{
    [JsonPropertyName("command")]
    public string Command { get; init; } = "";
    
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("args")]
    public List<string>? Args { get; init; }
    
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }
    
    [JsonPropertyName("env")]
    public List<EnvVariable>? Env { get; init; }
    
    [JsonPropertyName("outputByteLimit")]
    public int? OutputByteLimit { get; init; }
    
    public const string Method = "terminal/create";
}

/// <summary>
/// Create terminal response
/// </summary>
public class CreateTerminalResponse
{
    [JsonPropertyName("terminalId")]
    public string TerminalId { get; init; } = "";
}

/// <summary>
/// Terminal output request
/// </summary>
public class TerminalOutputRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("terminalId")]
    public string TerminalId { get; init; } = "";
    
    public const string Method = "terminal/output";
}

/// <summary>
/// Terminal output response
/// </summary>
public class TerminalOutputResponse
{
    [JsonPropertyName("exited")]
    public bool Exited { get; init; }
    
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; init; }
    
    [JsonPropertyName("stdout")]
    public string? Stdout { get; init; }
    
    [JsonPropertyName("stderr")]
    public string? Stderr { get; init; }
}

/// <summary>
/// Release terminal request
/// </summary>
public class ReleaseTerminalRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("terminalId")]
    public string TerminalId { get; init; } = "";
    
    public const string Method = "terminal/release";
}

/// <summary>
/// Release terminal response
/// </summary>
public class ReleaseTerminalResponse
{
    [JsonPropertyName("released")]
    public bool Released { get; init; }
}

/// <summary>
/// Wait for terminal exit request
/// </summary>
public class WaitForTerminalExitRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("terminalId")]
    public string TerminalId { get; init; } = "";
    
    public const string Method = "terminal/wait";
}

/// <summary>
/// Wait for terminal exit response
/// </summary>
public class WaitForTerminalExitResponse
{
    [JsonPropertyName("exitCode")]
    public int ExitCode { get; init; }
}

/// <summary>
/// Kill terminal command request
/// </summary>
public class KillTerminalCommandRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";
    
    [JsonPropertyName("terminalId")]
    public string TerminalId { get; init; } = "";
    
    public const string Method = "terminal/kill";
}

/// <summary>
/// Kill terminal command response
/// </summary>
public class KillTerminalCommandResponse
{
    [JsonPropertyName("killed")]
    public bool Killed { get; init; }
}
