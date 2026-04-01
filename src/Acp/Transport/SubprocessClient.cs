using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Acp.Exceptions;
using Acp.Interfaces;
using Acp.Messages;
using Acp.Types;
using Microsoft.Extensions.Logging;

namespace Acp.Transport;

/// <summary>
/// ACP Client that spawns a subprocess as an ACP agent. Implements <see cref="IAgentSessionClient"/> and inherits <see cref="Client"/>.
/// Does not include REPL, console UI, or automatic initialize/session-new; call <see cref="StartAsync"/> then use the Agent API as needed.
/// </summary>
public class SubprocessClient : Client, IAgentSessionClient, IAsyncDisposable, IDisposable
{
    private readonly object _stateLock = new();
    private SubprocessClientState _state = SubprocessClientState.Created;
    private readonly string _command;
    private readonly string[] _args;
    private readonly SubprocessClientOptions _options;
    private readonly CancellationTokenSource _internalCts;
    private readonly ILogger? _logger;
    
    private Process? _process;
    private ClientConnection? _connection;
    private Task? _listenTask;
    private Task? _stderrReaderTask;
    private string? _currentSessionId;
    private int? _exitCode;

    #region 事件定义

    /// <summary>进程启动完成事件</summary>
    public event EventHandler<ProcessStartedEventArgs>? ProcessStarted;
    
    /// <summary>进程退出事件</summary>
    public event EventHandler<ProcessExitedEventArgs>? ProcessExited;
    
    /// <summary>Stderr 输出事件</summary>
    public event EventHandler<StderrEventArgs>? StderrReceived;
    
    /// <summary>连接错误事件</summary>
    public event EventHandler<ConnectionErrorEventArgs>? ConnectionError;
    
    /// <summary>状态变更事件</summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    #endregion

    #region 属性

    /// <summary>当前客户端状态</summary>
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

    /// <summary>当前会话 ID；由 SessionNewAsync / SessionLoadAsync 自动设置</summary>
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

    /// <summary>进程是否已退出</summary>
    public bool HasExited
    {
        get
        {
            lock (_stateLock)
            {
                return _process == null || _process.HasExited;
            }
        }
    }

    /// <summary>进程退出码（仅当 HasExited 为 true 时有效）</summary>
    public int? ExitCode
    {
        get
        {
            lock (_stateLock)
            {
                return _exitCode;
            }
        }
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建子进程客户端实例
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <param name="args">命令参数</param>
    /// <param name="options">可选配置</param>
    public SubprocessClient(string command, string[]? args = null, SubprocessClientOptions? options = null)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _args = args ?? Array.Empty<string>();
        _options = options ?? new SubprocessClientOptions();
        _internalCts = new CancellationTokenSource();
        _logger = _options.Logger;
    }

    #endregion

    #region 状态管理

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

    #endregion

    #region 事件触发方法

    /// <summary>触发进程启动事件</summary>
    protected virtual void OnProcessStarted(ProcessStartedEventArgs e)
    {
        ProcessStarted?.Invoke(this, e);
    }

    /// <summary>触发进程退出事件</summary>
    protected virtual void OnProcessExited(ProcessExitedEventArgs e)
    {
        ProcessExited?.Invoke(this, e);
    }

    /// <summary>触发 Stderr 事件</summary>
    protected virtual void OnStderrReceived(StderrEventArgs e)
    {
        // 触发事件
        StderrReceived?.Invoke(this, e);

        // 向后兼容：写入 TextWriter
#pragma warning disable CS0618 // Type or member is obsolete
        if (_options.Stderr != null)
        {
            try
            {
                _options.Stderr.WriteLine($"[Agent stderr] {e.Line}");
                _options.Stderr.Flush();
            }
            catch { /* 忽略写入错误 */ }
        }
#pragma warning restore CS0618

        // 写入日志
        _logger?.Log(_options.StderrLogLevel, "[Agent stderr] {Line}", e.Line);
    }

