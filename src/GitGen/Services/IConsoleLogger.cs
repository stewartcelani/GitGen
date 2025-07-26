namespace GitGen.Services;

/// <summary>
///     Defines a contract for a structured console logger with multiple log levels and formatting options.
///     Provides colored output and debug mode support for enhanced development experience.
/// </summary>
public interface IConsoleLogger
{
    /// <summary>
    ///     Writes a debug-level log message, only visible when debug mode is enabled.
    /// </summary>
    /// <param name="message">The message template with optional placeholders.</param>
    /// <param name="args">Arguments to format into the message template.</param>
    void Debug(string message, params object[] args);

    /// <summary>
    ///     Writes an informational log message for general application status.
    /// </summary>
    /// <param name="message">The message template with optional placeholders.</param>
    /// <param name="args">Arguments to format into the message template.</param>
    void Information(string message, params object[] args);

    /// <summary>
    ///     Writes a warning log message to indicate potential issues or important notices.
    /// </summary>
    /// <param name="message">The message template with optional placeholders.</param>
    /// <param name="args">Arguments to format into the message template.</param>
    void Warning(string message, params object[] args);

    /// <summary>
    ///     Writes an error log message for application errors and failures.
    /// </summary>
    /// <param name="message">The message template with optional placeholders.</param>
    /// <param name="args">Arguments to format into the message template.</param>
    void Error(string message, params object[] args);

    /// <summary>
    ///     Writes an error log message associated with an exception, including exception details in debug mode.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="message">The message template with optional placeholders.</param>
    /// <param name="args">Arguments to format into the message template.</param>
    void Error(Exception exception, string message, params object[] args);

    /// <summary>
    ///     Writes a success log message to indicate successful operations.
    /// </summary>
    /// <param name="message">The message template with optional placeholders.</param>
    /// <param name="args">Arguments to format into the message template.</param>
    void Success(string message, params object[] args);

    /// <summary>
    ///     Writes a message with a specified highlight color for emphasis.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="color">The console color to use for highlighting.</param>
    void Highlight(string message, ConsoleColor color);

    /// <summary>
    ///     Writes a muted, less prominent log message for supplementary information.
    /// </summary>
    /// <param name="message">The message template with optional placeholders.</param>
    /// <param name="args">Arguments to format into the message template.</param>
    void Muted(string message, params object[] args);
}