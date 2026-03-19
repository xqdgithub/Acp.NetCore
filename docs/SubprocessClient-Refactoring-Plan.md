# SubprocessClient 重构设计方案

## 一、当前问题总结

### 1.1 资源管理问题

| 问题 | 严重程度 | 影响 |
|------|----------|------|
| `ListenAsync` 返回的 Task 被丢弃 | 🔴 高 | 异常被静默吞掉，无法监控连接状态 |
| 进程 `Exited` 事件未监听 | 🔴 高 | 无法感知进程意外退出 |
| `_internalCts` 类型不一致 | 🟡 中 | 可空类型但实际从不为 null |

### 1.2 线程安全问题

| 问题 | 严重程度 | 影响 |
|------|----------|------|
| 状态字段无锁保护 | 🔴 高 | 多线程访问导致竞态条件 |
| 重复调用 `StartAsync` 无防护 | 🔴 高 | 资源泄漏 |
| `HasExited` 属性竞态条件 | 🟡 中 | 读取过期状态 |

### 1.3 错误处理问题

| 问题 | 严重程度 | 影响 |
|------|----------|------|
| Stderr 读取器异常被吞 | 🟡 中 | 调试困难 |
| `StopAsync` 异常处理过宽 | 🟡 中 | 掩盖真实错误 |
| 启动失败缺少上下文 | 🟢 低 | 错误信息不友好 |

### 1.4 API 设计问题

| 问题 | 严重程度 | 影响 |
|------|----------|------|
| `CurrentSessionId` 需手动设置 | 🟡 中 | 易出错、违反封装 |
| `StopAsync` 使用 int 毫秒 | 🟢 低 | 不符合 .NET 惯例 |
| 缺少生命周期事件 | 🟡 中 | 调用者无法监控状态变化 |
| Stderr 输出机制单一 | 🟡 中 | 只支持 TextWriter |

### 1.5 可用性问题

| 问题 | 严重程度 | 影响 |
|------|----------|------|
| 缺少启动超时 | 🟡 中 | 进程挂起无法控制 |
| 无重连机制 | 🟢 低 | 需重新创建实例 |
| 缺少日志集成 | 🟢 低 | 调试困难 |

---

## 二、改进目标

### 2.1 核心目标

1. **健壮性**：确保资源正确管理，异常可追溯
2. **线程安全**：所有状态访问受保护
3. **可观测性**：提供完整的事件和日志
4. **易用性**：API 符合 .NET 最佳实践
5. **向后兼容**：保持现有 API 兼容

### 2.2 改进原则

- **最小破坏性**：优先添加而非修改 API
- **渐进式改进**：分阶段实施
- **遵循框架惯例**：使用 `TimeSpan`、`ILogger`、事件模式等

---

## 三、改进后架构设计

### 3.1 类结构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                         SubprocessClient                             │
├─────────────────────────────────────────────────────────────────────┤
│ - _stateLock: object                                                 │
│ - _state: SubprocessClientState                                      │
│ - _process: Process?                                                 │
│ - _connection: ClientConnection?                                     │
│ - _listenTask: Task?                                                 │
│ - _stderrReaderTask: Task?                                           │
│ - _internalCts: CancellationTokenSource                              │
│ - _options: SubprocessClientOptions                                  │
│ - _logger: ILogger?                                                  │
├─────────────────────────────────────────────────────────────────────┤
│ + ProcessStarted: EventHandler<ProcessStartedEventArgs>?             │
│ + ProcessExited: EventHandler<ProcessExitedEventArgs>?               │
│ + StderrReceived: EventHandler<StderrEventArgs>?                     │
│ + ConnectionError: EventHandler<ConnectionErrorEventArgs>?           │
│ + StateChanged: EventHandler<StateChangedEventArgs>?                 │
├─────────────────────────────────────────────────────────────────────┤
│ + State: SubprocessClientState (readonly)                            │
│ + CurrentSessionId: string? (readonly)                               │
│ + HasExited: bool                                                    │
│ + ExitCode: int?                                                     │
├─────────────────────────────────────────────────────────────────────┤
│ + StartAsync(cancellationToken, startTimeout)                        │
│ + StopAsync(gracefulTimeout, cancellationToken)                      │
│ + WaitForExitAsync(timeout, cancellationToken)                       │
│ + IAgentSessionClient 方法                                           │
├─────────────────────────────────────────────────────────────────────┤
│ # OnProcessStarted(args)                                             │
│ # OnProcessExited(args)                                              │
│ # OnStderrReceived(args)                                             │
│ # OnConnectionError(args)                                            │
│ # DisposeAsync()                                                     │
│ # Dispose(disposing)                                                 │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 继承
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                             Client                                   │
│                      (IClient 实现)                                  │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.2 状态机设计

