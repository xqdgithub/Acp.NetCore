using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Acp.Transport;

/// <summary>
/// SubprocessClient 配置选项
/// </summary>
public class SubprocessClientOptions
{
    /// <summary>
    /// Stderr 输出目标（向后兼容，推荐使用 StderrReceived 事件）
    /// </summary>
    [Obsolete("Use StderrReceived event on SubprocessClient for more control.")]
    public TextWriter? Stderr { get; init; }
    /// <summary>If set, raw transport events (jsonrpc send/receive, stderr, internal errors) are appended to this writer.</summary>
    public TextWriter? TransportLog { get; init; }

    /// <summary>Optional. If set, used as base for process start; FileName/Arguments are still overridden from constructor.</summary>
    public ProcessStartInfo? StartInfo { get; init; }
    
    /// <summary>
    /// 日志记录器（可选）
    /// </summary>
    public ILogger? Logger { get; init; }
    
    /// <summary>
    /// Stderr 输出的日志级别
    /// </summary>
    public LogLevel StderrLogLevel { get; init; } = LogLevel.Warning;
    
    /// <summary>
    /// 默认启动超时（默认 30 秒）
    /// </summary>
    public TimeSpan DefaultStartTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 默认停止超时（默认 3 秒）
    /// </summary>
    public TimeSpan DefaultStopTimeout { get; init; } = TimeSpan.FromSeconds(3);
    
    /// <summary>
    /// 是否在进程意外退出时自动触发事件（默认 true）
    /// </summary>
    public bool EnableExitEvent { get; init; } = true;
    
    /// <summary>
    /// 是否自动管理 SessionId（默认 true）
    /// </summary>
    public bool AutoManageSessionId { get; init; } = true;
}