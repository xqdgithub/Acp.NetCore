using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;

namespace Acp.Interfaces;

/// <summary>
/// 客户端文件系统操作接口，定义读写文本文件的能力。
/// Agent 通过此接口访问客户端的文件系统。
/// </summary>
public interface IFileSystemClient
{
    /// <summary>
    /// 读取文本文件内容
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="limit">可选的读取字数限制</param>
    /// <param name="line">可选的指定行号（从 1 开始）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件内容响应</returns>
    Task<ReadTextFileResponse> ReadTextFileAsync(
        string path,
        string sessionId,
        int? limit = null,
        int? line = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 写入文本文件
    /// </summary>
    /// <param name="content">要写入的内容</param>
    /// <param name="path">文件路径</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入结果，如果操作被拒绝则返回 null</returns>
    Task<WriteTextFileResponse?> WriteTextFileAsync(
        string content,
        string path,
        string sessionId,
        CancellationToken cancellationToken = default);
}