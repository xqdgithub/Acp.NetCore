using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Acp.Interfaces;
using Acp.Messages;
using Acp.Types;

namespace Acp.Protocol;

/// <summary>
/// Delegate for custom client-side method handlers. Enables extension without editing the built-in switch.
/// </summary>
public delegate Task<object?> ClientMethodHandler(IClient client, JsonElement? parameters, CancellationToken cancellationToken);

/// <summary>
/// Dispatches JSON-RPC client-side methods to an <see cref="IClient"/>.
/// Supports extensibility via <see cref="Register"/> so new methods can be added without modifying core code.
/// </summary>
public class ClientRequestDispatcher
{
    private readonly IClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, ClientMethodHandler> _customHandlers = new(StringComparer.Ordinal);

    public ClientRequestDispatcher(IClient client, JsonSerializerOptions? jsonOptions = null)
    {
        _client = client;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Register a custom handler for a method name. Overrides built-in behavior for that method.
    /// </summary>
    public void Register(string method, ClientMethodHandler handler)
    {
        _customHandlers[method] = handler;
    }

    /// <summary>
    /// Dispatch a single request by method name and optional params. Returns the result to be sent as JSON-RPC result (or null for notifications).
    /// </summary>
    public async Task<object?> DispatchAsync(string method, JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (_customHandlers.TryGetValue(method, out var custom))
            return await custom(_client, parameters, cancellationToken);

        object? result = null;

        switch (method)
        {
            case "session/update":
                var updateReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<SessionUpdateNotification>(parameters!.Value, _jsonOptions)
                    : new SessionUpdateNotification();
                await _client.SessionUpdateAsync(
                    updateReq?.SessionId ?? "",
                    updateReq?.Update ?? new AgentMessageChunk(),
                    cancellationToken);
                result = new { };
                break;

            case "fs/read_text_file":
                var readReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<ReadTextFileRequest>(parameters!.Value, _jsonOptions)
                    : new ReadTextFileRequest();
                result = await _client.ReadTextFileAsync(
                    readReq?.Path ?? "",
                    readReq?.SessionId ?? "",
                    readReq?.Limit,
                    readReq?.Line,
                    cancellationToken);
                break;

            case "fs/write_text_file":
                var writeReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<WriteTextFileRequest>(parameters!.Value, _jsonOptions)
                    : new WriteTextFileRequest();
                result = await _client.WriteTextFileAsync(
                    writeReq?.Content ?? "",
                    writeReq?.Path ?? "",
                    writeReq?.SessionId ?? "",
                    cancellationToken);
                break;

            case "session/request_permission":
                var permReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<RequestPermissionRequest>(parameters!.Value, _jsonOptions)
                    : new RequestPermissionRequest();
                result = await _client.RequestPermissionAsync(
                    permReq?.Options ?? new List<PermissionOption>(),
                    permReq?.SessionId ?? "",
                    permReq?.ToolCall ?? new ToolCallUpdate(),
                    cancellationToken);
                break;

            case "terminal/create":
                var createTermReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<CreateTerminalRequest>(parameters!.Value, _jsonOptions)
                    : new CreateTerminalRequest();
                result = await _client.CreateTerminalAsync(
                    createTermReq?.Command ?? "",
                    createTermReq?.SessionId ?? "",
                    createTermReq?.Args,
                    createTermReq?.Cwd,
                    createTermReq?.Env,
                    createTermReq?.OutputByteLimit,
                    cancellationToken);
                break;

            case "terminal/output":
                var termOutputReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<TerminalOutputRequest>(parameters!.Value, _jsonOptions)
                    : new TerminalOutputRequest();
                result = await _client.TerminalOutputAsync(
                    termOutputReq?.SessionId ?? "",
                    termOutputReq?.TerminalId ?? "",
                    cancellationToken);
                break;

            case "terminal/release":
                var releaseTermReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<ReleaseTerminalRequest>(parameters!.Value, _jsonOptions)
                    : new ReleaseTerminalRequest();
                result = await _client.ReleaseTerminalAsync(
                    releaseTermReq?.SessionId ?? "",
                    releaseTermReq?.TerminalId ?? "",
                    cancellationToken);
                break;

            case "terminal/wait":
                var waitTermReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<WaitForTerminalExitRequest>(parameters!.Value, _jsonOptions)
                    : new WaitForTerminalExitRequest();
                result = await _client.WaitForTerminalExitAsync(
                    waitTermReq?.SessionId ?? "",
                    waitTermReq?.TerminalId ?? "",
                    cancellationToken);
                break;

            case "terminal/kill":
                var killTermReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<KillTerminalCommandRequest>(parameters!.Value, _jsonOptions)
                    : new KillTerminalCommandRequest();
                result = await _client.KillTerminalAsync(
                    killTermReq?.SessionId ?? "",
                    killTermReq?.TerminalId ?? "",
                    cancellationToken);
                break;

            default:
                var extParams = parameters.HasValue
                    ? JsonSerializer.Deserialize<Dictionary<string, object?>>(parameters!.Value, _jsonOptions)
                    : new Dictionary<string, object?>();
                result = await _client.ExtMethodAsync(method, extParams ?? new Dictionary<string, object?>(), cancellationToken);
                break;
        }

        return result;
    }
}
