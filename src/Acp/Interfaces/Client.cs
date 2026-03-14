using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;

namespace Acp.Interfaces;

/// <summary>
/// Base client implementation providing default implementations for all methods.
/// </summary>
public class Client : IClient
{
    public virtual Task<RequestPermissionResponse> RequestPermissionAsync(
        IEnumerable<PermissionOption> options,
        string sessionId,
        ToolCallUpdate toolCall,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PermissionOutcomes.SelectedResponse(options.FirstOrDefault()?.Id));
    }

    public virtual Task SessionUpdateAsync(
        string sessionId,
        SessionUpdate update,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task<WriteTextFileResponse?> WriteTextFileAsync(
        string content,
        string path,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            File.WriteAllText(path, content);
            return Task.FromResult<WriteTextFileResponse?>(new WriteTextFileResponse { Applied = true });
        }
        catch
        {
            return Task.FromResult<WriteTextFileResponse?>(new WriteTextFileResponse { Applied = false });
        }
    }

    public virtual Task<ReadTextFileResponse> ReadTextFileAsync(
        string path,
        string sessionId,
        int? limit = null,
        int? line = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return Task.FromResult(new ReadTextFileResponse { Content = "" });
        }
        
        var content = File.ReadAllText(path);
        
        if (line.HasValue && line.Value > 0)
        {
            var lines = content.Split('\n');
            if (line.Value <= lines.Length)
            {
                content = lines[line.Value - 1];
            }
            else
            {
                content = "";
            }
        }
        
        if (limit.HasValue && limit.Value > 0 && content.Length > limit.Value)
        {
            content = content.Substring(0, limit.Value);
        }
        
        return Task.FromResult(new ReadTextFileResponse { Content = content });
    }

    public virtual Task<CreateTerminalResponse> CreateTerminalAsync(
        string command,
        string sessionId,
        List<string>? args = null,
        string? cwd = null,
        List<EnvVariable>? env = null,
        int? outputByteLimit = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CreateTerminalResponse
        {
            TerminalId = Guid.NewGuid().ToString()
        });
    }

    public virtual Task<TerminalOutputResponse> TerminalOutputAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TerminalOutputResponse
        {
            Exited = false
        });
    }

    public virtual Task<ReleaseTerminalResponse?> ReleaseTerminalAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ReleaseTerminalResponse?>(new ReleaseTerminalResponse { Released = true });
    }

    public virtual Task<WaitForTerminalExitResponse> WaitForTerminalExitAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new WaitForTerminalExitResponse { ExitCode = 0 });
    }

    public virtual Task<KillTerminalCommandResponse?> KillTerminalAsync(
        string sessionId,
        string terminalId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<KillTerminalCommandResponse?>(new KillTerminalCommandResponse { Killed = true });
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

    public virtual void OnConnect(IAgent agent)
    {
        // Override to handle agent connection
    }
}

/// <summary>
/// A simple echo agent for testing.
/// </summary>
public class EchoAgent : Agent
{
    public override Task<PromptResponse> PromptAsync(
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
}