```
                    ┌─────────────┐
                    │   Created   │
                    └──────┬──────┘
                           │ StartAsync()
                           ▼
                    ┌─────────────┐
           ┌───────►│   Starting  │◄───────┐
           │        └──────┬──────┘        │
           │               │               │
           │    成功       │               │ 失败
           │               ▼               │
           │        ┌─────────────┐        │
           │        │   Running   │        │
           │        └──────┬──────┘        │
           │               │               │
           │               │ StopAsync()   │
           │               │ 或进程退出     │
           │               ▼               │
           │        ┌─────────────┐        │
           │        │   Stopping  │────────┘
           │        └──────┬──────┘
           │               │
           │               ▼
           │        ┌─────────────┐
           └────────│   Stopped   │
                    └─────────────┘
                           │
                           │ Dispose()
                           ▼
                    ┌─────────────┐
                    │  Disposed   │
                    └─────────────┘
```

### 3.3 新增类型定义

#### 3.3.1 状态枚举

```csharp
namespace Acp.Transport;

/// <summary>
/// SubprocessClient 生命周期状态
/// </summary>
public enum SubprocessClientState
{
    /// <summary>已创建，未启动</summary>
    Created = 0,
    
    /// <summary>正在启动进程</summary>
    Starting = 1,
    
    /// <summary>进程运行中</summary>
    Running = 2,
    
    /// <summary>正在停止进程</summary>
    Stopping = 3,
    
    /// <summary>已停止（可重新启动）</summary>
    Stopped = 4,
    
    /// <summary>已释放（不可重用）</summary>
    Disposed = 5
}
```

#### 3.3.2 事件参数类型

```csharp
namespace Acp.Transport;

/// <summary>进程启动事件参数</summary>
public class ProcessStartedEventArgs : EventArgs
{
    /// <summary>进程 ID</summary>
    public int ProcessId { get; init; }
    
    /// <summary>启动时间</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>进程退出事件参数</summary>
public class ProcessExitedEventArgs : EventArgs
{
    /// <summary>退出码</summary>
    public int ExitCode { get; init; }
    
    /// <summary>是否正常退出（ExitCode == 0）</summary>
    public bool IsNormalExit => ExitCode == 0;
    
    /// <summary>退出时间</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Stderr 输出事件参数</summary>
public class StderrEventArgs : EventArgs
{
    /// <summary>输出行内容</summary>
    public string Line { get; init; } = "";
    
    /// <summary>进程 ID（如果可用）</summary>
    public int? ProcessId { get; init; }
    
    /// <summary>时间戳</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>连接错误事件参数</summary>
public class ConnectionErrorEventArgs : EventArgs
{
    /// <summary>错误异常</summary>
    public Exception Error { get; init; } = null!;
    
    /// <summary>是否为致命错误（连接已断开）</summary>
    public bool IsFatal { get; init; }
    
    /// <summary>时间戳</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>状态变更事件参数</summary>
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
```

#### 3.3.3 改进后的 Options

```csharp
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
    
    /// <summary>
    /// 可选的进程启动信息基础配置
    /// </summary>
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
```

### 3.4 接口改进

#### 3.4.1 ISubprocessClient 接口（新增）

