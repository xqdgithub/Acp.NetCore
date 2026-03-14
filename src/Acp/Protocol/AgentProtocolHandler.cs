using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Acp.Interfaces;

namespace Acp.Protocol;

/// <summary>
/// Protocol handler for agent side: parses JSON-RPC, dispatches to <see cref="AgentRequestDispatcher"/>, returns response line.
/// </summary>
public class AgentProtocolHandler : IProtocolHandler
{
    private readonly AgentRequestDispatcher _dispatcher;
    private readonly JsonSerializerOptions _jsonOptions;

    public AgentProtocolHandler(IAgent agent, JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        _dispatcher = new AgentRequestDispatcher(agent, _jsonOptions);
    }

    /// <summary>
    /// Dispatcher for registering custom method handlers.
    /// </summary>
    public AgentRequestDispatcher Dispatcher => _dispatcher;

    /// <inheritdoc />
    public async Task<string?> ProcessMessageAsync(string requestLine, CancellationToken cancellationToken = default)
    {
        object? id = null;
        try
        {
            using var doc = JsonDocument.Parse(requestLine);
            var root = doc.RootElement;

            if (!root.TryGetProperty("method", out var methodEl))
                return BuildErrorResponse(null, -32600, "Invalid Request: missing method");

            var method = methodEl.GetString() ?? "";
            var hasId = root.TryGetProperty("id", out var idElement);
            if (hasId)
                id = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : idElement.GetRawText();

            JsonElement? parameters = null;
            if (root.TryGetProperty("params", out var paramsElement))
                parameters = paramsElement;

            var result = await _dispatcher.DispatchAsync(method, parameters, cancellationToken);

            if (!hasId)
                return null;

            var response = new { jsonrpc = "2.0", id, result };
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return BuildErrorResponse(id, -32700, "Parse error: " + ex.Message);
        }
        catch (System.Exception ex)
        {
            return BuildErrorResponse(id, -32603, ex.Message);
        }
    }

    private static string BuildErrorResponse(object? id, int code, string message)
    {
        var error = new { jsonrpc = "2.0", id, error = new { code, message } };
        return JsonSerializer.Serialize(error);
    }
}
