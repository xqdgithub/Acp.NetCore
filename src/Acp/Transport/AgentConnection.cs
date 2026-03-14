using System.IO;
using Acp.Interfaces;
using Acp.Protocol;

namespace Acp.Transport;

/// <summary>
/// Agent-side connection: stdio transport + agent protocol handler.
/// </summary>
public class AgentConnection : Connection
{
    public AgentConnection(IAgent agent, TextReader input, TextWriter output)
        : base(input, output, new AgentProtocolHandler(agent))
    {
    }

    /// <summary>
    /// Access the dispatcher to register custom method handlers.
    /// </summary>
    public AgentRequestDispatcher Dispatcher => ((AgentProtocolHandler)Handler).Dispatcher;
}
