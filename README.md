# dotnet-acp

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**.NET 实现的 ACP（Agent Communication Protocol）客户端库**，用于与支持 ACP 的 Agent 进行会话、提示与工具调用等通信。

## 特性

- **协议实现**：ACP 1.0 协议（会话初始化、prompt、会话管理、MCP 等）
- **传输方式**：子进程（Subprocess）等方式连接 Agent，支持自定义超时
- **接口隔离设计**：
  - `IAgent` 拆分为 `ISessionManagement`、`IPromptHandler`、`ISessionConfig`、`IAgentLifecycle`、`IAgentExtensions`
  - `IClient` 拆分为 `IFileSystemClient`、`ITerminalClient`、`IPermissionClient` 等
- **类型安全**：完整的请求/响应与会话更新类型，支持 JSON 多态序列化
- **资源管理**：支持 `IAsyncDisposable` 异步资源释放，避免同步上下文死锁
- **可观测性**：
  - 完整的事件系统（进程启动/退出、Stderr、连接错误、状态变更）
  - 集成 `ILogger` 支持结构化日志
  - 线程安全的状态机管理
- **健壮性**：启动超时、优雅关闭、异常安全、自动 SessionId 管理

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download) 或更高

## 快速开始

### 安装

将项目引用添加到你的 `.csproj`：

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Acp.NetCore.csproj" />
</ItemGroup>
```

或从源码克隆后直接引用 `src/Acp/Acp.NetCore.csproj`。

### 使用示例

```csharp
using Acp.Transport;
using Acp.Interfaces;

// 创建客户端
var client = new SubprocessClient("node", new[] { "agent.js" });

// 订阅事件（可选但推荐）
client.ProcessExited += (s, e) => 
    Console.WriteLine($"进程退出: {e.ExitCode}, 正常退出: {e.IsNormalExit}");
client.StderrReceived += (s, e) => 
    Console.WriteLine($"[stderr] {e.Line}");
client.StateChanged += (s, e) => 
    Console.WriteLine($"状态: {e.OldState} -> {e.NewState}");

// 启动进程（带超时）
await client.StartAsync(startTimeout: TimeSpan.FromSeconds(30));

// 初始化连接
var initResponse = await client.InitializeAsync(protocolVersion: 1);
Console.WriteLine($"连接到: {initResponse.AgentInfo.Name}");

// 创建会话（SessionId 自动管理）
var sessionResponse = await client.SessionNewAsync(cwd: "/path/to/work");
Console.WriteLine($"会话 ID: {client.CurrentSessionId}"); // 自动设置

// 发送提示
var promptResponse = await client.SessionPromptAsync(
    client.CurrentSessionId!,
    new[] { new TextContentBlock("Hello, Agent!") }
);

// 优雅关闭
await client.StopAsync(TimeSpan.FromSeconds(5));
```

### 使用 ILogger

```csharp
using Microsoft.Extensions.Logging;

var options = new SubprocessClientOptions
{
    Logger = loggerFactory.CreateLogger<SubprocessClient>(),
    StderrLogLevel = LogLevel.Warning,
    DefaultStartTimeout = TimeSpan.FromSeconds(30),
    DefaultStopTimeout = TimeSpan.FromSeconds(5)
};

var client = new SubprocessClient("node", new[] { "agent.js" }, options);
```

更多示例见 [examples/Acp.ConsoleTest](examples/Acp.ConsoleTest)。

## 架构设计

### 分层架构

```
┌─────────────────────────────────────────────────────────────┐
│                      应用层 (Application)                    │
│                  SubprocessClient / AcpCore                  │
├─────────────────────────────────────────────────────────────┤
│                      传输层 (Transport)                      │
│   Connection → ClientConnection / AgentConnection            │
├─────────────────────────────────────────────────────────────┤
│                      协议层 (Protocol)                       │
│   IProtocolHandler → ProtocolHandlerBase → Dispatcher       │
├─────────────────────────────────────────────────────────────┤
│                      接口层 (Interfaces)                     │
│   IClient / IAgent 及其子接口                                │
├─────────────────────────────────────────────────────────────┤
│                      领域层 (Domain)                         │
│   Messages (DTO) / Types (领域类型) / Helpers / Exceptions   │
└─────────────────────────────────────────────────────────────┘
```

### SubprocessClient 状态机

```
                    ┌─────────────┐
                    │   Created   │
                    └──────┬──────┘
                           │ StartAsync()
                           ▼
                    ┌─────────────┐
           ┌───────►│   Starting  │◄───────┐
           │        └──────┬──────┘        │
           │    成功       │               │ 失败
           │               ▼               │
           │        ┌─────────────┐        │
           │        │   Running   │        │
           │        └──────┬──────┘        │
           │               │               │
           │               │ StopAsync()   │
           │               │ 或进程退出     │
           │               ▼               │
           │        ┌─────────────┐        │
           │        │   Stopping  │────────┘
           │        └──────┬──────┘
           │               │
           │               ▼
           │        ┌─────────────┐
           └────────│   Stopped   │
                    └─────────────┘
                           │
                           │ Dispose()
                           ▼
                    ┌─────────────┐
                    │  Disposed   │
                    └─────────────┘
