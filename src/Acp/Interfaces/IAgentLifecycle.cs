using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Agent 生命周期接口，定义初始化、认证和连接回调的能力。
/// 这是 Agent 启动和建立连接时必须实现的接口。
/// </summary>
public interface IAgentLifecycle
{
    /// <summary>
    /// 初始化 Agent 会话，协商协议版本和能力
    /// </summary>
    /// <param name="protocolVersion">协议版本</param>
    /// <param name="clientCapabilities">客户端能力</param>
    /// <param name="clientInfo">客户端实现信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化响应，包含 Agent 能力信息</returns>
    Task<InitializeResponse> InitializeAsync(
        int protocolVersion,
        ClientCapabilities? clientCapabilities = null,
        Implementation? clientInfo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 认证 Agent
    /// </summary>
    /// <param name="methodId">认证方法 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证响应，如果不需要认证则返回 null</returns>
    Task<AuthenticateResponse?> AuthenticateAsync(
        string methodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 当连接到客户端时调用
    /// </summary>
    /// <param name="client">客户端实例</param>
    void OnConnect(IClient client);
}