using System.Collections.Generic;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Interface for ACP protocol methods available on the client side.
/// These methods are called by the agent to interact with the client.
/// </summary>
public interface IClient
{
    /// <summary>
    /// Request permission from the user for a tool call.
    /// </summary>
    Task<RequestPermissionResponse> RequestPermissionAsync(
        IEnumerable<PermissionOption> options,
        string sessionId,
        ToolCallUpdate toolCall,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a session update notification to the client.
    /// </summary>
    Task SessionUpdateAsync(
        string sessionId,
        SessionUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Write text content to a file.
    /// </summary>
    Task<WriteTextFileResponse?> WriteTextFileAsync(
        string content,
        string path,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read text content from a file.
    /// </summary>
    Task<ReadTextFileResponse> ReadTextFileAsync(
        string path,
        string sessionId,
        int? limit = null,
        int? line = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a terminal.
    /// </summary>
    Task<CreateTerminalResponse> CreateTerminalAsync(
        string command,
        string sessionId,
        List<string>? args = null,
        string? cwd = null,
        List<EnvVariable>? env = null,
        int? outputByteLimit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get terminal output.
    /// </summary>
    Task<TerminalOutputResponse> TerminalOutputAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Release a terminal.
    /// </summary>
    Task<ReleaseTerminalResponse?> ReleaseTerminalAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wait for terminal to exit.
    /// </summary>
    Task<WaitForTerminalExitResponse> WaitForTerminalExitAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kill a terminal process.
    /// </summary>
    Task<KillTerminalCommandResponse?> KillTerminalAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);

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
    /// Called when connected to an agent.
    /// </summary>
    void OnConnect(IAgent agent);
}
