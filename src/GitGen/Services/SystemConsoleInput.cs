namespace GitGen.Services;

/// <summary>
///     Production implementation of IConsoleInput that wraps System.Console input operations.
/// </summary>
public class SystemConsoleInput : IConsoleInput
{
    /// <inheritdoc />
    public string? ReadLine()
    {
        return Console.ReadLine();
    }

    /// <inheritdoc />
    public ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        return Console.ReadKey(intercept);
    }

    /// <inheritdoc />
    public string ReadPassword()
    {
        var pass = string.Empty;
        ConsoleKey key;
        do
        {
            var keyInfo = Console.ReadKey(true);
            key = keyInfo.Key;
            if (key == ConsoleKey.Backspace && pass.Length > 0)
            {
                Console.Write("\b \b");
                pass = pass[..^1];
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                Console.Write("*");
                pass += keyInfo.KeyChar;
            }
        } while (key != ConsoleKey.Enter);
        
        return pass;
    }
}