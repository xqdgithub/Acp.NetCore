using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acp.Messages;
using Acp.Types;
using Acp.Transport;

namespace Acp.ConsoleTest;

/// <summary>
/// Console REPL client: extends <see cref="SubprocessClient"/> with ANSI session updates and a read-eval loop (/help, /quit, /new, /sessions, /read, /write).
/// </summary>
public class SubprocessConsoleClient : SubprocessClient
{
    private readonly TextReader _userInput;
    private readonly TextWriter _userOutput;
    private readonly string _commandAndArgs;
    private string _lastSessionUpdateKind = "";

    public SubprocessConsoleClient(string command, string[] args, TextReader userInput, TextWriter userOutput)
        : base(command, args, new SubprocessClientOptions { Stderr = userOutput })
    {
        _userInput = userInput ?? throw new ArgumentNullException(nameof(userInput));
        _userOutput = userOutput ?? throw new ArgumentNullException(nameof(userOutput));
        _commandAndArgs = command + " " + string.Join(" ", args);
    }

    public override Task<RequestPermissionResponse> RequestPermissionAsync(
        IEnumerable<PermissionOption> options,
        string sessionId,
        ToolCallUpdate toolCall,
        CancellationToken cancellationToken = default)
    {
        var list = options as IReadOnlyList<PermissionOption> ?? options.ToList();
        var allow = list.FirstOrDefault(o =>
            string.Equals(o.Kind, PermissionKind.AllowOnce, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.Kind, PermissionKind.AllowAlways, StringComparison.OrdinalIgnoreCase));
        var optionId = (allow ?? list.FirstOrDefault())?.Id;
        return Task.FromResult(PermissionOutcomes.SelectedResponse(optionId));
    }

    public override async Task SessionUpdateAsync(string sessionId, SessionUpdate update, CancellationToken cancellationToken = default)
    {
        const string ansiThought = "\x1b[2;96m";
        const string ansiMessage = "\x1b[32m";
        const string ansiReset = "\x1b[0m";

        if (update is AgentThoughtChunk thoughtChunk && !string.IsNullOrEmpty(thoughtChunk.Thought))
        {
            if (_lastSessionUpdateKind == "agent_message_chunk" || _lastSessionUpdateKind == "")
                await _userOutput.WriteAsync("\n");
            _lastSessionUpdateKind = "agent_thought_chunk";
            await _userOutput.WriteAsync(ansiThought);
            await _userOutput.WriteAsync(thoughtChunk.Thought);
            await _userOutput.WriteAsync(ansiReset);
            await _userOutput.FlushAsync(cancellationToken);
        }
        else if (update is AgentMessageChunk msgChunk)
        {
            foreach (var block in msgChunk.Message?.Content ?? new List<ContentBlock>())
            {
                if (block is TextContentBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
                {
                    if (_lastSessionUpdateKind == "agent_thought_chunk" || _lastSessionUpdateKind == "")
                        await _userOutput.WriteAsync("\n");
                    _lastSessionUpdateKind = "agent_message_chunk";
                    await _userOutput.WriteAsync(ansiMessage);
                    await _userOutput.WriteAsync(textBlock.Text);
                    await _userOutput.WriteAsync(ansiReset);
                    await _userOutput.FlushAsync(cancellationToken);
                }
            }
        }
        else
        {
            _lastSessionUpdateKind = "";
        }

        await base.SessionUpdateAsync(sessionId, update, cancellationToken);
    }

    /// <summary>Start subprocess, initialize, create session, then run REPL until /quit or EOF. Calls <see cref="SubprocessClient.StopAsync"/> on exit.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _userOutput.WriteLine($"=== Spawning ACP Agent: {_commandAndArgs} ===");
        _userOutput.WriteLine();

        await StartAsync(cancellationToken).ConfigureAwait(false);

        _userOutput.WriteLine("Initializing connection...");

        try
        {
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var initResponse = await InitializeAsync(
                1,
                new ClientCapabilities
                {
                    Fs = new FsCapabilities { ReadTextFile = true, WriteTextFile = true },
                    Terminal = true,
                    Prompt = new PromptCapabilities { Audio = true, Image = true, EmbeddedContext = false }
                },
                Implementation.Create("acp-console", "1.0.0"),
                requestCts.Token).ConfigureAwait(false);

            _userOutput.WriteLine($"Connected to: {initResponse.AgentInfo.Name} v{initResponse.AgentInfo.Version}");
            _userOutput.WriteLine();

            var sessionResponse = await SessionNewAsync(Directory.GetCurrentDirectory(), new List<McpServerConfig>(), requestCts.Token).ConfigureAwait(false);
            CurrentSessionId = sessionResponse.SessionId;
            _userOutput.WriteLine($"Session created: {CurrentSessionId}");
            _userOutput.WriteLine();

            await MainLoopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _userOutput.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            _userOutput.WriteLine("\nShutting down...");
            await StopAsync().ConfigureAwait(false);
            _userOutput.WriteLine("Goodbye!");
        }
    }

