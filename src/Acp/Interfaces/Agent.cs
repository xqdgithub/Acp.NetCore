using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Base agent implementation providing default implementations for all methods.
/// </summary>
public class Agent : IAgent
{
    public virtual Task<InitializeResponse> InitializeAsync(
        int protocolVersion,
        ClientCapabilities? clientCapabilities = null,
        Implementation? clientInfo = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InitializeResponse
        {
            ProtocolVersion = 1,
            AgentCapabilities = new AgentCapabilities(),
            AgentInfo = Implementation.Create("acp-dotnet", "1.0.0")
        });
    }

    public virtual Task<NewSessionResponse> NewSessionAsync(
        string cwd,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NewSessionResponse
        {
            SessionId = Guid.NewGuid().ToString()
        });
    }

    public virtual Task<LoadSessionResponse?> LoadSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<LoadSessionResponse?>(null);
    }

    public virtual Task<ListSessionsResponse> ListSessionsAsync(
        string? cursor = null,
        string? cwd = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ListSessionsResponse
        {
            Sessions = new List<SessionInfo>()
        });
    }

    public virtual Task<SetSessionModeResponse?> SetSessionModeAsync(
        string modeId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SetSessionModeResponse?>(new SetSessionModeResponse { ModeId = modeId });
    }

    public virtual Task<SetSessionModelResponse?> SetSessionModelAsync(
        string modelId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SetSessionModelResponse?>(new SetSessionModelResponse { ModelId = modelId });
    }

    public virtual Task<SetSessionConfigOptionResponse?> SetConfigOptionAsync(
        string configId,
        string value,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SetSessionConfigOptionResponse?>(new SetSessionConfigOptionResponse { ConfigId = configId });
    }

    public virtual Task<AuthenticateResponse?> AuthenticateAsync(
        string methodId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AuthenticateResponse?>(new AuthenticateResponse { Authenticated = true });
    }

    public virtual Task<PromptResponse> PromptAsync(
        IEnumerable<ContentBlock> prompt,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var textBlocks = prompt.OfType<TextContentBlock>().ToList();
        var input = string.Join(" ", textBlocks.Select(t => t.Text));
        
        return Task.FromResult(new PromptResponse
        {
            Content = new List<ContentBlock> { new TextContentBlock { Text = "Echo: " + input } }
        });
    }

    public virtual Task<ForkSessionResponse> ForkSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ForkSessionResponse
        {
            SessionId = Guid.NewGuid().ToString()
        });
    }

    public virtual Task<ResumeSessionResponse> ResumeSessionAsync(
        string cwd,
        string sessionId,
        List<McpServerConfig>? mcpServers = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResumeSessionResponse
        {
            SessionId = sessionId
        });
    }

    public virtual Task CancelAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task<Dictionary<string, object?>> ExtMethodAsync(
        string method,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<string, object?>());
    }

    public virtual Task ExtNotificationAsync(
        string method,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual void OnConnect(IClient client)
    {
        // Override to handle client connection
    }
}
