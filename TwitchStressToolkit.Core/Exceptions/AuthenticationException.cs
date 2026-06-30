using System;

namespace TwitchStressToolkit.Core.Exceptions;

public class AuthenticationException : Exception
{
    public string? Username { get; }
    public string? Reason { get; }

    public AuthenticationException(string message) : base(message) { }

    public AuthenticationException(string message, Exception inner) : base(message, inner) { }

    public AuthenticationException(string message, string? username, string? reason) : base(message)
    {
        Username = username;
        Reason = reason;
    }
}
