using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Agent 会话管理接口，定义会话的创建、加载、列表、克隆和恢复操作。
/// 实现此接口的 Agent 可以管理多个会话的生命周期。
/// </summary>
public interface ISessionManagement
{
    /// <summary>
    /// 创建新的 Agent 会话
    /// </summary>
    /// <param name="cwd">工作目录</param>
    /// <param name="mcpServers">可选的 MCP 服务器配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新会话的响应信息</returns>
    Task<NewSessionResponse> NewSessionAsync(
        string cwd,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载已存在的会话
    /// </summary>
    /// <param name="cwd">工作目录</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="mcpServers">可选的 MCP 服务器配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>加载的会话响应，如果会话不存在则返回 null</returns>
    Task<LoadSessionResponse?> LoadSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出所有可用会话
    /// </summary>
    /// <param name="cursor">分页游标</param>
    /// <param name="cwd">工作目录过滤</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话列表响应</returns>
    Task<ListSessionsResponse> ListSessionsAsync(
        string? cursor = null,
        string? cwd = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 克隆一个会话
    /// </summary>
    /// <param name="cwd">新会话的工作目录</param>
    /// <param name="sessionId">被克隆的会话 ID</param>
    /// <param name="mcpServers">可选的 MCP 服务器配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新会话的响应信息</returns>
    Task<ForkSessionResponse> ForkSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复一个会话
    /// </summary>
    /// <param name="cwd">工作目录</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="mcpServers">可选的 MCP 服务器配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>恢复的会话响应信息</returns>
    Task<ResumeSessionResponse> ResumeSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default);
}