using System;

namespace Acp.Exceptions;

/// <summary>
/// Base exception for ACP errors
/// </summary>
public class AcpException : Exception
{
    public AcpException(string message) : base(message) { }
    public AcpException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when JSON-RPC returns an error
/// </summary>
public class RpcException : AcpException
{
    public int Code { get; }
    
    public RpcException(int code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Exception thrown when protocol version mismatch
/// </summary>
public class ProtocolException : AcpException
{
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }
    
    public ProtocolException(int expected, int actual) 
        : base($"Protocol version mismatch: expected {expected}, got {actual}")
    {
        ExpectedVersion = expected;
        ActualVersion = actual;
    }
}

/// <summary>
/// Exception thrown when session is not found
/// </summary>
public class SessionNotFoundException : AcpException
{
    public string SessionId { get; }
    
    public SessionNotFoundException(string sessionId) 
        : base($"Session not found: {sessionId}")
    {
        SessionId = sessionId;
    }
}

/// <summary>
/// Exception thrown when transport fails
/// </summary>
public class TransportException : AcpException
{
    public TransportException(string message) : base(message) { }
    public TransportException(string message, Exception innerException) : base(message, innerException) { }
}
