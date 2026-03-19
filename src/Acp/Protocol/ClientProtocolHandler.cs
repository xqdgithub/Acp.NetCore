using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Acp.Interfaces;

namespace Acp.Protocol;

/// <summary>
/// Protocol handler for client side: parses JSON-RPC, dispatches to <see cref="ClientRequestDispatcher"/>, returns response line.
/// </summary>
public sealed class ClientProtocolHandler : ProtocolHandlerBase<ClientRequestDispatcher>
{
    /// <summary>
    /// 创建客户端协议处理器
    /// </summary>
    /// <param name="client">客户端实现</param>
    /// <param name="jsonOptions">可选的 JSON 序列化选项</param>
    public ClientProtocolHandler(IClient client, JsonSerializerOptions? jsonOptions = null)
        : base(new ClientRequestDispatcher(client, jsonOptions ?? CreateDefaultJsonOptions()), jsonOptions)
    {
    }

    /// <summary>
    /// 注册自定义方法处理器
    /// </summary>
    /// <param name="method">方法名</param>
    /// <param name="handler">处理器委托</param>
    public void RegisterHandler(string method, ClientMethodHandler handler)
        => Dispatcher.Register(method, handler);

    /// <inheritdoc />
    protected override Task<object?> DispatchCoreAsync(
        string method, 
        JsonElement? parameters, 
        CancellationToken cancellationToken)
        => Dispatcher.DispatchAsync(method, parameters, cancellationToken);
}