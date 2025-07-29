namespace GitGen.Services;

/// <summary>
///     Defines a contract for console output operations to enable testable console interactions.
/// </summary>
public interface IConsoleOutput
{
    /// <summary>
    ///     Writes the specified string value to the standard output stream.
    /// </summary>
    /// <param name="value">The value to write.</param>
    void Write(string value);

    /// <summary>
    ///     Writes the specified string value, followed by the current line terminator, to the standard output stream.
    /// </summary>
    /// <param name="value">The value to write. If value is null, only the line terminator is written.</param>
    void WriteLine(string value = "");

    /// <summary>
    ///     Clears the console buffer and corresponding console window of display information.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Gets or sets the foreground color of the console.
    /// </summary>
    ConsoleColor ForegroundColor { get; set; }

    /// <summary>
    ///     Gets or sets the background color of the console.
    /// </summary>
    ConsoleColor BackgroundColor { get; set; }

    /// <summary>
    ///     Sets the position of the cursor.
    /// </summary>
    /// <param name="left">The column position of the cursor. Columns are numbered from left to right starting at 0.</param>
    /// <param name="top">The row position of the cursor. Rows are numbered from top to bottom starting at 0.</param>
    void SetCursorPosition(int left, int top);

    /// <summary>
    ///     Gets or sets a value indicating whether the cursor is visible.
    /// </summary>
    bool CursorVisible { get; set; }

    /// <summary>
    ///     Resets the foreground and background console colors to their defaults.
    /// </summary>
    void ResetColor();
}