    private async Task MainLoopAsync(CancellationToken cancellationToken)
    {
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCts.CancelAfter(TimeSpan.FromSeconds(120));

        while (!cancellationToken.IsCancellationRequested)
        {
            _userOutput.Write("> ");
            _userOutput.Flush();

            var line = await _userInput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null) break;

            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("/"))
            {
                await HandleCommandAsync(line, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var promptResponse = await SessionPromptAsync(
                    CurrentSessionId,
                    new[] { new TextContentBlock(line) },
                    requestCts.Token).ConfigureAwait(false);

                foreach (var block in promptResponse.Content)
                {
                    if (block is TextContentBlock textBlock)
                    {
                        _userOutput.WriteLine();
                        _userOutput.WriteLine($"Agent: {textBlock.Text}");
                    }
                    else if (block is ImageContentBlock imageBlock)
                    {
                        _userOutput.WriteLine($"[Image: {imageBlock.Source.MimeType}]");
                    }
                    else if (block is ResourceContentBlock resourceBlock)
                    {
                        _userOutput.WriteLine($"[Resource: {resourceBlock.Resource.Uri}]");
                    }
                }
                _userOutput.WriteLine();
            }
            catch (Exception ex)
            {
                _userOutput.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private async Task HandleCommandAsync(string command, CancellationToken cancellationToken)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLower();

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCts.CancelAfter(TimeSpan.FromSeconds(120));

        switch (cmd)
        {
            case "/help":
                _userOutput.WriteLine("Available commands:");
                _userOutput.WriteLine("  /help     - Show this help");
                _userOutput.WriteLine("  /quit     - Exit the program");
                _userOutput.WriteLine("  /new      - Create a new session");
                _userOutput.WriteLine("  /sessions - List available sessions");
                _userOutput.WriteLine("  /read <path> - Read a file (client capability)");
                _userOutput.WriteLine("  /write <path> <content> - Write to a file (client capability)");
                break;

            case "/quit":
            case "/exit":
                Environment.Exit(0);
                break;

            case "/new":
                var response = await SessionNewAsync(Directory.GetCurrentDirectory(), new List<McpServerConfig>(), requestCts.Token).ConfigureAwait(false);
                CurrentSessionId = response.SessionId;
                _userOutput.WriteLine($"New session: {CurrentSessionId}");
                break;

            case "/sessions":
                var sessions = await SessionListAsync(cwd: null, cursor: null, requestCts.Token).ConfigureAwait(false);
                _userOutput.WriteLine("Available sessions:");
                foreach (var s in sessions.Sessions ?? new List<SessionInfo>())
                {
                    _userOutput.WriteLine($"  - {s.Id} (cwd: {s.Cwd})");
                }
                break;

            case "/read":
                if (parts.Length < 2)
                {
                    _userOutput.WriteLine("Usage: /read <path>");
                    return;
                }
                var readResult = await ReadTextFileAsync(parts[1], CurrentSessionId, cancellationToken: requestCts.Token).ConfigureAwait(false);
                _userOutput.WriteLine($"File content ({parts[1]}):");
                _userOutput.WriteLine(readResult.Content);
                break;

            case "/write":
                if (parts.Length < 3)
                {
                    _userOutput.WriteLine("Usage: /write <path> <content>");
                    return;
                }
                var path = parts[1];
                var content = command.Substring(command.IndexOf(parts[1], StringComparison.Ordinal) + parts[1].Length + 1);
                var writeResult = await WriteTextFileAsync(content, path, CurrentSessionId, requestCts.Token).ConfigureAwait(false);
                _userOutput.WriteLine(writeResult?.Applied == true ? "File written successfully." : "Failed to write file.");
                break;

            default:
                _userOutput.WriteLine($"Unknown command: {cmd}");
                _userOutput.WriteLine("Type /help for available commands.");
                break;
        }
    }
}
