using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Acp.Interfaces;
using Acp.Messages;
using Acp.Types;

namespace Acp.Protocol;

/// <summary>
/// Delegate for custom agent-side method handlers. Enables extension without editing the built-in switch.
/// </summary>
public delegate Task<object?> AgentMethodHandler(IAgent agent, JsonElement? parameters, CancellationToken cancellationToken);

/// <summary>
/// Dispatches JSON-RPC agent-side methods to an <see cref="IAgent"/>.
/// Supports extensibility via <see cref="Register"/> so new methods can be added without modifying core code.
/// </summary>
public class AgentRequestDispatcher
{
    private readonly IAgent _agent;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, AgentMethodHandler> _customHandlers = new(StringComparer.Ordinal);

    public AgentRequestDispatcher(IAgent agent, JsonSerializerOptions? jsonOptions = null)
    {
        _agent = agent;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Register a custom handler for a method name. Overrides built-in behavior for that method.
    /// </summary>
    public void Register(string method, AgentMethodHandler handler)
    {
        _customHandlers[method] = handler;
    }

    /// <summary>
    /// Dispatch a single request by method name and optional params. Returns the result to be sent as JSON-RPC result.
    /// </summary>
    public async Task<object?> DispatchAsync(string method, JsonElement? parameters, CancellationToken cancellationToken)
    {
        if (_customHandlers.TryGetValue(method, out var custom))
            return await custom(_agent, parameters, cancellationToken);

        object? result = null;

        switch (method)
        {
            case "initialize":
                var initReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<InitializeRequest>(parameters!.Value, _jsonOptions)
                    : null;
                result = await _agent.InitializeAsync(
                    initReq?.ProtocolVersion ?? 1,
                    initReq?.ClientCapabilities,
                    initReq?.ClientInfo,
                    cancellationToken);
                break;

            case "session/new":
                var newSessionReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<NewSessionRequest>(parameters!.Value, _jsonOptions)
                    : new NewSessionRequest();
                result = await _agent.NewSessionAsync(
                    newSessionReq?.Cwd ?? ".",
                    newSessionReq?.McpServers,
                    cancellationToken);
                break;

            case "session/load":
                var loadSessionReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<LoadSessionRequest>(parameters!.Value, _jsonOptions)
                    : new LoadSessionRequest();
                result = await _agent.LoadSessionAsync(
                    loadSessionReq?.Cwd ?? ".",
                    loadSessionReq?.SessionId ?? "",
                    loadSessionReq?.McpServers,
                    cancellationToken);
                break;

            case "sessions/list":
                var listSessionsReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<ListSessionsRequest>(parameters!.Value, _jsonOptions)
                    : new ListSessionsRequest();
                result = await _agent.ListSessionsAsync(
                    listSessionsReq?.Cursor,
                    listSessionsReq?.Cwd,
                    cancellationToken);
                break;

            case "session/set_mode":
                var setModeReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<SetSessionModeRequest>(parameters!.Value, _jsonOptions)
                    : new SetSessionModeRequest();
                result = await _agent.SetSessionModeAsync(
                    setModeReq?.ModeId ?? "",
                    setModeReq?.SessionId ?? "",
                    cancellationToken);
                break;

            case "session/set_model":
                var setModelReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<SetSessionModelRequest>(parameters!.Value, _jsonOptions)
                    : new SetSessionModelRequest();
                result = await _agent.SetSessionModelAsync(
                    setModelReq?.ModelId ?? "",
                    setModelReq?.SessionId ?? "",
                    cancellationToken);
                break;

            case "session/set_config_option":
                var setConfigReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<SetSessionConfigOptionRequest>(parameters!.Value, _jsonOptions)
                    : new SetSessionConfigOptionRequest();
                result = await _agent.SetConfigOptionAsync(
                    setConfigReq?.ConfigId ?? "",
                    setConfigReq?.Value ?? "",
                    setConfigReq?.SessionId ?? "",
                    cancellationToken);
                break;

            case "authenticate":
                var authReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<AuthenticateRequest>(parameters!.Value, _jsonOptions)
                    : new AuthenticateRequest();
                result = await _agent.AuthenticateAsync(
                    authReq?.MethodId ?? "",
                    cancellationToken);
                break;

            case "session/prompt":
                var promptReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<PromptRequest>(parameters!.Value, _jsonOptions)
                    : new PromptRequest();
                result = await _agent.PromptAsync(
                    promptReq?.Prompt ?? new List<ContentBlock>(),
                    promptReq?.SessionId ?? "",
                    cancellationToken);
                break;

            case "session/fork":
                var forkReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<ForkSessionRequest>(parameters!.Value, _jsonOptions)
                    : new ForkSessionRequest();
                result = await _agent.ForkSessionAsync(
                    forkReq?.Cwd ?? ".",
                    forkReq?.SessionId ?? "",
                    forkReq?.McpServers,
                    cancellationToken);
                break;

            case "session/resume":
                var resumeReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<ResumeSessionRequest>(parameters!.Value, _jsonOptions)
                    : new ResumeSessionRequest();
                result = await _agent.ResumeSessionAsync(
                    resumeReq?.Cwd ?? ".",
                    resumeReq?.SessionId ?? "",
                    resumeReq?.McpServers,
                    cancellationToken);
                break;

            case "session/cancel":
                var cancelReq = parameters.HasValue
                    ? JsonSerializer.Deserialize<CancelNotification>(parameters!.Value, _jsonOptions)
                    : new CancelNotification();
                await _agent.CancelAsync(cancelReq?.SessionId ?? "", cancellationToken);
                result = new { };
                break;

            default:
                var extParams = parameters.HasValue
                    ? JsonSerializer.Deserialize<Dictionary<string, object?>>(parameters!.Value, _jsonOptions)
                    : new Dictionary<string, object?>();
                result = await _agent.ExtMethodAsync(method, extParams ?? new Dictionary<string, object?>(), cancellationToken);
                break;
        }

        return result;
    }
}