```csharp
namespace Acp.Transport;

/// <summary>
/// 子进程客户端接口，提供进程生命周期管理能力
/// </summary>
public interface ISubprocessClient : IAgentSessionClient, IAsyncDisposable
{
    /// <summary>当前状态</summary>
    SubprocessClientState State { get; }
    
    /// <summary>当前会话 ID（由 SessionNew/SessionLoad 自动设置）</summary>
    string? CurrentSessionId { get; }
    
    /// <summary>进程是否已退出</summary>
    bool HasExited { get; }
    
    /// <summary>进程退出码（仅当 HasExited 为 true 时有效）</summary>
    int? ExitCode { get; }
    
    /// <summary>进程启动事件</summary>
    event EventHandler<ProcessStartedEventArgs>? ProcessStarted;
    
    /// <summary>进程退出事件</summary>
    event EventHandler<ProcessExitedEventArgs>? ProcessExited;
    
    /// <summary>Stderr 输出事件</summary>
    event EventHandler<StderrEventArgs>? StderrReceived;
    
    /// <summary>连接错误事件</summary>
    event EventHandler<ConnectionErrorEventArgs>? ConnectionError;
    
    /// <summary>状态变更事件</summary>
    event EventHandler<StateChangedEventArgs>? StateChanged;
    
    /// <summary>启动子进程</summary>
    Task StartAsync(CancellationToken cancellationToken = default, TimeSpan? startTimeout = null);
    
    /// <summary>停止子进程</summary>
    Task StopAsync(TimeSpan? gracefulTimeout = null, CancellationToken cancellationToken = default);
    
    /// <summary>等待进程退出</summary>
    Task WaitForExitAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
```

---

## 四、分阶段实施计划

### 第一阶段：核心修复（必须）

**目标**：修复资源管理和线程安全问题

**任务清单**：

1. **修复 `_internalCts` 类型**
   - 从 `CancellationTokenSource?` 改为 `CancellationTokenSource`
   - 移除所有 `!` 非空断言

2. **保存并监控 `ListenAsync` 返回的 Task**
   - 添加 `_listenTask` 字段
   - 在 `StopAsync` 中等待该 Task 完成
   - 添加 `ConnectionError` 事件处理监听异常

3. **监听进程 `Exited` 事件**
   - 添加 `ProcessExited` 事件
   - 订阅 `process.Exited` 事件

4. **添加状态锁和状态机**
   - 添加 `_stateLock` 对象
   - 添加 `SubprocessClientState` 枚举
   - 实现 `StateChanged` 事件

5. **防止重复调用 `StartAsync`**
   - 在 `StartAsync` 开始时检查状态
   - 如果已启动则抛出异常

**预计工作量**：4-6 小时

### 第二阶段：API 改进（推荐）

**目标**：改进 API 易用性和符合 .NET 惯例

**任务清单**：

1. **自动管理 `CurrentSessionId`**
   - 在 `SessionNewAsync` 成功后自动设置
   - 在 `SessionLoadAsync` 成功后自动设置
   - 移除 public setter

2. **使用 `TimeSpan` 替代 `int` 毫秒**
   - `StopAsync(int gracefulTimeoutMs)` → `StopAsync(TimeSpan? gracefulTimeout)`
   - 添加默认值使用 `SubprocessClientOptions.DefaultStopTimeout`

3. **添加启动超时参数**
   - `StartAsync` 添加 `TimeSpan? startTimeout` 参数
   - 实现启动超时逻辑

4. **添加 `WaitForExitAsync` 方法**
   - 异步等待进程退出
   - 支持超时和取消

**预计工作量**：2-3 小时

### 第三阶段：可观测性增强（推荐）

**目标**：提供完整的事件和日志支持

**任务清单**：

1. **完善事件系统**
   - 添加 `ProcessStartedEventArgs`
   - 添加 `ProcessExitedEventArgs`
   - 添加 `StderrEventArgs`
   - 添加 `ConnectionErrorEventArgs`
   - 添加 `StateChangedEventArgs`

2. **集成 `ILogger`**
   - 在关键操作点添加日志
   - 使用结构化日志

