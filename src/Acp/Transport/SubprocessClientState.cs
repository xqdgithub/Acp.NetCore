namespace Acp.Transport;

/// <summary>
/// SubprocessClient 生命周期状态
/// </summary>
public enum SubprocessClientState
{
    /// <summary>已创建，未启动</summary>
    Created = 0,
    
    /// <summary>正在启动进程</summary>
    Starting = 1,
    
    /// <summary>进程运行中</summary>
    Running = 2,
    
    /// <summary>正在停止进程</summary>
    Stopping = 3,
    
    /// <summary>已停止（可重新启动）</summary>
    Stopped = 4,
    
    /// <summary>已释放（不可重用）</summary>
    Disposed = 5
}