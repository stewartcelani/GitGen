using System.Text;
using GitGen.Services;

namespace GitGen.Tests.Helpers;

/// <summary>
/// Helper class for testing console-based interactions.
/// </summary>
public class ConsoleTestHelper : IDisposable
{
    private readonly StringWriter _output;
    private readonly StringReader _input;
    private readonly TextWriter _originalOutput;
    private readonly TextReader _originalInput;

    public ConsoleTestHelper(string? input = null)
    {
        _originalOutput = Console.Out;
        _originalInput = Console.In;
        
        _output = new StringWriter();
        Console.SetOut(_output);
        
        if (input != null)
        {
            _input = new StringReader(input);
            Console.SetIn(_input);
        }
        else
        {
            _input = new StringReader(string.Empty);
        }
    }

    public string GetOutput() => _output.ToString();
    
    public string[] GetOutputLines() => GetOutput()
        .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
        .Where(line => !string.IsNullOrEmpty(line))
        .ToArray();

    public void SetInput(string input)
    {
        var newInput = new StringReader(input);
        Console.SetIn(newInput);
        _input.Dispose();
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        Console.SetIn(_originalInput);
        _output.Dispose();
        _input.Dispose();
    }
}

/// <summary>
/// Captures console logger output for testing.
/// </summary>
public class TestConsoleLogger : IConsoleLogger
{
    private readonly List<LogEntry> _logs = new();
    
    public IReadOnlyList<LogEntry> Logs => _logs;
    
    public void Information(string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Information, string.Format(message, args)));
    }

    public void Warning(string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Warning, string.Format(message, args)));
    }

    public void Error(string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Error, string.Format(message, args)));
    }
    
    public void Error(Exception exception, string message, params object[] args)
    {
        var formattedMessage = string.Format(message, args);
        _logs.Add(new LogEntry(LogLevel.Error, $"{formattedMessage} - Exception: {exception.Message}"));
    }

    public void Success(string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Success, string.Format(message, args)));
    }
    
    public void Highlight(string message, ConsoleColor color)
    {
        _logs.Add(new LogEntry(LogLevel.Information, $"[{color}] {message}"));
    }

    public void Muted(string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Muted, string.Format(message, args)));
    }

    public void Debug(string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Debug, string.Format(message, args)));
    }

    public void Clear()
    {
        _logs.Clear();
    }
    
    public bool HasMessage(string message) => 
        _logs.Any(l => l.Message.Contains(message));
    
    public bool HasMessageOfLevel(LogLevel level, string message) => 
        _logs.Any(l => l.Level == level && l.Message.Contains(message));
}

public record LogEntry(LogLevel Level, string Message);

public enum LogLevel
{
    Information,
    Warning,
    Error,
    Success,
    Muted,
    Debug
}

