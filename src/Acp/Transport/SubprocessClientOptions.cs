using System.Diagnostics;
using System.IO;

namespace Acp.Transport;

/// <summary>
/// Options for <see cref="SubprocessClient"/> (stderr forwarding, process start settings).
/// </summary>
public class SubprocessClientOptions
{
    /// <summary>If set, subprocess stderr is read in a background loop and written to this writer.</summary>
    public TextWriter? Stderr { get; init; }
    /// <summary>If set, raw transport events (jsonrpc send/receive, stderr, internal errors) are appended to this writer.</summary>
    public TextWriter? TransportLog { get; init; }

    /// <summary>Optional. If set, used as base for process start; FileName/Arguments are still overridden from constructor.</summary>
    public ProcessStartInfo? StartInfo { get; init; }
}
