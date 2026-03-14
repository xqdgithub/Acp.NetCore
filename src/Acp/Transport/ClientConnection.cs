using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Acp.Interfaces;
using Acp.Protocol;

namespace Acp.Transport;

/// <summary>
/// Client-side connection: stdio transport + client protocol handler.
/// </summary>
public class ClientConnection : Connection
{
    public ClientConnection(IClient client, TextReader input, TextWriter output)
        : base(input, output, new ClientProtocolHandler(client))
    {
    }

    /// <summary>
    /// Register a custom handler for a client method. Enables extension without modifying core code.
    /// </summary>
    public void RegisterHandler(string method, ClientMethodHandler handler)
        => ((ClientProtocolHandler)Handler).RegisterHandler(method, handler);

    /// <summary>
    /// Send a JSON-RPC request and wait for typed response. Must be used together with ListenAsync running (e.g. in background) so that responses are dispatched by the single read loop via the client protocol handler.
    /// </summary>
    public new Task<T> SendRequestAsync<T>(string method, object? parameters, CancellationToken cancellationToken = default)
        => base.SendRequestAsync<T>(method, parameters, cancellationToken);

    /// <summary>
    /// Send a JSON-RPC notification (no response expected). Used for e.g. session/cancel.
    /// </summary>
    public new Task SendNotificationAsync(string method, object? parameters, CancellationToken cancellationToken = default)
        => base.SendNotificationAsync(method, parameters, cancellationToken);
}