    /// <summary>触发连接错误事件</summary>
    protected virtual void OnConnectionError(ConnectionErrorEventArgs e)
    {
        ConnectionError?.Invoke(this, e);
    }

    /// <summary>触发状态变更事件</summary>
    protected virtual void OnStateChanged(StateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, e);
        _logger?.LogDebug("State changed: {OldState} -> {NewState} ({Reason})", e.OldState, e.NewState, e.Reason ?? "unknown");
    }

    #endregion

    #region 生命周期方法

    /// <summary>
    /// 启动子进程并建立连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="startTimeout">启动超时时间（可选，默认使用配置值）</param>
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
        var timeout = startTimeout ?? _options.DefaultStartTimeout;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _internalCts.Token);
            linkedCts.CancelAfter(timeout);

            var startInfo = BuildProcessStartInfo();

            process = new Process { StartInfo = startInfo };
            process.EnableRaisingEvents = true;

            // 订阅进程退出事件
            if (_options.EnableExitEvent)
            {
                process.Exited += HandleProcessExited;
            }

            _logger?.LogInformation("Starting process: {Command} {Arguments}", _command, string.Join(" ", _args));
            process.Start();

            var processId = process.Id;
            _logger?.LogInformation("Process started with PID {ProcessId}", processId);
            OnProcessStarted(new ProcessStartedEventArgs { ProcessId = processId });

            var processInput = process.StandardInput;
            var processOutput = process.StandardOutput;
            var processStderr = process.StandardError;

            // 启动 stderr 读取器
            if (processStderr != null)
            {
                _stderrReaderTask = StartStderrReaderAsync(processStderr, processId, linkedCts.Token);
            }

            lock (_stateLock)
            {
                _process = process;
            }
            
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

    /// <summary>
    /// 停止子进程（优雅关闭或强制终止）
    /// </summary>
    /// <param name="gracefulTimeout">优雅关闭超时时间（可选，默认使用配置值）</param>
    /// <param name="cancellationToken">取消令牌</param>
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

        var timeout = gracefulTimeout ?? _options.DefaultStopTimeout;

        try
        {
            // 取消内部操作
            _internalCts.Cancel();

            Process? processToStop;
            lock (_stateLock)
            {
                processToStop = _process;
            }

            if (processToStop != null && !processToStop.HasExited)
            {
                _logger?.LogInformation("Stopping process gracefully (timeout: {Timeout}s)", timeout.TotalSeconds);

                // 先尝试优雅关闭
                try { processToStop.StandardInput?.Close(); } catch { }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);

                try
                {
                    await processToStop.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogWarning("Process did not exit gracefully, forcing kill");
                    try { processToStop.Kill(entireProcessTree: true); } catch { }
                    await processToStop.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
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
            lock (_stateLock)
            {
                try { _process?.Dispose(); } catch { }
                _process = null;
            }
            
            _connection = null;
            _listenTask = null;
            _stderrReaderTask = null;

            ForceTransitionState(SubprocessClientState.Stopped);
        }
    }

    /// <summary>
    /// 等待进程退出
    /// </summary>
    /// <param name="timeout">超时时间（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>进程是否已退出</returns>
    public async Task<bool> WaitForExitAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        Process? processToWait;
        lock (_stateLock)
        {
            processToWait = _process;
        }

        if (processToWait == null)
            return true;

        if (processToWait.HasExited)
            return true;

        try
        {
            if (timeout.HasValue)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout.Value);
                await processToWait.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            else
            {
                await processToWait.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    #endregion

    #region 私有方法

    private ProcessStartInfo BuildProcessStartInfo()
    {
        var startInfo = _options.StartInfo != null
            ? CloneStartInfo(_options.StartInfo)
            : new ProcessStartInfo();

        startInfo.FileName = _command;
        startInfo.Arguments = string.Join(" ", _args);
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardInputEncoding = System.Text.Encoding.UTF8;
        startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
        //startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        startInfo.CreateNoWindow = true;

        return startInfo;
    }

    private void HandleProcessExited(object? sender, EventArgs e)
    {
        var process = sender as Process;
        var exitCode = process?.ExitCode ?? -1;

        _logger?.LogInformation("Process exited with code {ExitCode}", exitCode);

        if (_options?.Stderr != null && processStderr != null)
            _stderrReaderTask = StartStderrReader(processStderr, _options.Stderr, _internalCts!.Token);

        _connection = new ClientConnection(this, processOutput, processInput, _options?.TransportLog);
        _ = _connection.ListenAsync(_internalCts!.Token);

        // 如果当前是 Running 状态，转换为 Stopped
        TryTransitionState(SubprocessClientState.Running, SubprocessClientState.Stopped);
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

    private void CleanupFailedStart(Process? process)
    {
        if (process != null)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { process.Dispose(); } catch { }
        }
        
        lock (_stateLock)
        {
            _process = null;
        }
        
        _connection = null;
        _listenTask = null;
        _stderrReaderTask = null;
    }

    private static ProcessStartInfo CloneStartInfo(ProcessStartInfo source)
    {
        var clone = new ProcessStartInfo
        {
            UseShellExecute = source.UseShellExecute,
            RedirectStandardInput = source.RedirectStandardInput,
            RedirectStandardOutput = source.RedirectStandardOutput,
            RedirectStandardError = source.RedirectStandardError,
            StandardInputEncoding = source.StandardInputEncoding,
            StandardOutputEncoding = source.StandardOutputEncoding,
            StandardErrorEncoding = source.StandardErrorEncoding,
            CreateNoWindow = source.CreateNoWindow,
            WorkingDirectory = source.WorkingDirectory,
            FileName = source.FileName,
            Arguments = source.Arguments,
        };
        if (source.Environment != null)
        {
            foreach (var key in source.Environment.Keys)
                clone.Environment[key] = source.Environment[key];
        }
        return clone;
    }

    private static Task StartStderrReader(TextReader stderr, TextWriter sink, TextWriter? transportLog, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                string? line;
                while (!cancellationToken.IsCancellationRequested && (line = await stderr.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    if (transportLog != null)
                    {
                        await transportLog.WriteLineAsync($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [STDERR] {line}").ConfigureAwait(false);
                        await transportLog.FlushAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        await sink.WriteLineAsync("[Agent stderr] " + line).ConfigureAwait(false);
                        await sink.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (transportLog != null)
                {
                    try
                    {
                        await transportLog.WriteLineAsync($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] Stderr reader failed: {ex.Message}").ConfigureAwait(false);
                        await transportLog.FlushAsync().ConfigureAwait(false);
                    }
                    catch { /* best effort */ }
                }
            }
        }, cancellationToken);
    }

    #endregion

    // ----- IAgentSessionClient -----

    /// <inheritdoc />
    public Task<InitializeResponse> InitializeAsync(
        int protocolVersion,
        ClientCapabilities? clientCapabilities = null,
        Implementation? clientInfo = null,
        CancellationToken cancellationToken = default)
    {
        var request = new InitializeRequest
        {
            ProtocolVersion = protocolVersion,
            ClientCapabilities = clientCapabilities,
            ClientInfo = clientInfo
        };
        return GetConnectionOrThrow().SendRequestAsync<InitializeResponse>(InitializeRequest.Method, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AuthenticateResponse> AuthenticateAsync(string methodId, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<AuthenticateResponse>(AuthenticateRequest.Method, new AuthenticateRequest { MethodId = methodId }, cancellationToken);

    /// <inheritdoc />
    public async Task<NewSessionResponse> SessionNewAsync(string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default)
    {
        var request = new NewSessionRequest { Cwd = cwd, McpServers = mcpServers ?? new List<McpServerConfig>() };
        var response = await GetConnectionOrThrow().SendRequestAsync<NewSessionResponse>(NewSessionRequest.Method, request, cancellationToken).ConfigureAwait(false);

        // 自动管理 SessionId
        if (_options.AutoManageSessionId)
        {
            lock (_stateLock)
            {
                _currentSessionId = response.SessionId;
            }
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<LoadSessionResponse?> SessionLoadAsync(string sessionId, string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default)
    {
        var request = new LoadSessionRequest { SessionId = sessionId, Cwd = cwd, McpServers = mcpServers ?? new List<McpServerConfig>() };
        var response = await GetConnectionOrThrow().SendRequestAsync<LoadSessionResponse?>(LoadSessionRequest.Method, request, cancellationToken).ConfigureAwait(false);

        // 自动管理 SessionId
        if (response != null && _options.AutoManageSessionId)
        {
            lock (_stateLock)
            {
                _currentSessionId = response.SessionId;
            }
        }

        return response;
    }

    /// <inheritdoc />
    public Task<PromptResponse> SessionPromptAsync(string sessionId, IEnumerable<ContentBlock> prompt, CancellationToken cancellationToken = default)
    {
        var request = new PromptRequest { SessionId = sessionId, Prompt = prompt as List<ContentBlock> ?? new List<ContentBlock>(prompt) };
        return GetConnectionOrThrow().SendRequestAsync<PromptResponse>(PromptRequest.Method, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task SessionCancelAsync(string sessionId, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendNotificationAsync(CancelNotification.Method, new CancelNotification { SessionId = sessionId }, cancellationToken);

    /// <inheritdoc />
    public Task<SetSessionModeResponse> SessionSetModeAsync(string sessionId, string modeId, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<SetSessionModeResponse>(SetSessionModeRequest.Method, new SetSessionModeRequest { SessionId = sessionId, ModeId = modeId }, cancellationToken);

    /// <inheritdoc />
    public Task<SetSessionModelResponse> SessionSetModelAsync(string sessionId, string modelId, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<SetSessionModelResponse>(SetSessionModelRequest.Method, new SetSessionModelRequest { SessionId = sessionId, ModelId = modelId }, cancellationToken);

    /// <inheritdoc />
    public Task<SetSessionConfigOptionResponse> SessionSetConfigOptionAsync(string sessionId, string configId, string value, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<SetSessionConfigOptionResponse>(SetSessionConfigOptionRequest.Method, new SetSessionConfigOptionRequest { SessionId = sessionId, ConfigId = configId, Value = value }, cancellationToken);

    /// <inheritdoc />
    public Task<ForkSessionResponse> SessionForkAsync(string sessionId, string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<ForkSessionResponse>(ForkSessionRequest.Method, new ForkSessionRequest { SessionId = sessionId, Cwd = cwd, McpServers = mcpServers ?? new List<McpServerConfig>() }, cancellationToken);

    /// <inheritdoc />
    public Task<ResumeSessionResponse> SessionResumeAsync(string sessionId, string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<ResumeSessionResponse>(ResumeSessionRequest.Method, new ResumeSessionRequest { SessionId = sessionId, Cwd = cwd, McpServers = mcpServers ?? new List<McpServerConfig>() }, cancellationToken);

    /// <inheritdoc />
    public Task<ListSessionsResponse> SessionListAsync(string? cwd = null, string? cursor = null, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<ListSessionsResponse>(ListSessionsRequest.Method, new ListSessionsRequest { Cwd = cwd, Cursor = cursor }, cancellationToken);

    #region 资源释放

    /// <summary>异步释放资源（推荐使用 await using）</summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _internalCts.Dispose();
        
        ForceTransitionState(SubprocessClientState.Disposed);
        
        GC.SuppressFinalize(this);
    }

    /// <summary>同步释放资源（向后兼容，但在同步上下文中可能阻塞）</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Override to add dispose logic; always call base.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // 尝试异步停止，但在同步上下文中可能阻塞
                StopAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // 最后手段：强制终止进程
                try
                {
                    Process? processToKill;
                    lock (_stateLock)
                    {
                        processToKill = _process;
                    }
                    processToKill?.Kill(entireProcessTree: true);
                }
                catch { }
            }
            _internalCts.Dispose();
            
            ForceTransitionState(SubprocessClientState.Disposed);
        }
    }

    #endregion
}
