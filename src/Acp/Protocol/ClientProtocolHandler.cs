using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Acp.Interfaces;

namespace Acp.Protocol;

/// <summary>
/// Protocol handler for client side: parses JSON-RPC, dispatches to <see cref="ClientRequestDispatcher"/>, returns response line.
/// </summary>
public class ClientProtocolHandler : IProtocolHandler
{
    private readonly ClientRequestDispatcher _dispatcher;
    private readonly JsonSerializerOptions _jsonOptions;

    public ClientProtocolHandler(IClient client, JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        _dispatcher = new ClientRequestDispatcher(client, _jsonOptions);
    }

    /// <summary>
    /// Register a custom handler for a client method.
    /// </summary>
    public void RegisterHandler(string method, ClientMethodHandler handler) => _dispatcher.Register(method, handler);

    /// <inheritdoc />
    public async Task<string?> ProcessMessageAsync(string requestLine, CancellationToken cancellationToken = default)
    {
        object? id = null;
        bool isNotification = false;
        try
        {
            using var doc = JsonDocument.Parse(requestLine);
            var root = doc.RootElement;

            if (!root.TryGetProperty("method", out var methodEl))
                return BuildErrorResponse(null, -32600, "Invalid Request: missing method");

            var method = methodEl.GetString() ?? "";
            var hasId = root.TryGetProperty("id", out var idElement);
            if (hasId)
                id = idElement.Clone();
            isNotification = !hasId;

            JsonElement? parameters = null;
            if (root.TryGetProperty("params", out var paramsElement))
                parameters = paramsElement;

            var result = await _dispatcher.DispatchAsync(method, parameters, cancellationToken);

            if (isNotification)
                return null;

            var response = new { jsonrpc = "2.0", id, result };
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (JsonException ex)
        {
            if (isNotification) return null;
            return BuildErrorResponse(id, -32700, "Parse error: " + ex.Message);
        }
        catch (System.Exception ex)
        {
            if (isNotification) return null;
            return BuildErrorResponse(id, -32603, ex.Message);
        }
    }

    private static string BuildErrorResponse(object? id, int code, string message)
    {
        var error = new { jsonrpc = "2.0", id, error = new { code, message } };
        return JsonSerializer.Serialize(error);
    }
}
