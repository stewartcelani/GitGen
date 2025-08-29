using FluentAssertions;
using GitGen.Services;
using Xunit;

namespace GitGen.Tests.Services;

public class MessageCleaningServiceTests
{
    #region CleanLlmResponse Tests

    [Fact]
    public void CleanLlmResponse_WithThinkTags_RemovesTags()
    {
        // Arrange
        var input = "Here is my response <think>internal thoughts here</think> with more text";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Here is my response  with more text");
    }

    [Fact]
    public void CleanLlmResponse_WithThinkingTags_RemovesTags()
    {
        // Arrange
        var input = "Start <thinking>some deep thoughts</thinking> end";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Start  end");
    }

    [Fact]
    public void CleanLlmResponse_WithMixedCaseTags_RemovesAllTags()
    {
        // Arrange
        var input = "<THINK>uppercase</THINK> and <ThInKiNg>mixed case</ThInKiNg> tags";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("and  tags");
    }

    [Fact]
    public void CleanLlmResponse_WithNestedTags_RemovesOuterTagContent()
    {
        // Arrange
        var input = "Text <think>outer <think>nested</think> thoughts</think> after";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        // Note: The regex is non-greedy, so it matches the first closing tag it finds
        result.Should().Be("Text  thoughts</think> after");
    }

    [Fact]
    public void CleanLlmResponse_WithMultilineTags_RemovesAcrossLines()
    {
        // Arrange
        var input = @"First line
<thinking>
Multi-line
thinking content
</thinking>
Last line";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be(@"First line

Last line");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void CleanLlmResponse_WithWhitespace_ReturnsFallbackMessage(string input)
    {
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be(Constants.Fallbacks.NoResponseMessage);
    }

    [Fact]
    public void CleanLlmResponse_WithNull_ReturnsFallbackMessage()
    {
        // Act
        var result = MessageCleaningService.CleanLlmResponse(null as string);
        
        // Assert
        result.Should().Be(Constants.Fallbacks.NoResponseMessage);
    }

    [Fact]
    public void CleanLlmResponse_WithNoTags_ReturnsOriginalText()
    {
        // Arrange
        var input = "This is a clean message without any tags";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void CleanLlmResponse_WithShellCharacters_SanitizesForShell()
    {
        // Arrange
        var input = "Fix \"bug\" with `command` & improve performance; use | pipe";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Fix 'bug' with 'command' and improve performance, use - pipe");
    }

    [Fact]
    public void CleanLlmResponse_WithDollarSignsAndBackslashes_RemovesOrReplaces()
    {
        // Arrange
        var input = "Cost is $100 for path\\to\\file";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Cost is 100 for path/to/file");
    }

    [Fact]
    public void CleanLlmResponse_WithMalformedTags_HandlesGracefully()
    {
        // Arrange
        var input = "Text with <think>unclosed tag and <thinking>another</thinking> one";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Text with <think>unclosed tag and  one");
    }

    #endregion

    #region CleanCommitMessage Tests

    [Fact]
    public void CleanCommitMessage_CallsCleanLlmResponse()
    {
        // Arrange
        var input = "feat: add feature <think>implementation details</think>";
        
        // Act
        var result = MessageCleaningService.CleanCommitMessage(input);
        
        // Assert
        result.Should().Be("feat: add feature");
    }

    #endregion

    #region CleanForDisplay Tests

    [Fact]
    public void CleanForDisplay_CallsCleanLlmResponse()
    {
        // Arrange
        var input = "Display this <thinking>but not this</thinking> text";
        
        // Act
        var result = MessageCleaningService.CleanForDisplay(input);
        
        // Assert
        result.Should().Be("Display this  text");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CleanLlmResponse_WithEmptyTags_RemovesTags()
    {
        // Arrange
        var input = "Text with <think></think> empty tags <thinking></thinking> here";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Text with  empty tags  here");
    }

    [Fact]
    public void CleanLlmResponse_WithSpecialCharactersInTags_RemovesTags()
    {
        // Arrange
        var input = "Message <think>with $pecial ch@rs & symbols!</think> clean";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Message  clean");
    }

    [Fact]
    public void CleanLlmResponse_TrimsWhitespace()
    {
        // Arrange
        var input = "  \t  Trimmed message  \n  ";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Trimmed message");
    }

    [Fact]
    public void CleanLlmResponse_PreservesInternalWhitespace()
    {
        // Arrange
        var input = "Line one\n\nLine two\tTabbed";
        
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);
        
        // Assert
        result.Should().Be("Line one\n\nLine two\tTabbed");
    }

    #endregion
}