3. **改进 Stderr 处理**
   - 触发 `StderrReceived` 事件
   - 同时支持 TextWriter（向后兼容）
   - 支持 ILogger 输出

**预计工作量**：3-4 小时

### 第四阶段：异常处理改进（可选）

**目标**：完善异常处理，提供更好的错误信息

**任务清单**：

1. **使用 `TransportException` 包装异常**
   - 启动失败时包装原始异常
   - 包含命令和参数信息

2. **改进 `StopAsync` 异常处理**
   - 区分不同类型的异常
   - 记录异常到日志

3. **添加异常上下文**
   - 在异常消息中包含命令、参数、工作目录等

**预计工作量**：1-2 小时

### 第五阶段：高级功能（可选）

**目标**：添加高级功能，提升可用性

**任务清单**：

1. **自动重连机制**
   - 添加 `AutoReconnect` 选项
   - 实现重连逻辑

2. **健康检查**
   - 添加 `IsHealthy` 属性
   - 添加 `CheckHealthAsync` 方法

3. **进程输出捕获**
   - 添加 `ProcessOutput` 事件
   - 支持捕获 stdout（调试用）

**预计工作量**：4-6 小时

---

## 五、关键实现示例

### 5.1 状态管理核心实现

```csharp
public class SubprocessClient : Client, ISubprocessClient
{
    private readonly object _stateLock = new();
    private SubprocessClientState _state = SubprocessClientState.Created;
    private readonly CancellationTokenSource _internalCts;
    private Process? _process;
    private ClientConnection? _connection;
    private Task? _listenTask;
    private Task? _stderrReaderTask;
    private string? _currentSessionId;
    private int? _exitCode;

    public SubprocessClientState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    private bool TryTransitionState(
        SubprocessClientState expectedState, 
        SubprocessClientState newState, 
        [CallerMemberName] string? reason = null)
    {
        lock (_stateLock)
        {
            if (_state != expectedState)
                return false;
            
            var oldState = _state;
            _state = newState;
            
            OnStateChanged(new StateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                Reason = reason
            });
            
            return true;
        }
    }

    private void ForceTransitionState(
        SubprocessClientState newState, 
        [CallerMemberName] string? reason = null)
    {
        lock (_stateLock)
        {
            var oldState = _state;
            _state = newState;
            
            OnStateChanged(new StateChangedEventArgs
            {
                OldState = oldState,
                NewState = newState,
                Reason = reason
            });
        }
    }
}
```

### 5.2 改进后的 StartAsync

