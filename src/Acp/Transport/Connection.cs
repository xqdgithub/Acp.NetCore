using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Acp.Protocol;

namespace Acp.Transport;

/// <summary>
/// Transport layer: reads/writes lines and delegates message processing to <see cref="IProtocolHandler"/>.
/// Protocol (method dispatch, JSON-RPC shape) is decoupled from transport (stdio, future WebSocket, etc.).
/// Single read loop: responses to our requests complete pending TCS by id; incoming requests go to handler.
/// </summary>
public class Connection
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter? _transportLog;
    private readonly IProtocolHandler _handler;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponses =
        new(StringComparer.Ordinal);
    protected readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// The protocol handler used for incoming messages. Exposed for subclasses that need to access handler-specific APIs (e.g. Dispatcher).
    /// </summary>
    protected IProtocolHandler Handler => _handler;

    /// <summary>
    /// Create a connection that uses the given protocol handler for incoming messages.
    /// </summary>
    public Connection(TextReader input, TextWriter output, IProtocolHandler handler, JsonSerializerOptions? jsonOptions = null, TextWriter? transportLog = null)
    {
        _input = input;
        _output = output;
        _transportLog = transportLog;
        _handler = handler;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    private async Task LogTransportAsync(string category, string message)
    {
        if (_transportLog == null) return;
        try
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
            await _transportLog.WriteLineAsync(logLine).ConfigureAwait(false);
            await _transportLog.FlushAsync().ConfigureAwait(false);
        }
        catch
        {
            // 记录原始传输日志失败时不影响主流程
        }
    }

    /// <summary>
    /// Send a JSON-RPC request and wait for response. Registers a pending TCS by request id; the single read loop completes it when a response line with matching id is received.
    /// </summary>
    protected async Task<T> SendRequestAsync<T>(string method, object? parameters, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString();
        var request = new { jsonrpc = "2.0", id = requestId, method, @params = parameters };
        var json = JsonSerializer.Serialize(request, _jsonOptions);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingResponses.TryAdd(requestId, tcs))
            throw new InvalidOperationException("Failed to register pending response");

        try
        {
            await LogTransportAsync("SEND", json).ConfigureAwait(false);
            await _output.WriteLineAsync(json);
            await _output.FlushAsync(cancellationToken);

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                var responseLine = await tcs.Task;
                if (string.IsNullOrEmpty(responseLine))
                    throw new InvalidOperationException("Empty response");

                using var doc = JsonDocument.Parse(responseLine);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error))
                    throw new InvalidOperationException($"RPC Error: {error.GetProperty("message").GetString()}");

                if (!root.TryGetProperty("result", out var resultEl))
                    throw new InvalidOperationException("Response has no result (and no error).");
                return JsonSerializer.Deserialize<T>(resultEl, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize response");
            }
        }
        finally
        {
            _pendingResponses.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Send a JSON-RPC notification (no response expected).
    /// </summary>
    protected async Task SendNotificationAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        var notification = new { jsonrpc = "2.0", method, @params = parameters };
        var json = JsonSerializer.Serialize(notification, _jsonOptions);

        await LogTransportAsync("SEND", json).ConfigureAwait(false);
        await _output.WriteLineAsync(json);
        await _output.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Get response id as string key for pending map (string or number in JSON).
    /// </summary>
    private static string? GetResponseId(JsonElement idElement)
    {
        return idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()
            : idElement.GetRawText();
    }

    /// <summary>
    /// Start listening for incoming messages. Single read loop: if line is a response (id + result/error, no method), complete pending TCS; else pass to protocol handler and write back response.
    /// </summary>
    public async Task ListenAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _input.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;
            await LogTransportAsync("RECV", line).ConfigureAwait(false);

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // 响应：有 id 且无 method（即对己方请求的回复）→ 交给对应 SendRequestAsync 的 TCS；不要求一定含 result/error
                var hasMethod = root.TryGetProperty("method", out _);
                var hasId = root.TryGetProperty("id", out var idEl);
                if (!hasMethod && hasId)
                {
                    var id = GetResponseId(idEl);
                    if (!string.IsNullOrEmpty(id) && _pendingResponses.TryRemove(id, out var tcs))
                        tcs.TrySetResult(line);
                    continue;
                }
                // 请求或通知：由 ClientProtocolHandler 处理，若有返回值则写回
                var response = await _handler.ProcessMessageAsync(line, cancellationToken);
                if (response != null)
                {
                    await LogTransportAsync("SEND", response).ConfigureAwait(false);
                    await _output.WriteLineAsync(response);
                    await _output.FlushAsync(cancellationToken);
                }
            }
            catch (JsonException ex)
            {
                await LogTransportAsync("ERROR", $"JsonException: {ex.Message}; Raw={line}").ConfigureAwait(false);
                // 非 JSON 或无效 JSON：仍交给 handler 处理（可能返回解析错误响应）
                try
                {
                    var response = await _handler.ProcessMessageAsync(line, cancellationToken);
                    if (response != null)
                    {
                        await LogTransportAsync("SEND", response).ConfigureAwait(false);
                        await _output.WriteLineAsync(response);
                        await _output.FlushAsync(cancellationToken);
                    }
                }
                catch (Exception innerEx)
                {
                    await LogTransportAsync("ERROR", $"Error processing message: {innerEx.Message}").ConfigureAwait(false);
                    if (_transportLog == null)
                        Console.Error.WriteLine($"Error processing message: {innerEx.Message}");
                }
            }
            catch (Exception ex)
            {
                await LogTransportAsync("ERROR", $"Error processing message: {ex.Message}").ConfigureAwait(false);
                if (_transportLog == null)
                    Console.Error.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        await LogTransportAsync("ERROR", "Connection closed").ConfigureAwait(false);
        foreach (var kv in _pendingResponses)
            kv.Value.TrySetException(new InvalidOperationException("Connection closed"));
        _pendingResponses.Clear();
    }
}
