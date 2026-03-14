# ACP 项目架构审查与改进说明

## 一、项目概览

本项目是 ACP (Agent Communication Protocol) 的 .NET 实现，基于 JSON-RPC 2.0，通过 stdio 在 Client 与 Agent 之间通信。

---

## 二、已发现并修复的流程问题

### 2.1 通知方法名不一致（已修复）

- **问题**：协议中 Agent 发给 Client 的通知方法名为 `session/update`，示例 `SubprocessClient` 的 `StartOutputReader` 中错误地判断为 `session_update`（下划线），导致 session 更新通知从未被正确处理。
- **修复**：将判断改为 `session/update`，与 `ClientConnection` 和 `SessionUpdateNotification.Method` 一致。

### 2.2 SessionUpdate JSON 字段名错误（已修复）

- **问题**：`HandleSessionUpdateAsync` 中按 `sessionUpdate` 取更新类型，而协议与 `SessionUpdate` 类型使用字段 `type`（如 `agent_message_chunk`）。
- **修复**：改为从 `update` 对象读取 `type` 字段。

### 2.3 Agent 无法回调 Client（已修复）

- **问题**：在子进程模式下，Agent 通过 stdout 发送请求（如 `fs/read_text_file`）给父进程，父进程仅处理「响应」和「session/update」通知，未把「来自 Agent 的请求」分发给 `IClient` 并写回响应到 Agent 的 stdin，导致 Agent 无法真正调用 Client 能力。
- **修复**：
  - 抽取 `ClientRequestDispatcher`，统一根据 `method` + `params` 调用 `IClient` 并返回结果。
  - `ClientConnection` 改为使用 `ClientRequestDispatcher`，避免重复分发逻辑。
  - `SubprocessClient` 在读取到带 `method` + `id` 的消息时，视为 Agent 发来的请求，用 `ClientRequestDispatcher` 调用自身（`IClient`），将结果写回 `_processInput`（Agent 的 stdin）。

### 2.4 OnConnect 未使用、Agent 侧无 IClient 通道（设计说明）

- **现状**：`AcpCore.RunAgentAsync` 未调用 `agent.OnConnect(client)`，且未向 Agent 注入任何 `IClient`。在子进程模式下，Agent 与 Client 分属不同进程，Agent 只能通过 stdout 发请求、stdin 收响应，由父进程（SubprocessClient）扮演 Client 并回写。
- **建议**：若需「同进程内」测试（Agent 直接调用 IClient），可在后续为 `AgentConnection` 增加「Client 桩」：实现一个通过当前 Connection 的 `_input/_output` 发送请求并等待响应的 `IClient`，在 `ListenAsync` 前注入并调用 `agent.OnConnect(clientStub)`。当前子进程流程已通过父进程的请求处理闭环。

---

## 三、架构改进建议

### 3.1 可扩展的 Handler 注册（已落地）

- **问题**：`AgentConnection` 与 `ClientConnection` 使用巨型 `switch (method)`，新增协议方法需要改多处。
- **改进**：
  - **Client 侧**：`ClientRequestDispatcher` 提供 `Register(string method, ClientMethodHandler handler)`，扩展时只需注册委托，无需改 switch；`ClientConnection.RegisterHandler` 对外暴露注册入口。
  - **Agent 侧**：新增 `AgentRequestDispatcher`，提供 `Register(string method, AgentMethodHandler handler)`；`AgentConnection.Dispatcher` 暴露调度器以便注册。两侧均先查自定义 handler，再走内置方法，最后走 `ExtMethodAsync`。

### 3.2 协议层与传输层分离（已落地）

- **实现**：协议与传输已解耦。
  - **协议层**（`Acp.ProtocolLayer`）：`IProtocolHandler` 定义 `ProcessMessageAsync(requestLine) → responseLine?`；`AgentProtocolHandler` / `ClientProtocolHandler` 负责解析 JSON-RPC、调用对应 Dispatcher、序列化响应或错误。
  - **传输层**（`Acp.Transport`）：`Connection` 仅依赖 `IProtocolHandler`，在 `ListenAsync` 中「读行 → 调用 handler.ProcessMessageAsync → 若有返回值则写行」，不关心 JSON-RPC 结构。`AgentConnection` / `ClientConnection` 仅组装「stdio + 对应 ProtocolHandler」。
- **扩展**：若要支持 WebSocket 或 TCP，只需实现新的「连接」类：持有 `IProtocolHandler` 与自己的读写通道（如 `WebSocket`），循环「读一条消息 → handler.ProcessMessageAsync → 写回」，协议逻辑无需改动。