```

### 接口设计

#### Agent 端接口

```csharp
// 完整的 Agent 接口，组合所有子接口
public interface IAgent : 
    ISessionManagement,    // 会话管理：NewSession, LoadSession, ForkSession 等
    IPromptHandler,        // 提示处理：PromptAsync, CancelAsync
    ISessionConfig,        // 会话配置：SetMode, SetModel, SetConfigOption
    IAgentLifecycle,       // 生命周期：Initialize, Authenticate, OnConnect
    IAgentExtensions       // 扩展方法：ExtMethod, ExtNotification
{
}
```

#### Client 端接口

```csharp
// 完整的 Client 接口，组合所有子接口
public interface IClient :
    IFileSystemClient,     // 文件操作：ReadTextFile, WriteTextFile
    ITerminalClient,       // 终端管理：CreateTerminal, TerminalOutput 等
    IPermissionClient,     // 权限请求：RequestPermission
    ISessionUpdateReceiver,// 会话更新：SessionUpdate
    IClientExtensions,     // 扩展方法
    IClientConnection      // 连接回调
{
}
```

### 关键设计

- **状态机管理**：`SubprocessClient` 使用线程安全的状态机，防止重复启动和竞态条件
- **事件驱动**：5 个事件提供完整生命周期可观测性
- **协议处理基类**：`ProtocolHandlerBase<TDispatcher>` 消除 Client/Agent 协议处理器的代码重复
- **请求超时机制**：`Connection.SendRequestAsync` 支持可配置超时，防止永久挂起
- **异步资源释放**：`SubprocessClient` 实现 `IAsyncDisposable`，推荐使用 `await using`
- **异常安全启动**：`StartAsync` 在进程启动失败时正确清理资源
- **ILogger 集成**：结构化日志输出，支持 Serilog、NLog 等

### SubprocessClient 事件

| 事件 | 说明 | 事件参数 |
|------|------|----------|
| `ProcessStarted` | 进程启动完成 | `ProcessId`, `Timestamp` |
| `ProcessExited` | 进程退出 | `ExitCode`, `IsNormalExit`, `Timestamp` |
| `StderrReceived` | Stderr 输出 | `Line`, `ProcessId`, `Timestamp` |
| `ConnectionError` | 连接错误 | `Error`, `IsFatal`, `Timestamp` |
| `StateChanged` | 状态变更 | `OldState`, `NewState`, `Reason`, `Timestamp` |

## 项目结构

```
dotnet-acp/
├── src/Acp/                    # 主库 Acp.NetCore
│   ├── Core/                   # 协议版本常量
│   ├── Interfaces/             # 公共接口定义
│   │   ├── IAgent.cs           # Agent 组合接口
│   │   ├── IClient.cs          # Client 组合接口
│   │   ├── ISessionManagement.cs
│   │   ├── IPromptHandler.cs
│   │   ├── ISessionConfig.cs
│   │   ├── IAgentLifecycle.cs
│   │   ├── IFileSystemClient.cs
│   │   ├── ITerminalClient.cs
│   │   └── IPermissionClient.cs
│   ├── Messages/               # 请求/响应 DTO
│   ├── Protocol/               # 协议处理与分发
│   │   ├── IProtocolHandler.cs
│   │   ├── ProtocolHandlerBase.cs
│   │   ├── ClientProtocolHandler.cs
│   │   ├── AgentProtocolHandler.cs
│   │   └── *RequestDispatcher.cs
│   ├── Transport/              # 传输层实现
│   │   ├── Connection.cs       # 基础连接类（超时机制）
│   │   ├── ClientConnection.cs
│   │   ├── AgentConnection.cs
│   │   ├── SubprocessClient.cs       # 子进程客户端
│   │   ├── SubprocessClientState.cs  # 状态枚举
│   │   ├── SubprocessClientEventArgs.cs # 事件参数
│   │   └── SubprocessClientOptions.cs   # 配置选项
│   ├── Types/                  # 领域类型
│   ├── Helpers/                # 构建器辅助
│   └── Exceptions/             # 异常层次
├── examples/
│   └── Acp.ConsoleTest/        # 控制台示例
├── docs/
│   └── SubprocessClient-Refactoring-Plan.md  # 重构设计文档
├── Acp.sln
├── README.md
└── LICENSE
```

## 构建与测试

```bash
# 还原依赖并构建
dotnet restore
dotnet build