```csharp
public virtual async Task StartAsync(
    CancellationToken cancellationToken = default,
    TimeSpan? startTimeout = null)
{
    if (!TryTransitionState(SubprocessClientState.Created, SubprocessClientState.Starting))
    {
        var currentState = State;
        throw new InvalidOperationException(
            $"Cannot start: client is in {currentState} state. " +
            $"Only {nameof(SubprocessClientState.Created)} state allows starting.");
    }

    Process? process = null;
    var timeout = startTimeout ?? _options?.DefaultStartTimeout ?? TimeSpan.FromSeconds(30);
    
    try
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _internalCts.Token);
        linkedCts.CancelAfter(timeout);

        var startInfo = BuildProcessStartInfo();

        process = new Process { StartInfo = startInfo };
        process.EnableRaisingEvents = true;
        
        // 订阅进程退出事件
        process.Exited += HandleProcessExited;

        _logger?.LogInformation("Starting process: {Command} {Arguments}", _command, string.Join(" ", _args));
        process.Start();

        _logger?.LogInformation("Process started with PID {ProcessId}", process.Id);
        OnProcessStarted(new ProcessStartedEventArgs { ProcessId = process.Id });

        var processInput = process.StandardInput;
        var processOutput = process.StandardOutput;
        var processStderr = process.StandardError;

        // 启动 stderr 读取器
        if (processStderr != null)
        {
            _stderrReaderTask = StartStderrReaderAsync(processStderr, process.Id, linkedCts.Token);
        }

        _process = process;
        _connection = new ClientConnection(this, processOutput, processInput);
        
        // 保存监听任务以便监控异常
        _listenTask = _connection.ListenAsync(linkedCts.Token);
        _ = MonitorListenTaskAsync(_listenTask);

        TryTransitionState(SubprocessClientState.Starting, SubprocessClientState.Running);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        CleanupFailedStart(process);
        ForceTransitionState(SubprocessClientState.Stopped);
        throw new TimeoutException($"Process start timed out after {timeout.TotalSeconds:F1} seconds");
    }
    catch (Exception ex)
    {
        CleanupFailedStart(process);
        ForceTransitionState(SubprocessClientState.Stopped);
        
        _logger?.LogError(ex, "Failed to start process: {Command}", _command);
        throw new TransportException(
            $"Failed to start subprocess '{_command}': {ex.Message}", ex);
    }
}

private async Task MonitorListenTaskAsync(Task listenTask)
{
    try
    {
        await listenTask.ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // 正常取消，不是错误
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Connection listener failed");
        OnConnectionError(new ConnectionErrorEventArgs
        {
            Error = ex,
            IsFatal = true
        });
    }
}

private void HandleProcessExited(object? sender, EventArgs e)
{
    var process = (Process?)sender;
    var exitCode = process?.ExitCode ?? -1;
    
    _logger?.LogInformation("Process exited with code {ExitCode}", exitCode);
    
    lock (_stateLock)
    {
        _exitCode = exitCode;
    }
    
    OnProcessExited(new ProcessExitedEventArgs { ExitCode = exitCode });
    
    // 如果当前是 Running 状态，转换为 Stopped
    TryTransitionState(SubprocessClientState.Running, SubprocessClientState.Stopped);
}

private void CleanupFailedStart(Process? process)
{
    if (process != null)
    {
        try { process.Kill(entireProcessTree: true); } catch { }
        try { process.Dispose(); } catch { }
    }
    _process = null;
    _connection = null;
    _listenTask = null;
    _stderrReaderTask = null;
}
```

### 5.3 改进后的 StopAsync

```csharp
public virtual async Task StopAsync(
    TimeSpan? gracefulTimeout = null,
    CancellationToken cancellationToken = default)
{
    SubprocessClientState previousState;
    lock (_stateLock)
    {
        previousState = _state;
        
        if (previousState != SubprocessClientState.Running)
        {
            // 已经停止或未启动，无需操作
            return;
        }
        
        _state = SubprocessClientState.Stopping;
    }

    OnStateChanged(new StateChangedEventArgs
    {
        OldState = previousState,
        NewState = SubprocessClientState.Stopping,
        Reason = nameof(StopAsync)
    });

    var timeout = gracefulTimeout ?? _options?.DefaultStopTimeout ?? TimeSpan.FromSeconds(3);
    
    try
    {
        // 取消内部操作
        _internalCts.Cancel();

        if (_process != null && !_process.HasExited)
        {
            _logger?.LogInformation("Stopping process gracefully (timeout: {Timeout}s)", timeout.TotalSeconds);
            
            // 先尝试优雅关闭
            try { _process.StandardInput?.Close(); } catch { }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            try
            {
                await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Process did not exit gracefully, forcing kill");
                try { _process.Kill(entireProcessTree: true); } catch { }
                await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        // 等待监听任务完成
        if (_listenTask != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _listenTask.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch { /* 忽略 */ }
        }

        // 等待 stderr 读取器完成
        if (_stderrReaderTask != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _stderrReaderTask.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch { /* 忽略 */ }
        }

        _logger?.LogInformation("Process stopped successfully");
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Error during process stop");
    }
    finally
    {
        // 清理资源
        try { _process?.Dispose(); } catch { }
        
        _process = null;
        _connection = null;
        _listenTask = null;
        _stderrReaderTask = null;
        
        ForceTransitionState(SubprocessClientState.Stopped);
    }
}
```

### 5.4 自动管理 SessionId

