using System.Text.RegularExpressions;

namespace GitGen.Services;

/// <summary>
///     A console logger implementation that provides structured logging with colored output and debug mode support.
///     Supports both simple output mode and detailed debug mode with timestamps and categorization.
/// </summary>
public class ConsoleLogger : IConsoleLogger
{
    private static bool _debugMode;
    private readonly string _categoryName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsoleLogger" /> class with a specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for this logger instance, typically the class name.</param>
    public ConsoleLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    /// <inheritdoc />
    public void Debug(string message, params object[] args)
    {
        if (_debugMode)
            WriteLog("DEBUG", ConsoleColor.DarkGray, message, args);
    }

    /// <inheritdoc />
    public void Information(string message, params object[] args)
    {
        if (_debugMode)
            WriteLog("INFO", ConsoleColor.White, message, args);
        else
            Console.WriteLine(Format(message, args));
    }

    /// <inheritdoc />
    public void Warning(string message, params object[] args)
    {
        if (_debugMode)
            WriteLog("WARN", ConsoleColor.Yellow, message, args);
        else
            WriteColored(ConsoleColor.Yellow, Format(message, args));
    }

    /// <inheritdoc />
    public void Error(string message, params object[] args)
    {
        if (_debugMode)
            WriteLog("ERROR", ConsoleColor.Red, message, args);
        else
            WriteColored(ConsoleColor.Red, Format(message, args));
    }

    /// <inheritdoc />
    public void Error(Exception exception, string message, params object[] args)
    {
        Error(message, args);
        if (_debugMode && exception != null)
        {
            WriteColored(ConsoleColor.DarkRed, $"Exception: {exception.Message}");
            if (exception.StackTrace != null)
                WriteColored(ConsoleColor.DarkRed, exception.StackTrace);
        }
    }

    /// <inheritdoc />
    public void Success(string message, params object[] args)
    {
        if (_debugMode)
            WriteLog("SUCCESS", ConsoleColor.Green, message, args);
        else
            WriteColored(ConsoleColor.Green, Format(message, args));
    }

    /// <inheritdoc />
    public void Highlight(string message, ConsoleColor color)
    {
        WriteColored(color, message);
    }

    /// <inheritdoc />
    public void Muted(string message, params object[] args)
    {
        WriteColored(ConsoleColor.DarkGray, Format(message, args));
    }

    /// <summary>
    ///     Sets the global debug mode for all ConsoleLogger instances.
    ///     When enabled, shows detailed output with timestamps and debug information.
    /// </summary>
    /// <param name="debugMode">True to enable debug mode; false for normal output mode.</param>
    public static void SetDebugMode(bool debugMode)
    {
        _debugMode = debugMode;
    }

    private void WriteLog(string level, ConsoleColor color, string message, params object[] args)
    {
        var timestamp = DateTime.Now.ToString(Constants.UI.TimestampFormat);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"{timestamp} ");
        Console.ForegroundColor = color;
        Console.Write($"[{level}] ");
        Console.WriteLine(Format(message, args));
        Console.ResetColor();
    }

    private void WriteColored(ConsoleColor color, string message)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static string Format(string template, params object[] args)
    {
        if (args == null || args.Length == 0)
            return template;

        try
        {
            // Simple structured logging support: replace {Name} with {0}, {Count} with {1}, etc.
            var index = 0;
            var result = Regex.Replace(
                template,
                @"\{([a-zA-Z_][a-zA-Z0-9_]*)\}",
                m => $"{{{index++}}}");

            return string.Format(result, args);
        }
        catch
        {
            return template + " " + string.Join(" ", args);
        }
    }
}

/// <summary>
///     Factory for creating ConsoleLogger instances with typed or named categories.
///     Provides a centralized way to create logger instances throughout the application.
/// </summary>
public class ConsoleLoggerFactory
{
    /// <summary>
    ///     Creates a logger instance with a category name derived from the specified type.
    /// </summary>
    /// <typeparam name="T">The type to use for the logger category name.</typeparam>
    /// <returns>A new <see cref="IConsoleLogger" /> instance.</returns>
    public virtual IConsoleLogger CreateLogger<T>()
    {
        return new ConsoleLogger(typeof(T).Name);
    }

    /// <summary>
    ///     Creates a logger instance with the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <returns>A new <see cref="IConsoleLogger" /> instance.</returns>
    public virtual IConsoleLogger CreateLogger(string categoryName)
    {
        return new ConsoleLogger(categoryName);
    }
}