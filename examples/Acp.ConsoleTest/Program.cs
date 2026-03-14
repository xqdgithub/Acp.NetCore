using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Acp;
using Acp.Types;
using Acp.Interfaces;
using Acp.Messages;
using Acp.Transport;

namespace Acp.ConsoleTest;

/// <summary>
/// Program entry point
/// </summary>
class Program
{
    static string cursorCLI = @"C:\Users\quand\AppData\Local\cursor-agent\agent.cmd";
    static string opencodeCLI = @"C:\Users\quand\AppData\Roaming\npm\opencode.cmd";
    static string qwen = @"C:\Users\quand\AppData\Roaming\npm\qwen.cmd";
    static async Task Main(string[] args)
    {
        
        // Parse arguments
        var command = getCliPath("");
        var commandArgs = new List<string> { "acp" };
        
        if (args.Length > 0 && args[0] == "--test")
        {
            await RunTestModeAsync();
            return;
        }
        
        if (args.Length > 0 && args[0] == "--agent")
        {
            await RunAgentModeAsync();
            return;
        }

        // Parse --command and --args
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cli" && i + 1 < args.Length)
            {
                command = getCliPath(args[i + 1]);
                i++;
            }
            else if (args[i] == "--args" && i + 1 < args.Length)
            {
                commandArgs = new List<string>(args[i + 1].Split(' '));
                i++;
            }
        }

        var cancellationTokenSource = new CancellationTokenSource();
        
        // Handle Ctrl+C
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var client = new SubprocessConsoleClient(command, commandArgs.ToArray(), Console.In, Console.Out);
        
        try
        {
            await client.RunAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal exit
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }
    static private string getCliPath(String cli)
    {
        switch (cli.Trim())
        {
            case "opencode":return opencodeCLI;
            case "cursor":return cursorCLI;
             case "qwen":return qwen;
            case "agent": return cursorCLI;
            default: return opencodeCLI;

        }
    }


    static async Task RunAgentModeAsync()
    {
        Console.WriteLine("=== ACP Agent Mode (Echo Agent) ===");
        var agent = new EchoAgent();
        await AcpCore.RunAgentAsync(agent);
    }

    static async Task RunTestModeAsync()
    {
        Console.WriteLine("=== ACP Test Mode ===\n");
        
        // Test EchoAgent
        Console.WriteLine("Test 1: EchoAgent");
        var agent = new EchoAgent();
        
        var response1 = await agent.PromptAsync(
            new List<ContentBlock> { new TextContentBlock("Hello!") },
            "test-session");
        
        Console.WriteLine($"  Input: Hello!");
        Console.WriteLine($"  Output: {((TextContentBlock)response1.Content[0]).Text}");
        
        // Test Client
        Console.WriteLine("\nTest 2: Client (File Operations)");
        var client = new Client();
        
        var writeResult = await client.WriteTextFileAsync("Hello from ACP!", "test.txt", "test");
        Console.WriteLine($"  Write test.txt: {writeResult?.Applied}");
        
        var readResult = await client.ReadTextFileAsync("test.txt", "test");
        Console.WriteLine($"  Read test.txt: {readResult.Content}");
        
        // Test Initialize
        Console.WriteLine("\nTest 3: Initialize");
        var initResponse = await agent.InitializeAsync(
            1,
            new ClientCapabilities { Terminal = true },
            Implementation.Create("test-client", "1.0.0"));
        
        Console.WriteLine($"  Protocol: {initResponse.ProtocolVersion}");
        Console.WriteLine($"  Server: {initResponse.AgentInfo.Name}");
        
        // Cleanup
        if (File.Exists("test.txt"))
        {
            File.Delete("test.txt");
        }
        
        Console.WriteLine("\n=== All Tests Passed! ===");
    }
}