```csharp
public string? CurrentSessionId
{
    get
    {
        lock (_stateLock)
        {
            return _currentSessionId;
        }
    }
}

public async Task<NewSessionResponse> SessionNewAsync(
    string cwd,
    List<McpServerConfig>? mcpServers = null,
    CancellationToken cancellationToken = default)
{
    var request = new NewSessionRequest 
    { 
        Cwd = cwd, 
        McpServers = mcpServers ?? new List<McpServerConfig>() 
    };
    
    var response = await GetConnectionOrThrow()
        .SendRequestAsync<NewSessionResponse>(NewSessionRequest.Method, request, cancellationToken)
        .ConfigureAwait(false);
    
    // 自动管理 SessionId
    if (_options?.AutoManageSessionId != false)
    {
        lock (_stateLock)
        {
            _currentSessionId = response.SessionId;
        }
    }
    
    return response;
}

public async Task<LoadSessionResponse?> SessionLoadAsync(
    string sessionId,
    string cwd,
    List<McpServerConfig>? mcpServers = null,
    CancellationToken cancellationToken = default)
{
    var request = new LoadSessionRequest 
    { 
        SessionId = sessionId, 
        Cwd = cwd, 
        McpServers = mcpServers ?? new List<McpServerConfig>() 
    };
    
    var response = await GetConnectionOrThrow()
        .SendRequestAsync<LoadSessionResponse?>(LoadSessionRequest.Method, request, cancellationToken)
        .ConfigureAwait(false);
    
    // 自动管理 SessionId
    if (response != null && _options?.AutoManageSessionId != false)
    {
        lock (_stateLock)
        {
            _currentSessionId = response.SessionId;
        }
    }
    
    return response;
}
```

### 5.5 事件触发方法

```csharp
public event EventHandler<ProcessStartedEventArgs>? ProcessStarted;
public event EventHandler<ProcessExitedEventArgs>? ProcessExited;
public event EventHandler<StderrEventArgs>? StderrReceived;
public event EventHandler<ConnectionErrorEventArgs>? ConnectionError;
public event EventHandler<StateChangedEventArgs>? StateChanged;

protected virtual void OnProcessStarted(ProcessStartedEventArgs e)
{
    ProcessStarted?.Invoke(this, e);
}

protected virtual void OnProcessExited(ProcessExitedEventArgs e)
{
    ProcessExited?.Invoke(this, e);
}

protected virtual void OnStderrReceived(StderrEventArgs e)
{
    // 触发事件
    StderrReceived?.Invoke(this, e);
    
    // 向后兼容：写入 TextWriter
#pragma warning disable CS0618 // Type or member is obsolete
    if (_options?.Stderr != null)
    {
        try
        {
            _options.Stderr.WriteLine($"[Agent stderr] {e.Line}");
            _options.Stderr.Flush();
        }
        catch { /* 忽略 */ }
    }
#pragma warning restore CS0618
    
    // 写入日志
    _options?.Logger?.Log(_options.StderrLogLevel, "[Agent stderr] {Line}", e.Line);
}

protected virtual void OnConnectionError(ConnectionErrorEventArgs e)
{
    ConnectionError?.Invoke(this, e);
}

protected virtual void OnStateChanged(StateChangedEventArgs e)
{
    StateChanged?.Invoke(this, e);
}
```

### 5.6 改进后的 Stderr 读取器

```csharp
private async Task StartStderrReaderAsync(TextReader stderr, int processId, CancellationToken cancellationToken)
{
    try
    {
        string? line;
        while (!cancellationToken.IsCancellationRequested && 
               (line = await stderr.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            OnStderrReceived(new StderrEventArgs
            {
                Line = line,
                ProcessId = processId
            });
        }
    }
    catch (OperationCanceledException)
    {
        // 正常取消
    }
    catch (Exception ex)
    {
        _options?.Logger?.LogError(ex, "Stderr reader failed");
    }
}
```

---

## 六、迁移指南

### 6.1 破坏性变更

