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
    private readonly IProtocolHandler _handler;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponses =
        new(StringComparer.Ordinal);
    protected readonly JsonSerializerOptions _jsonOptions;

    /// <summary>默认请求超时时间（120 秒）</summary>
    protected TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// The protocol handler used for incoming messages. Exposed for subclasses that need to access handler-specific APIs (e.g. Dispatcher).
    /// </summary>
    protected IProtocolHandler Handler => _handler;

    /// <summary>
    /// Create a connection that uses the given protocol handler for incoming messages.
    /// </summary>
    public Connection(TextReader input, TextWriter output, IProtocolHandler handler, JsonSerializerOptions? jsonOptions = null)
    {
        _input = input;
        _output = output;
        _handler = handler;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Send a JSON-RPC request and wait for response. Registers a pending TCS by request id; the single read loop completes it when a response line with matching id is received.
    /// </summary>
    /// <param name="method">RPC 方法名</param>
    /// <param name="parameters">请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="timeout">可选超时时间，默认使用 <see cref="DefaultTimeout"/></param>
    /// <exception cref="TimeoutException">请求超时时抛出</exception>
    protected async Task<T> SendRequestAsync<T>(string method, object? parameters, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(effectiveTimeout);

        var requestId = Guid.NewGuid().ToString();
        var request = new { jsonrpc = "2.0", id = requestId, method, @params = parameters };
        var json = JsonSerializer.Serialize(request, _jsonOptions);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingResponses.TryAdd(requestId, tcs))
            throw new InvalidOperationException("Failed to register pending response");

        try
        {
            await _output.WriteLineAsync(json);
            await _output.FlushAsync(timeoutCts.Token);

            using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Request '{method}' timed out after {effectiveTimeout.TotalSeconds:F1}s");
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
                    await _output.WriteLineAsync(response);
                    await _output.FlushAsync(cancellationToken);
                }
            }
            catch (JsonException)
            {
                // 非 JSON 或无效 JSON：仍交给 handler 处理（可能返回解析错误响应）
                try
                {
                    var response = await _handler.ProcessMessageAsync(line, cancellationToken);
                    if (response != null)
                    {
                        await _output.WriteLineAsync(response);
                        await _output.FlushAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing message: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        foreach (var kv in _pendingResponses)
            kv.Value.TrySetException(new InvalidOperationException("Connection closed"));
        _pendingResponses.Clear();
    }
}
