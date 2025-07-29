using GitGen.Services;

namespace GitGen.Tests.Helpers;

/// <summary>
///     Test implementation of IConsoleInput that allows programmatic control of console input in tests.
/// </summary>
public class TestConsoleInput : IConsoleInput
{
    private readonly Queue<string> _lineInputs = new();
    private readonly Queue<ConsoleKeyInfo> _keyInputs = new();
    private readonly Queue<string> _passwordInputs = new();

    /// <summary>
    ///     Adds a line of input that will be returned by the next call to ReadLine.
    /// </summary>
    public void AddLineInput(string input)
    {
        _lineInputs.Enqueue(input);
    }

    /// <summary>
    ///     Adds multiple lines of input that will be returned by successive calls to ReadLine.
    /// </summary>
    public void AddLineInputs(params string[] inputs)
    {
        foreach (var input in inputs)
        {
            _lineInputs.Enqueue(input);
        }
    }

    /// <summary>
    ///     Adds a key press that will be returned by the next call to ReadKey.
    /// </summary>
    public void AddKeyInput(ConsoleKeyInfo keyInfo)
    {
        _keyInputs.Enqueue(keyInfo);
    }

    /// <summary>
    ///     Adds a key press using the specified character.
    /// </summary>
    public void AddKeyInput(char keyChar)
    {
        var key = keyChar switch
        {
            '\r' => ConsoleKey.Enter,
            '\n' => ConsoleKey.Enter,
            '\b' => ConsoleKey.Backspace,
            '\t' => ConsoleKey.Tab,
            ' ' => ConsoleKey.Spacebar,
            _ => (ConsoleKey)char.ToUpper(keyChar)
        };
        
        _keyInputs.Enqueue(new ConsoleKeyInfo(keyChar, key, false, false, false));
    }

    /// <summary>
    ///     Adds a password that will be returned by the next call to ReadPassword.
    /// </summary>
    public void AddPasswordInput(string password)
    {
        _passwordInputs.Enqueue(password);
    }

    /// <inheritdoc />
    public string? ReadLine()
    {
        return _lineInputs.Count > 0 ? _lineInputs.Dequeue() : null;
    }

    /// <inheritdoc />
    public ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        if (_keyInputs.Count > 0)
        {
            return _keyInputs.Dequeue();
        }
        
        // Return Enter key as default to prevent infinite loops in tests
        return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
    }

    /// <inheritdoc />
    public string ReadPassword()
    {
        return _passwordInputs.Count > 0 ? _passwordInputs.Dequeue() : string.Empty;
    }
}