using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// 客户端权限请求接口，定义请求用户权限的能力。
/// Agent 通过此接口向用户请求执行敏感操作的许可。
/// </summary>
public interface IPermissionClient
{
    /// <summary>
    /// 请求用户授权
    /// </summary>
    /// <param name="options">可选的权限选项列表</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="toolCall">工具调用信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户的授权决定</returns>
    Task<RequestPermissionResponse> RequestPermissionAsync(
        IEnumerable<PermissionOption> options,
        string sessionId,
        ToolCallUpdate toolCall,
        CancellationToken cancellationToken = default);
}