### 3.3 错误与取消

- **建议**：在 Connection 层统一将异常转换为 JSON-RPC `error` 对象并写回，避免未捕获异常导致对端一直等待。SubprocessClient 的 `HandleAgentRequestAsync` 已对异常做了错误响应回写，可视为参考。
- 在 `ListenAsync` 循环中对 `CancellationToken` 和 `OperationCanceledException` 做明确处理，保证关闭时能正确结束循环并释放资源。

### 3.4 类型与命名统一

- **SessionUpdate**：协议中若统一使用 `type` 表示更新类型，则文档与实现应一致使用 `type`，避免再出现 `sessionUpdate` 等别名。
- **RequestId**：当前已有 `Types.RequestId` 与转换器，Connection 层可考虑统一使用 `RequestId` 而非 `string`/`Guid`，以便与协议规范一致（支持 null、number、string）。

---

## 四、当前数据流（子进程模式）

```
[Parent: SubprocessClient]
  - 用户输入 → SendRequest(session/prompt, ...) → processInput (agent stdin)
  - processOutput (agent stdout) → 若为 response(id) → 完成 pending TCS，返回给调用方
  - processOutput → 若为 request(method, id, params) → ClientRequestDispatcher.DispatchAsync(this) → 写 response 到 processInput
  - processOutput → 若为 notification(session/update) → HandleSessionUpdateAsync

[Child: Agent]
  - AcpCore.RunAgentAsync(agent, process.StandardInput, process.StandardOutput)
  - AgentConnection.ListenAsync: 读 stdin → ProcessMessageAsync → 调用 IAgent 方法 → 写 response 到 stdout
  - （若 Agent 需调用 Client：在子进程内需通过 stdout 发 request，父进程已能处理并回写）
```

---

## 五、文件与职责概览

| 文件/类型 | 职责 |
|-----------|------|
| `AcpCore` | 入口：RunAgentAsync / RunClientAsync，绑定 stdio 与 Connection。 |
| `Connection` | 基类：JSON-RPC 读写、ListenAsync 循环、SendRequestAsync/SendNotificationAsync。 |
| `AgentConnection` | 解析请求 → 调用 IAgent → 写回 response。 |
| `ClientConnection` | 解析请求/通知 → ClientRequestDispatcher.DispatchAsync → 写回 response（若有 id）。 |
| `ClientRequestDispatcher` | 按 method 调用 IClient，供 ClientConnection 与 SubprocessClient 复用。 |
| `SubprocessClient` | 启动子进程；父进程侧：发 request、收 response、处理 Agent 的 request/notification。 |
| `Acp.ProtocolLayer` | `IProtocolHandler`、`AgentProtocolHandler`、`ClientProtocolHandler`、`AgentRequestDispatcher`、`ClientRequestDispatcher`；协议解析与分发，与传输无关。 |

---

## 六、扩展用法示例

**注册自定义 Client 方法（如自定义 `my/custom`）：**

```csharp
var client = new MyClient();
var connection = new ClientConnection(client, Console.In, Console.Out);
connection.RegisterHandler("my/custom", async (c, parameters, ct) =>
{
    // 自定义反序列化与逻辑
    return new { ok = true };
});
await connection.ListenAsync(ct);
```

**注册自定义 Agent 方法：**

```csharp
var agent = new MyAgent();
var connection = new AgentConnection(agent, Console.In, Console.Out);
connection.Dispatcher.Register("my/agent_method", async (a, parameters, ct) =>
{
    return new { result = "done" };
});
await connection.ListenAsync(ct);
```

**换传输、复用协议（解耦后）：**

```csharp
// 同一套协议逻辑，可挂到不同传输（例如未来 WebSocket）
var handler = new AgentProtocolHandler(agent);
var connection = new Connection(stdin, stdout, handler);
await connection.ListenAsync(ct);
```

---

## 七、小结

- **已修复**：`session/update` 方法名、SessionUpdate 的 `type` 字段、Agent→Client 请求在父进程中的分发与响应回写；并抽取 `ClientRequestDispatcher` / `AgentRequestDispatcher` 统一分发逻辑，支持通过 `Register` / `RegisterHandler` 扩展方法而不改核心 switch。
- **建议后续**：协议与传输解耦（如 `IProtocolDriver`）、统一错误与取消处理、类型与命名与协议规范对齐。若需同进程内 Agent 调用 Client，可为 Agent 注入基于当前 Connection 的 IClient 桩并在连接建立时调用 `OnConnect`。
