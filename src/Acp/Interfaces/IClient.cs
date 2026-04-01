using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// 客户端会话更新接口，定义接收会话更新通知的能力。
/// </summary>
public interface ISessionUpdateReceiver
{
    /// <summary>
    /// 接收会话更新通知
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="update">会话更新内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SessionUpdateAsync(
        string sessionId,
        SessionUpdate update,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 客户端扩展方法接口，定义处理自定义方法和通知的能力。
/// </summary>
public interface IClientExtensions
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
/// 客户端连接回调接口
/// </summary>
public interface IClientConnection
{
    /// <summary>
    /// 当连接到 Agent 时调用
    /// </summary>
    /// <param name="agent">Agent 实例</param>
    void OnConnect(IAgent agent);
}

/// <summary>
/// 客户端完整接口，组合所有子接口。
/// 实现此接口的客户端将具备完整的 ACP 协议能力。
/// </summary>
/// <remarks>
/// 此接口通过组合多个职责单一的子接口来实现完整的客户端能力：
/// - <see cref="IFileSystemClient"/>: 文件系统操作
/// - <see cref="ITerminalClient"/>: 终端管理
/// - <see cref="IPermissionClient"/>: 权限请求
/// - <see cref="ISessionUpdateReceiver"/>: 会话更新接收
/// - <see cref="IClientExtensions"/>: 扩展方法
/// - <see cref="IClientConnection"/>: 连接回调
/// </remarks>
public interface IClient : IFileSystemClient, ITerminalClient, IPermissionClient, ISessionUpdateReceiver, IClientExtensions, IClientConnection
{
}