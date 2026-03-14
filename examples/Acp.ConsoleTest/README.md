# ACP .NET Console Test

交互式 ACP (Agent Client Protocol) 控制台客户端

## 快速开始

### 测试模式 (本地 Echo)
```bash
cd examples/Acp.ConsoleTest
dotnet run -- --test
```

### 作为 Agent 运行 (监听 STDIO)
```bash
dotnet run -- --agent
```

### 启动外部 Agent 并连接

#### OpenCode (推荐)
```bash
# 启动 OpenCode 作为 ACP Agent
dotnet run -- --command opencode --args "acp"
```

#### Gemini CLI
```bash
# 需要安装 gemini-cli
dotnet run -- --command gemini --args "--experimental-acp"
```

#### 自定义 Agent
```bash
# 任何支持 ACP 的命令
dotnet run -- --command <your-agent> --args "acp"
```

## 使用方式

```bash
# 基本使用 (默认使用 opencode acp)
dotnet run

# 指定 Agent 命令
dotnet run -- --command opencode --args "acp"

# Gemini CLI
dotnet run -- --command gemini --args "--experimental-acp"
```

## 支持的命令

| 命令 | 说明 |
|------|------|
| `/help` | 显示帮助 |
| `/quit` | 退出 |
| `/new` | 创建新会话 |
| `/sessions` | 列出会话 |
| `/read <path>` | 读取文件 |
| `/write <path> <content>` | 写入文件 |
| `<文本>` | 发送对话 |

## 工作原理

- 本示例使用 **SubprocessConsoleClient**（继承自 Acp 包中的 `Acp.Transport.SubprocessClient`），在控制台提供 REPL 与 ANSI 会话输出。
- 通过 NuGet 或项目引用使用 Acp 时，可引用 **SubprocessClient** 与 **IAgentSessionClient** 作为默认子进程实现，并基于其扩展（如本示例）。

```
┌─────────────────────────────┐
│   .NET Console (Client)     │
│   SubprocessConsoleClient   │
│   (extends SubprocessClient)│
│  ┌─────────────────────┐    │
│  │ Process.Start()     │    │
│  │ Redirect stdin/out  │    │
│  └──────────┬──────────┘    │
└─────────────┼───────────────┘
              │ STDIO (JSON-RPC)
              ▼
┌─────────────────────────────┐
│   opencode acp             │
│   (Agent)                  │
└─────────────────────────────┘
```

## 协议说明

ACP 使用 JSON-RPC 2.0 通过 STDIO 通信：

```json
// 请求
{"jsonrpc": "2.0", "id": "1", "method": "initialize", "params": {...}}

// 响应
{"jsonrpc": "2.0", "id": "1", "result": {...}}
```

## 项目结构

```
dotnet-acp/
├── src/Acp/                  # 核心库
│   ├── Core/                 # 协议版本
│   ├── Types/                # 类型定义
│   ├── Messages/             # 请求/响应
│   ├── Interfaces/           # 接口定义
│   ├── Transport/            # STDIO 传输
│   ├── Exceptions/           # 异常
│   └── Helpers/              # 构建器
└── examples/Acp.ConsoleTest/ # 测试控制台
```

## 参考

- [ACP 官方文档](https://agentclientprotocol.com)
- [OpenCode ACP 文档](https://opencode.ai/docs/acp/)
- [Zed Blog - ACP](https://zed.dev/blog/bring-your-own-agent-to-zed)
- [Python SDK](https://github.com/agentclientprotocol/python-sdk)
