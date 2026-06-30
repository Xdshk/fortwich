using System;

namespace TwitchStressToolkit.Core.Exceptions;

public class ConnectionException : Exception
{
    public string? Channel { get; }
    public string? AccountName { get; }

    public ConnectionException(string message) : base(message) { }

    public ConnectionException(string message, Exception inner) : base(message, inner) { }

    public ConnectionException(string message, string? channel, string? accountName) : base(message)
    {
        Channel = channel;
        AccountName = accountName;
    }
}
