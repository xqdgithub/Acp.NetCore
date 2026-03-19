using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Agent 提示处理接口，定义接收用户提示和取消操作的能力。
/// 这是 Agent 最核心的能力，用于处理用户输入并生成响应。
/// </summary>
public interface IPromptHandler
{
    /// <summary>
    /// 向 Agent 发送提示（用户输入）
    /// </summary>
    /// <param name="prompt">内容块列表，包含用户的输入</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent 的响应</returns>
    Task<PromptResponse> PromptAsync(
        IEnumerable<ContentBlock> prompt,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消当前正在进行的操作
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CancelAsync(string sessionId, CancellationToken cancellationToken = default);
}