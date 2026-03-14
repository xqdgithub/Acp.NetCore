using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Acp.Interfaces;
using Acp.Messages;
using Acp.Types;

namespace Acp.Transport;

/// <summary>
/// ACP Client that spawns a subprocess as an ACP agent. Implements <see cref="IAgentSessionClient"/> and inherits <see cref="Client"/>.
/// Does not include REPL, console UI, or automatic initialize/session-new; call <see cref="StartAsync"/> then use the Agent API as needed.
/// </summary>
public class SubprocessClient : Client, IAgentSessionClient, IDisposable
{
    private readonly string _command;
    private readonly string[] _args;
    private readonly SubprocessClientOptions? _options;
    private readonly CancellationTokenSource? _internalCts;
    private ClientConnection? _connection;
    private Process? _process;
    private Task? _stderrReaderTask;

    /// <summary>Current session ID; set by caller after SessionNewAsync / SessionLoadAsync.</summary>
    public string CurrentSessionId { get; set; } = "";

    public SubprocessClient(string command, string[] args, SubprocessClientOptions? options = null)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _args = args ?? Array.Empty<string>();
        _options = options;
        _internalCts = new CancellationTokenSource();
    }

    /// <summary>Connection to the subprocess agent; null until <see cref="StartAsync"/> has been called.</summary>
    protected ClientConnection? Connection => _connection;

    /// <summary>Indicates whether the subprocess has exited.</summary>
    public bool HasExited => _process == null || _process.HasExited;

    /// <summary>Throws if connection is not established (StartAsync not called or already stopped).</summary>
    protected ClientConnection GetConnectionOrThrow()
    {
        if (_connection == null)
            throw new InvalidOperationException("Connection not established. Call StartAsync first.");
        return _connection;
    }

    /// <summary>Start the subprocess and begin listening. Returns when the connection is ready; then call InitializeAsync, SessionNewAsync, etc. as needed.</summary>
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = _options?.StartInfo != null
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
        startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        startInfo.CreateNoWindow = true;

        _process = new Process { StartInfo = startInfo };
        _process.EnableRaisingEvents = true;
        _process.Start();

        var processInput = _process.StandardInput;
        var processOutput = _process.StandardOutput;
        var processStderr = _process.StandardError;

        if (_options?.Stderr != null && processStderr != null)
            _stderrReaderTask = StartStderrReader(processStderr, _options.Stderr, _internalCts!.Token);

        _connection = new ClientConnection(this, processOutput, processInput);
        _ = _connection.ListenAsync(_internalCts!.Token);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Stop the subprocess gracefully (kill if still running after timeout) and clear the connection.</summary>
    public virtual async Task StopAsync(int gracefulTimeoutMs = 3000)
    {
        // Cancel internal operations first
        _internalCts?.Cancel();

        if (_process != null && !_process.HasExited)
        {
            try
            {
                // Try graceful shutdown first: close input stream to signal EOF to the process
                try { _process.StandardInput?.Close(); } catch { /* best effort */ }

                // Wait for graceful exit with timeout
                var exited = _process.WaitForExit(gracefulTimeoutMs);
                if (!exited)
                {
                    // Force kill if still running
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited - this is fine
            }
            catch (Exception)
            {
                // Best effort kill if other methods failed
                try { _process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }
            finally
            {
                // Ensure process resources are released
                try { _process.Dispose(); } catch { /* best effort */ }
            }
        }

        // Wait for stderr reader to complete (with timeout)
        if (_stderrReaderTask != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(1000);
                await _stderrReaderTask.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
            catch (Exception) { /* best effort */ }
            _stderrReaderTask = null;
        }

        _connection = null;
        _process = null;
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

    private static Task StartStderrReader(TextReader stderr, TextWriter sink, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                string? line;
                while (!cancellationToken.IsCancellationRequested && (line = await stderr.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    await sink.WriteLineAsync("[Agent stderr] " + line).ConfigureAwait(false);
                    await sink.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { /* best effort */ }
        }, cancellationToken);
    }

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
    public Task<NewSessionResponse> SessionNewAsync(string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default)
    {
        var request = new NewSessionRequest { Cwd = cwd, McpServers = mcpServers ?? new List<McpServerConfig>() };
        return GetConnectionOrThrow().SendRequestAsync<NewSessionResponse>(NewSessionRequest.Method, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<LoadSessionResponse?> SessionLoadAsync(string sessionId, string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default)
    {
        var request = new LoadSessionRequest { SessionId = sessionId, Cwd = cwd, McpServers = mcpServers ?? new List<McpServerConfig>() };
        return GetConnectionOrThrow().SendRequestAsync<LoadSessionResponse?>(LoadSessionRequest.Method, request, cancellationToken);
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
    public Task<ListSessionsResponse> SessionListAsync(string? cwd = null, string? cursor = null, CancellationToken cancellationToken = default)
        => GetConnectionOrThrow().SendRequestAsync<ListSessionsResponse>(ListSessionsRequest.Method, new ListSessionsRequest { Cwd = cwd, Cursor = cursor }, cancellationToken);

    /// <summary>Dispose stops the subprocess and releases resources.</summary>
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
            try { StopAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }
            _internalCts?.Dispose();
        }
    }
}
