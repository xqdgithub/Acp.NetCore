using System.Collections.Generic;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Client-side interface for calling the Agent (initialize, session/new, session/prompt, etc.).
/// Implemented by <see cref="Acp.Transport.SubprocessClient"/>; allows substitution with other transports (e.g. WebSocket).
/// </summary>
public interface IAgentSessionClient
{
    /// <summary>Negotiate protocol version and capabilities. ACP: initialize</summary>
    Task<InitializeResponse> InitializeAsync(
        int protocolVersion,
        ClientCapabilities? clientCapabilities = null,
        Implementation? clientInfo = null,
        CancellationToken cancellationToken = default);

    /// <summary>Authenticate with the Agent if required. ACP: authenticate (optional)</summary>
    Task<AuthenticateResponse> AuthenticateAsync(string methodId, CancellationToken cancellationToken = default);

    /// <summary>Create a new session. ACP: session/new</summary>
    Task<NewSessionResponse> SessionNewAsync(string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default);

    /// <summary>Load an existing session (requires loadSession capability). ACP: session/load</summary>
    Task<LoadSessionResponse?> SessionLoadAsync(string sessionId, string cwd, List<McpServerConfig>? mcpServers = null, CancellationToken cancellationToken = default);

    /// <summary>Send user prompt to the Agent. ACP: session/prompt</summary>
    Task<PromptResponse> SessionPromptAsync(string sessionId, IEnumerable<ContentBlock> prompt, CancellationToken cancellationToken = default);

    /// <summary>Cancel ongoing operations (notification, no response). ACP: session/cancel</summary>
    Task SessionCancelAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Set current session mode (optional). ACP: session/set_mode</summary>
    Task<SetSessionModeResponse> SessionSetModeAsync(string sessionId, string modeId, CancellationToken cancellationToken = default);

    /// <summary>List sessions (optional; requires sessionCapabilities.list). ACP: session/list</summary>
    Task<ListSessionsResponse> SessionListAsync(string? cwd = null, string? cursor = null, CancellationToken cancellationToken = default);
}
