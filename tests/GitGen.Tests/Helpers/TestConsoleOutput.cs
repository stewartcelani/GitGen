using System.Text;
using GitGen.Services;

namespace GitGen.Tests.Helpers;

/// <summary>
///     Test implementation of IConsoleOutput that captures console output for verification in tests.
/// </summary>
public class TestConsoleOutput : IConsoleOutput
{
    private readonly StringBuilder _output = new();
    private ConsoleColor _foregroundColor = ConsoleColor.Gray;
    private ConsoleColor _backgroundColor = ConsoleColor.Black;
    private bool _cursorVisible = true;
    private int _cursorLeft;
    private int _cursorTop;
    
    /// <summary>
    ///     Gets all captured output as a single string.
    /// </summary>
    public string GetOutput() => _output.ToString();
    
    /// <summary>
    ///     Gets all captured output split into lines.
    /// </summary>
    public string[] GetOutputLines() => GetOutput()
        .Split(new[] { Environment.NewLine }, StringSplitOptions.None);
    
    /// <summary>
    ///     Clears all captured output.
    /// </summary>
    public void ClearOutput() => _output.Clear();

    /// <inheritdoc />
    public void Write(string value)
    {
        _output.Append(value);
    }

    /// <inheritdoc />
    public void WriteLine(string value = "")
    {
        _output.AppendLine(value);
    }

    /// <inheritdoc />
    public void Clear()
    {
        // In a real console, Clear() doesn't affect the output stream, 
        // it just clears the visual display. For testing, we don't want
        // to lose the output that was written.
        _cursorLeft = 0;
        _cursorTop = 0;
    }

    /// <inheritdoc />
    public ConsoleColor ForegroundColor
    {
        get => _foregroundColor;
        set => _foregroundColor = value;
    }

    /// <inheritdoc />
    public ConsoleColor BackgroundColor
    {
        get => _backgroundColor;
        set => _backgroundColor = value;
    }

    /// <inheritdoc />
    public void SetCursorPosition(int left, int top)
    {
        _cursorLeft = left;
        _cursorTop = top;
    }

    /// <inheritdoc />
    public bool CursorVisible
    {
        get => _cursorVisible;
        set => _cursorVisible = value;
    }

    /// <inheritdoc />
    public void ResetColor()
    {
        _foregroundColor = ConsoleColor.Gray;
        _backgroundColor = ConsoleColor.Black;
    }
    
    /// <summary>
    ///     Gets the current cursor position.
    /// </summary>
    public (int Left, int Top) GetCursorPosition() => (_cursorLeft, _cursorTop);
}