using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Acp.Interfaces;

namespace Acp.Protocol;

/// <summary>
/// Protocol handler for agent side: parses JSON-RPC, dispatches to <see cref="AgentRequestDispatcher"/>, returns response line.
/// </summary>
public sealed class AgentProtocolHandler : ProtocolHandlerBase<AgentRequestDispatcher>
{
    /// <summary>
    /// 创建 Agent 端协议处理器
    /// </summary>
    /// <param name="agent">Agent 实现</param>
    /// <param name="jsonOptions">可选的 JSON 序列化选项</param>
    public AgentProtocolHandler(IAgent agent, JsonSerializerOptions? jsonOptions = null)
        : base(new AgentRequestDispatcher(agent, jsonOptions ?? CreateDefaultJsonOptions()), jsonOptions)
    {
    }

    /// <summary>
    /// 获取分发器以注册自定义方法处理器
    /// </summary>
    public AgentRequestDispatcher RequestDispatcher => Dispatcher;

    /// <inheritdoc />
    protected override Task<object?> DispatchCoreAsync(
        string method, 
        JsonElement? parameters, 
        CancellationToken cancellationToken)
        => Dispatcher.DispatchAsync(method, parameters, cancellationToken);
}