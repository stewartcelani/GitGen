using System.Reflection;
using FluentAssertions;
using GitGen.Services;
using Xunit;

namespace GitGen.Tests.Services;

public class SystemConsoleInputTests
{
    // Using reflection to test the private static CleanInput method
    private static readonly MethodInfo CleanInputMethod = typeof(SystemConsoleInput)
        .GetMethod("CleanInput", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string? InvokeCleanInput(string? input)
    {
        return (string?)CleanInputMethod.Invoke(null, new object?[] { input });
    }

    #region CleanInput Tests - ANSI Escape Sequences

    [Theory]
    [InlineData("\x1B[0m", "")] // Reset
    [InlineData("\x1B[1m", "")] // Bold
    [InlineData("\x1B[31m", "")] // Red text
    [InlineData("\x1B[42m", "")] // Green background
    [InlineData("\x1B[2J", "")] // Clear screen
    [InlineData("Hello\x1B[0mWorld", "HelloWorld")] // Text with reset in middle
    [InlineData("\x1B[1;31mError\x1B[0m", "Error")] // Colored error text
    public void CleanInput_RemovesAnsiEscapeSequences(string input, string expected)
    {
        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region CleanInput Tests - Bracketed Paste Mode

    [Theory]
    [InlineData("\x1B[200~text\x1B[201~", "text")] // Standard bracketed paste
    [InlineData("[200~text[201~", "text")] // Without ESC character
    [InlineData("\x1B[200~multi\nline\ntext\x1B[201~", "multi\nline\ntext")] // Multiline paste
    [InlineData("before\x1B[200~pasted\x1B[201~after", "beforepastedafter")] // Mixed content
    [InlineData("[200~", "")] // Just start marker
    [InlineData("[201~", "")] // Just end marker
    [InlineData("200~text201~", "text")] // Without brackets
    public void CleanInput_RemovesBracketedPasteMarkers(string input, string expected)
    {
        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CleanInput_HandlesSpecificPasteIssue()
    {
        // This tests the specific pattern mentioned in the code: [2 at start, 1~ at end
        // Arrange
        var input = "[2some text1~";

        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be("some text");
    }

    #endregion

    #region CleanInput Tests - Control Characters

    [Fact]
    public void CleanInput_RemovesControlCharacters()
    {
        // Test null characters - they should be replaced with spaces
        var input1 = "text" + "\0" + "with" + "\0" + "nulls";
        var result1 = InvokeCleanInput(input1);
        result1.Should().Be("text with nulls");
        
        // Test various control characters
        var input2 = "text" + "\x01\x02\x03";
        var result2 = InvokeCleanInput(input2);
        result2.Should().Be("text");
        
        // Test bell character
        var input3 = "text" + "\x07" + "bell";
        var result3 = InvokeCleanInput(input3);
        result3.Should().Be("textbell");
        
        // Test escape character
        var input4 = "text" + "\x1B" + "escape";
        var result4 = InvokeCleanInput(input4);
        result4.Should().Be("textescape");
        
        // Test delete character
        var input5 = "text" + "\x7F" + "delete";
        var result5 = InvokeCleanInput(input5);
        result5.Should().Be("textdelete");
    }

    [Theory]
    [InlineData("line1\nline2", "line1\nline2")] // Newline preserved
    [InlineData("line1\rline2", "line1\rline2")] // Carriage return preserved
    [InlineData("col1\tcol2", "col1\tcol2")] // Tab preserved
    [InlineData("text with  spaces", "text with  spaces")] // Spaces preserved
    public void CleanInput_PreservesStandardWhitespace(string input, string expected)
    {
        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region CleanInput Tests - Edge Cases

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void CleanInput_HandlesNullAndEmpty(string? input, string? expected)
    {
        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CleanInput_TrimsWhitespace()
    {
        // Arrange
        var input = "  \t  text with spaces  \n  ";

        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be("text with spaces");
    }

    [Fact]
    public void CleanInput_HandlesComplexMixedInput()
    {
        // Arrange
        var input = "  \x1B[1mBold\x1B[0m [200~pasted text[201~ with \x00nulls\x7F and [2~other\x1B[31m stuff  ";

        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be("Bold pasted text with  nulls and other stuff");
    }

    #endregion

    #region CleanInput Tests - Specific Patterns

    [Theory]
    [InlineData("[123~", "")] // Three digit pattern
    [InlineData("[4567~", "")] // Four digit pattern - should remove entire pattern
    [InlineData("text[200~more", "textmore")] // Pattern in middle
    [InlineData("[2~", "")] // Specific pattern from code
    public void CleanInput_RemovesSpecificBracketPatterns(string input, string expected)
    {
        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CleanInput_HandlesMultipleCleaningPasses()
    {
        // Tests that all cleaning operations work together
        // Arrange
        var input = "\x1B[200~[200~nested\x1B[0m paste[201~[201~\x00";

        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be("nested paste");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void CleanInput_HandlesLargeInput()
    {
        // Arrange
        var largeText = new string('a', 10000);
        var input = $"\x1B[200~{largeText}\x1B[201~";

        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be(largeText);
    }

    [Fact]
    public void CleanInput_HandlesRepeatedPatterns()
    {
        // Arrange
        var input = string.Concat(Enumerable.Repeat("\x1B[31m", 100)) + "text" + 
                   string.Concat(Enumerable.Repeat("\x1B[0m", 100));

        // Act
        var result = InvokeCleanInput(input);

        // Assert
        result.Should().Be("text");
    }

    #endregion
}