# 运行示例
dotnet run --project examples/Acp.ConsoleTest/Acp.ConsoleTest.csproj

# 测试模式（本地 Echo Agent）
dotnet run --project examples/Acp.ConsoleTest/Acp.ConsoleTest.csproj -- --test
```

## 扩展开发

### 实现自定义 Agent

只需实现所需的子接口：

```csharp
// 最小化实现：只处理提示
public class MySimpleAgent : IPromptHandler
{
    public Task<PromptResponse> PromptAsync(
        IEnumerable<ContentBlock> prompt, 
        string sessionId,
        CancellationToken ct = default)
    {
        // 处理用户输入
        return Task.FromResult(new PromptResponse
        {
            Content = new List<ContentBlock> 
            { 
                new TextContentBlock("Response from MySimpleAgent") 
            }
        });
    }

    public Task CancelAsync(string sessionId, CancellationToken ct = default)
        => Task.CompletedTask;
}

// 完整实现：继承 Agent 基类
public class MyFullAgent : Agent
{
    public override Task<PromptResponse> PromptAsync(...)
    {
        // 自定义实现
    }
}
```

### 注册自定义方法处理器

```csharp
var connection = new ClientConnection(myClient, input, output);
connection.RegisterHandler("custom/method", async (client, parameters, ct) =>
{
    // 处理自定义方法
    return new { result = "ok" };
});
```

### 自定义 SubprocessClient 行为

```csharp
public class MySubprocessClient : SubprocessClient
{
    public MySubprocessClient(string command, string[] args, SubprocessClientOptions? options = null)
        : base(command, args, options)
    {
    }

    protected override void OnProcessExited(ProcessExitedEventArgs e)
    {
        // 自定义进程退出处理
        if (!e.IsNormalExit)
        {
            // 处理异常退出
        }
        base.OnProcessExited(e);
    }

    protected override void OnConnectionError(ConnectionErrorEventArgs e)
    {
        // 自定义连接错误处理
        base.OnConnectionError(e);
    }
}
```

## API 参考

### SubprocessClientOptions

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Logger` | `ILogger?` | `null` | 日志记录器 |
| `StderrLogLevel` | `LogLevel` | `Warning` | Stderr 日志级别 |
| `StartInfo` | `ProcessStartInfo?` | `null` | 进程启动信息基础配置 |
| `DefaultStartTimeout` | `TimeSpan` | 30s | 默认启动超时 |
| `DefaultStopTimeout` | `TimeSpan` | 3s | 默认停止超时 |
| `EnableExitEvent` | `bool` | `true` | 是否启用进程退出事件 |
| `AutoManageSessionId` | `bool` | `true` | 是否自动管理 SessionId |

### SubprocessClient 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `State` | `SubprocessClientState` | 当前状态 |
| `CurrentSessionId` | `string?` | 当前会话 ID（自动管理） |
| `HasExited` | `bool` | 进程是否已退出 |
| `ExitCode` | `int?` | 进程退出码 |

### SubprocessClient 方法

| 方法 | 说明 |
|------|------|
| `StartAsync(cancellationToken, startTimeout)` | 启动进程 |
| `StopAsync(gracefulTimeout, cancellationToken)` | 停止进程 |
| `WaitForExitAsync(timeout, cancellationToken)` | 等待进程退出 |
| `InitializeAsync(...)` | 初始化连接 |
| `SessionNewAsync(...)` | 创建新会话 |
| `SessionLoadAsync(...)` | 加载现有会话 |
| `SessionPromptAsync(...)` | 发送提示 |

## 许可证

本项目采用 [MIT](LICENSE) 许可证。