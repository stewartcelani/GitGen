namespace GitGen.Services;

/// <summary>
///     Defines a contract for console input operations to enable testable console interactions.
/// </summary>
public interface IConsoleInput
{
    /// <summary>
    ///     Reads the next line of characters from the standard input stream.
    /// </summary>
    /// <returns>The next line of characters from the input stream, or null if no more lines are available.</returns>
    string? ReadLine();

    /// <summary>
    ///     Obtains the next character or function key pressed by the user.
    /// </summary>
    /// <param name="intercept">true to not display the pressed key in the console window; otherwise, false.</param>
    /// <returns>An object that describes the ConsoleKey constant and Unicode character, if any, that correspond to the pressed console key.</returns>
    ConsoleKeyInfo ReadKey(bool intercept = false);

    /// <summary>
    ///     Reads a password from the console input, masking the characters.
    /// </summary>
    /// <returns>The password entered by the user.</returns>
    string ReadPassword();
}