using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Acp.Protocol;

/// <summary>
/// 协议处理器基类，封装 JSON-RPC 消息解析、错误处理和响应构建的公共逻辑。
/// 消除 ClientProtocolHandler 和 AgentProtocolHandler 的代码重复。
/// </summary>
/// <typeparam name="TDispatcher">请求分发器类型</typeparam>
public abstract class ProtocolHandlerBase<TDispatcher> : IProtocolHandler
    where TDispatcher : class
{
    /// <summary>请求分发器</summary>
    protected readonly TDispatcher Dispatcher;
    
    /// <summary>JSON 序列化选项</summary>
    protected readonly JsonSerializerOptions JsonOptions;

    /// <summary>
    /// 初始化协议处理器基类
    /// </summary>
    /// <param name="dispatcher">请求分发器实例</param>
    /// <param name="jsonOptions">可选的 JSON 序列化选项</param>
    protected ProtocolHandlerBase(TDispatcher dispatcher, JsonSerializerOptions? jsonOptions = null)
    {
        Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        JsonOptions = jsonOptions ?? CreateDefaultJsonOptions();
    }

    /// <inheritdoc />
    public async Task<string?> ProcessMessageAsync(string requestLine, CancellationToken cancellationToken = default)
    {
        object? id = null;
        bool isNotification = false;
        
        try
        {
            using var doc = JsonDocument.Parse(requestLine);
            var root = doc.RootElement;

            if (!root.TryGetProperty("method", out var methodEl))
                return BuildErrorResponse(null, -32600, "Invalid Request: missing method");

            var method = methodEl.GetString() ?? "";
            var hasId = root.TryGetProperty("id", out var idElement);
            if (hasId) id = idElement.Clone();
            isNotification = !hasId;

            JsonElement? parameters = null;
            if (root.TryGetProperty("params", out var paramsElement))
                parameters = paramsElement;

            var result = await DispatchCoreAsync(method, parameters, cancellationToken);

            if (isNotification) return null;

            return BuildSuccessResponse(id, result);
        }
        catch (JsonException ex)
        {
            return isNotification ? null : BuildErrorResponse(id, -32700, "Parse error: " + ex.Message);
        }
        catch (Exception ex)
        {
            return isNotification ? null : BuildErrorResponse(id, -32603, ex.Message);
        }
    }

    /// <summary>
    /// 核心分发逻辑，由子类实现具体的请求路由
    /// </summary>
    /// <param name="method">RPC 方法名</param>
    /// <param name="parameters">请求参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>方法执行结果</returns>
    protected abstract Task<object?> DispatchCoreAsync(
        string method, 
        JsonElement? parameters, 
        CancellationToken cancellationToken);

    /// <summary>
    /// 构建成功响应 JSON
    /// </summary>
    protected string BuildSuccessResponse(object? id, object? result)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }, JsonOptions);

    /// <summary>
    /// 构建错误响应 JSON
    /// </summary>
    protected static string BuildErrorResponse(object? id, int code, string message)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } });

    /// <summary>
    /// 创建默认的 JSON 序列化选项
    /// </summary>
    protected static JsonSerializerOptions CreateDefaultJsonOptions() => new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}