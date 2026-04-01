using System;

namespace Acp.Transport;

/// <summary>
/// 进程启动事件参数
/// </summary>
public class ProcessStartedEventArgs : EventArgs
{
    /// <summary>进程 ID</summary>
    public int ProcessId { get; init; }
    
    /// <summary>启动时间</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 进程退出事件参数
/// </summary>
public class ProcessExitedEventArgs : EventArgs
{
    /// <summary>退出码</summary>
    public int ExitCode { get; init; }
    
    /// <summary>是否正常退出（ExitCode == 0）</summary>
    public bool IsNormalExit => ExitCode == 0;
    
    /// <summary>退出时间</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Stderr 输出事件参数
/// </summary>
public class StderrEventArgs : EventArgs
{
    /// <summary>输出行内容</summary>
    public string Line { get; init; } = "";
    
    /// <summary>进程 ID（如果可用）</summary>
    public int? ProcessId { get; init; }
    
    /// <summary>时间戳</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 连接错误事件参数
/// </summary>
public class ConnectionErrorEventArgs : EventArgs
{
    /// <summary>错误异常</summary>
    public Exception Error { get; init; } = null!;
    
    /// <summary>是否为致命错误（连接已断开）</summary>
    public bool IsFatal { get; init; }
    
    /// <summary>时间戳</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 状态变更事件参数
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    /// <summary>旧状态</summary>
    public SubprocessClientState OldState { get; init; }
    
    /// <summary>新状态</summary>
    public SubprocessClientState NewState { get; init; }
    
    /// <summary>变更原因（可选）</summary>
    public string? Reason { get; init; }
    
    /// <summary>时间戳</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}