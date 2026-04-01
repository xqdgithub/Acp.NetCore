using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// 客户端终端管理接口，定义创建、控制和监控终端的能力。
/// Agent 通过此接口在客户端环境中执行命令。
/// </summary>
public interface ITerminalClient
{
    /// <summary>
    /// 创建新终端并执行命令
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="args">可选的命令参数</param>
    /// <param name="cwd">可选的工作目录</param>
    /// <param name="env">可选的环境变量</param>
    /// <param name="outputByteLimit">可选的输出字节限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的终端信息</returns>
    Task<CreateTerminalResponse> CreateTerminalAsync(
        string command,
        string sessionId,
        List<string>? args = null,
        string? cwd = null,
        List<EnvVariable>? env = null,
        int? outputByteLimit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取终端输出
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="terminalId">终端 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>终端输出</returns>
    Task<TerminalOutputResponse> TerminalOutputAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放终端资源
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="terminalId">终端 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>释放结果，如果终端不存在则返回 null</returns>
    Task<ReleaseTerminalResponse?> ReleaseTerminalAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 等待终端退出
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="terminalId">终端 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>终端退出信息，包含退出码</returns>
    Task<WaitForTerminalExitResponse> WaitForTerminalExitAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止终端进程
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="terminalId">终端 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>终止结果，如果终端不存在则返回 null</returns>
    Task<KillTerminalCommandResponse?> KillTerminalAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default);
}