using System.Text.RegularExpressions;

namespace GitGen.Services;

/// <summary>
///     Production implementation of IConsoleInput that wraps System.Console input operations.
/// </summary>
public class SystemConsoleInput : IConsoleInput
{
    // Regex to match ANSI escape sequences including bracketed paste mode
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[a-zA-Z~]", RegexOptions.Compiled);
    
    // Specific patterns for bracketed paste mode - both with and without ESC character
    private static readonly Regex BracketedPasteStartRegex = new(@"(\x1B)?\[200~", RegexOptions.Compiled);
    private static readonly Regex BracketedPasteEndRegex = new(@"(\x1B)?\[?201~", RegexOptions.Compiled);
    
    // Additional pattern to catch any remaining bracket sequences
    private static readonly Regex BracketSequenceRegex = new(@"\[\d+~", RegexOptions.Compiled);
    
    /// <inheritdoc />
    public string? ReadLine()
    {
        var input = Console.ReadLine();
        return CleanInput(input);
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
        
        // Clean the password of any escape sequences
        return CleanInput(pass) ?? string.Empty;
    }
    
    /// <summary>
    ///     Cleans input by removing ANSI escape sequences and bracketed paste mode markers.
    /// </summary>
    /// <param name="input">The raw input string.</param>
    /// <returns>Cleaned input string.</returns>
    private static string? CleanInput(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        
        // First remove bracketed paste mode sequences specifically
        var cleaned = BracketedPasteStartRegex.Replace(input, "");
        cleaned = BracketedPasteEndRegex.Replace(cleaned, "");
        
        // Remove any remaining bracket sequences
        cleaned = BracketSequenceRegex.Replace(cleaned, "");
        
        // Then remove any other ANSI escape sequences
        cleaned = AnsiEscapeRegex.Replace(cleaned, "");
        
        // Also clean up any other control characters except standard whitespace
        cleaned = Regex.Replace(cleaned, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");
        
        // Final cleanup: remove literal bracket paste markers that might have been pasted as text
        // Handle various forms these might appear in
        cleaned = cleaned.Replace("[200~", "")
                        .Replace("200~", "")
                        .Replace("[201~", "")
                        .Replace("201~", "")
                        .Replace("[2~", "");
        
        // Also clean up cases where the brackets might be encoded differently
        cleaned = Regex.Replace(cleaned, @"\[?\d{3}~", ""); // Matches [200~, 200~, [201~, 201~, etc.
        
        // Specific pattern for the exact issue we're seeing: [2 at start, 1~ at end
        if (cleaned.StartsWith("[2") && cleaned.EndsWith("1~"))
        {
            // This looks like a wrapped paste sequence
            var startMatch = Regex.Match(cleaned, @"^\[2\d*~?");
            var endMatch = Regex.Match(cleaned, @"\d*1~$");
            if (startMatch.Success)
                cleaned = cleaned.Substring(startMatch.Length);
            if (endMatch.Success && cleaned.Length > endMatch.Length)
                cleaned = cleaned.Substring(0, cleaned.Length - endMatch.Length);
        }
        
        // Trim to remove any leading/trailing whitespace that might have been added
        return cleaned.Trim();
    }
}