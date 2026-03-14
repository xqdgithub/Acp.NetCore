using System;
using System.IO;
using System.Text.Json;
using Acp.Messages;
using Acp.Types;

namespace Acp.Transport;

/// <summary>
/// Core entry points for running ACP agents and clients.
/// </summary>
public static class AcpCore
{
    /// <summary>
    /// Default stdio buffer size (50MB for multimodal use cases).
    /// </summary>
    public const int DefaultStdioBufferSize = 50 * 1024 * 1024;

    /// <summary>
    /// Run an ACP agent over the standard I/O streams.
    /// </summary>
    public static async Task RunAgentAsync(
        Interfaces.IAgent agent,
        TextReader? input = null,
        TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        input ??= Console.In;
        output ??= Console.Out;
        
        var connection = new AgentConnection(agent, input, output);
        await connection.ListenAsync(cancellationToken);
    }

    /// <summary>
    /// Run an ACP client over the standard I/O streams.
    /// </summary>
    public static async Task RunClientAsync(
        Interfaces.IClient client,
        TextReader? input = null,
        TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        input ??= Console.In;
        output ??= Console.Out;
        
        var connection = new ClientConnection(client, input, output);
        await connection.ListenAsync(cancellationToken);
    }
}
