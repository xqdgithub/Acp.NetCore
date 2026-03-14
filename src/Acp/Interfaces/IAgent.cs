using System.Collections.Generic;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Interface for ACP protocol methods available on the agent side.
/// These methods are called by the client to interact with the agent.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Initialize the agent session with client capabilities.
    /// </summary>
    Task<InitializeResponse> InitializeAsync(
        int protocolVersion,
        ClientCapabilities? clientCapabilities = null,
        Implementation? clientInfo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new agent session.
    /// </summary>
    Task<NewSessionResponse> NewSessionAsync(
        string cwd,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load an existing session.
    /// </summary>
    Task<LoadSessionResponse?> LoadSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all available sessions.
    /// </summary>
    Task<ListSessionsResponse> ListSessionsAsync(
        string? cursor = null,
        string? cwd = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the session mode.
    /// </summary>
    Task<SetSessionModeResponse?> SetSessionModeAsync(
        string modeId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the session model.
    /// </summary>
    Task<SetSessionModelResponse?> SetSessionModelAsync(
        string modelId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a session config option.
    /// </summary>
    Task<SetSessionConfigOptionResponse?> SetConfigOptionAsync(
        string configId,
        string value,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticate with the agent.
    /// </summary>
    Task<AuthenticateResponse?> AuthenticateAsync(
        string methodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a prompt to the agent.
    /// </summary>
    Task<PromptResponse> PromptAsync(
        IEnumerable<ContentBlock> prompt,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fork a session.
    /// </summary>
    Task<ForkSessionResponse> ForkSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume a session.
    /// </summary>
    Task<ResumeSessionResponse> ResumeSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a running prompt.
    /// </summary>
    Task CancelAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle extended method calls.
    /// </summary>
    Task<Dictionary<string, object?>> ExtMethodAsync(
        string method,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle extended notifications.
    /// </summary>
    Task ExtNotificationAsync(
        string method,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when connected to a client.
    /// </summary>
    void OnConnect(IClient client);
}
