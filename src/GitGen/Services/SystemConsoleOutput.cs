namespace GitGen.Services;

/// <summary>
///     Production implementation of IConsoleOutput that wraps System.Console output operations.
/// </summary>
public class SystemConsoleOutput : IConsoleOutput
{
    /// <inheritdoc />
    public void Write(string value)
    {
        Console.Write(value);
    }

    /// <inheritdoc />
    public void WriteLine(string value = "")
    {
        Console.WriteLine(value);
    }

    /// <inheritdoc />
    public void Clear()
    {
        Console.Clear();
    }

    /// <inheritdoc />
    public ConsoleColor ForegroundColor
    {
        get => Console.ForegroundColor;
        set => Console.ForegroundColor = value;
    }

    /// <inheritdoc />
    public ConsoleColor BackgroundColor
    {
        get => Console.BackgroundColor;
        set => Console.BackgroundColor = value;
    }

    /// <inheritdoc />
    public void SetCursorPosition(int left, int top)
    {
        Console.SetCursorPosition(left, top);
    }

    /// <inheritdoc />
    public bool CursorVisible
    {
        get => Console.CursorVisible;
        set => Console.CursorVisible = value;
    }

    /// <inheritdoc />
    public void ResetColor()
    {
        Console.ResetColor();
    }
}