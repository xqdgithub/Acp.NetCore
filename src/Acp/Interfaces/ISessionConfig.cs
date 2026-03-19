using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;

namespace Acp.Interfaces;

/// <summary>
/// Agent 会话配置接口，定义设置会话模式、模型和配置选项的能力。
/// 允许客户端动态调整 Agent 的行为。
/// </summary>
public interface ISessionConfig
{
    /// <summary>
    /// 设置会话模式
    /// </summary>
    /// <param name="modeId">模式 ID</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设置结果，如果不支持则返回 null</returns>
    Task<SetSessionModeResponse?> SetSessionModeAsync(
        string modeId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置会话使用的模型
    /// </summary>
    /// <param name="modelId">模型 ID</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设置结果，如果不支持则返回 null</returns>
    Task<SetSessionModelResponse?> SetSessionModelAsync(
        string modelId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置会话配置选项
    /// </summary>
    /// <param name="configId">配置项 ID</param>
    /// <param name="value">配置值</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设置结果，如果不支持则返回 null</returns>
    Task<SetSessionConfigOptionResponse?> SetConfigOptionAsync(
        string configId,
        string value,
        string sessionId,
        CancellationToken cancellationToken = default);
}