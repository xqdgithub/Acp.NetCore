using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Agent 扩展方法接口，定义处理自定义方法和通知的能力。
/// 允许协议扩展而不修改核心接口。
/// </summary>
public interface IAgentExtensions
{
    /// <summary>
    /// 处理扩展方法调用
    /// </summary>
    /// <param name="method">方法名</param>
    /// <param name="parameters">参数字典</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>方法执行结果</returns>
    Task<Dictionary<string, object?>> ExtMethodAsync(
        string method,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理扩展通知
    /// </summary>
    /// <param name="method">方法名</param>
    /// <param name="parameters">参数字典</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ExtNotificationAsync(
        string method,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent 完整接口，组合所有子接口。
/// 实现此接口的 Agent 将具备完整的 ACP 协议能力。
/// </summary>
/// <remarks>
/// 此接口通过组合多个职责单一的子接口来实现完整的 Agent 能力：
/// - <see cref="ISessionManagement"/>: 会话管理
/// - <see cref="IPromptHandler"/>: 提示处理
/// - <see cref="ISessionConfig"/>: 会话配置
/// - <see cref="IAgentLifecycle"/>: 生命周期管理
/// - <see cref="IAgentExtensions"/>: 扩展方法
/// </remarks>
public interface IAgent : ISessionManagement, IPromptHandler, ISessionConfig, IAgentLifecycle, IAgentExtensions
{
}