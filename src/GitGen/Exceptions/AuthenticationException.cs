namespace GitGen.Exceptions;

/// <summary>
///     Exception thrown when authentication or API key configuration issues are detected.
/// </summary>
public class AuthenticationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthenticationException" /> class with a default message.
    /// </summary>
    public AuthenticationException() : base("Authentication failed")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthenticationException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AuthenticationException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthenticationException" /> class with a specified error message and a
    ///     reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}