| 变更 | 影响 | 迁移方式 |
|------|------|----------|
| `StopAsync(int)` 改为 `StopAsync(TimeSpan?)` | 编译错误 | 使用 `TimeSpan.FromMilliseconds()` 或直接传 `TimeSpan` |
| `CurrentSessionId` 变为只读 | 编译错误 | 移除手动设置代码，自动管理 |
| `SubprocessClientOptions.Stderr` 标记过时 | 编译警告 | 使用 `StderrReceived` 事件 |

### 6.2 迁移示例

**旧代码**：
```csharp
var client = new SubprocessClient("node", new[] { "agent.js" });
await client.StartAsync();
await client.SessionNewAsync("/workspace");
client.CurrentSessionId = "my-session"; // 手动设置
await client.StopAsync(5000); // 毫秒
```

**新代码**：
```csharp
var client = new SubprocessClient("node", new[] { "agent.js" });
client.ProcessExited += (s, e) => Console.WriteLine($"进程退出: {e.ExitCode}");
client.StderrReceived += (s, e) => Console.WriteLine($"Stderr: {e.Line}");

await client.StartAsync(startTimeout: TimeSpan.FromSeconds(30));
var session = await client.SessionNewAsync("/workspace");
// CurrentSessionId 自动设置，无需手动设置
Console.WriteLine($"Session: {client.CurrentSessionId}");

await client.StopAsync(TimeSpan.FromSeconds(5));
```

---

## 七、测试计划

### 7.1 单元测试

- [ ] 状态转换正确性测试
- [ ] 重复 StartAsync 抛出异常
- [ ] 进程退出事件触发测试
- [ ] Stderr 事件触发测试
- [ ] 启动超时测试
- [ ] 停止超时测试
- [ ] SessionId 自动管理测试

### 7.2 集成测试

- [ ] 正常启动/停止流程
- [ ] 进程崩溃恢复
- [ ] 连接断开处理
- [ ] 并发访问测试
- [ ] 取消令牌传播测试

### 7.3 性能测试

- [ ] 内存泄漏检测
- [ ] 长时间运行稳定性
- [ ] 高频消息处理

---

## 八、时间估算

| 阶段 | 优先级 | 工作量 | 建议时间 |
|------|--------|--------|----------|
| 第一阶段：核心修复 | 必须 | 4-6 小时 | 第 1 天 |
| 第二阶段：API 改进 | 推荐 | 2-3 小时 | 第 2 天上午 |
| 第三阶段：可观测性增强 | 推荐 | 3-4 小时 | 第 2 天下午 |
| 第四阶段：异常处理改进 | 可选 | 1-2 小时 | 第 3 天 |
| 第五阶段：高级功能 | 可选 | 4-6 小时 | 后续迭代 |
| **总计** | - | **14-21 小时** | **2-3 天** |

---

## 九、风险评估

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| 破坏现有用户代码 | 中 | 中 | 提供迁移指南，保留过时 API |
| 引入新的竞态条件 | 低 | 高 | 充分的单元测试和并发测试 |
| 性能回归 | 低 | 中 | 性能基准测试 |
| 文档不完善 | 中 | 低 | 同步更新 README 和 API 文档 |

---

## 十、验收标准

### 第一阶段验收标准

- [ ] 所有状态字段通过锁访问
- [ ] 重复 `StartAsync` 抛出 `InvalidOperationException`
- [ ] 进程退出时触发 `ProcessExited` 事件
- [ ] `ListenAsync` 异常通过 `ConnectionError` 事件报告

### 第二阶段验收标准

- [ ] `SessionNewAsync` 成功后 `CurrentSessionId` 自动设置
- [ ] `StopAsync` 接受 `TimeSpan` 参数
- [ ] `StartAsync` 支持启动超时

### 第三阶段验收标准

- [ ] 所有事件正常触发并携带正确信息
- [ ] 结构化日志正常输出
- [ ] Stderr 同时支持事件、TextWriter 和 ILogger

---

*文档版本：1.0*  
*创建日期：2026-03-19*  
*作者：Sisyphus AI Agent*