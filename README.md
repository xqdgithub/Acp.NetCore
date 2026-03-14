# dotnet-acp

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**.NET 实现的 ACP（Agent Communication Protocol）客户端库**，用于与支持 ACP 的 Agent 进行会话、提示与工具调用等通信。

## 特性

- **协议实现**：ACP 1.0 协议（会话初始化、prompt、会话管理、MCP 等）
- **传输方式**：子进程（Subprocess）等方式连接 Agent
- **客户端接口**：实现 `IClient`，支持权限请求、会话更新、文件读写、终端创建与输出等
- **Agent 接口**：实现 `IAgent`，可编写自定义 Agent 或对接现有 ACP Agent
- **类型与消息**：完整的请求/响应与会话更新类型（含 JSON 序列化）

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

// 使用子进程方式连接 Agent
var options = new SubprocessClientOptions
{
    Command = "path/to/your-acp-agent",
    Arguments = new[] { "--stdio" }
};

await using var client = new SubprocessClient(options);
await client.StartAsync();

// 调用 Agent 会话 API（如 initialize、session/new、session/prompt 等）
// 详见 IAgentSessionClient 与 examples/Acp.ConsoleTest
```

更多示例见 [examples/Acp.ConsoleTest](examples/Acp.ConsoleTest)。

## 项目结构

```
dotnet-acp/
├── src/Acp/           # 主库 Acp.NetCore
│   ├── Core/          # 协议版本等
│   ├── Interfaces/    # IClient, IAgent, IAgentSessionClient
│   ├── Messages/      # 请求/响应与会话状态
│   ├── Protocol/      # 协议处理与分发
│   ├── Transport/     # 连接与 SubprocessClient
│   ├── Types/         # 能力、内容块、会话更新等类型
│   ├── Helpers/       # 构建器与工具
│   └── Exceptions/    # ACP 异常类型
├── examples/
│   └── Acp.ConsoleTest/   # 控制台示例
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
```

## 许可证

本项目采用 [MIT](LICENSE) 许可证。
