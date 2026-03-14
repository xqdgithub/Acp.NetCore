namespace Acp.Protocol;

/// <summary>
/// Processes one JSON-RPC message (request or notification) and returns the response line to send, if any.
/// Enables protocol layer to be independent of transport (stdio, WebSocket, etc.).
/// </summary>
public interface IProtocolHandler
{
    /// <summary>
    /// Process an incoming message line. Returns the response line to send (for requests with id), or null for notifications or when no response is needed.
    /// </summary>
    Task<string?> ProcessMessageAsync(string requestLine, CancellationToken cancellationToken = default);
}
