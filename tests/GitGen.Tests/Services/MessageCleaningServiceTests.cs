using FluentAssertions;
using GitGen.Services;
using Xunit;

namespace GitGen.Tests.Services;

public class MessageCleaningServiceTests
{
    [Theory]
    [InlineData("<think>Some thinking</think>Actual message", "Actual message")]
    [InlineData("Message before<thinking>Internal thoughts</thinking>Message after", "Message beforeMessage after")]
    [InlineData("<THINK>Case insensitive</THINK>Result", "Result")]
    [InlineData("No tags here", "No tags here")]
    [InlineData("<think>Nested <think>tags</think></think>Message", "</think>Message")]
    public void CleanLlmResponse_RemovesThinkingTags(string input, string expected)
    {
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Message with \"quotes\"", "Message with 'quotes'")]
    [InlineData("Command with `backticks`", "Command with 'backticks'")]
    [InlineData("Path with\\backslash", "Path with/backslash")]
    [InlineData("Text with $variable", "Text with variable")]
    [InlineData("Multiple;semicolons;here", "Multiple,semicolons,here")]
    [InlineData("Pipes|are|replaced", "Pipes-are-replaced")]
    [InlineData("Ampersand & symbol", "Ampersand and symbol")]
    public void CleanLlmResponse_SanitizesForShell(string input, string expected)
    {
        // Act
        var result = MessageCleaningService.CleanLlmResponse(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CleanLlmResponse_WithNullOrWhitespace_ReturnsNoResponseMessage()
    {
        // Act
        var result1 = MessageCleaningService.CleanLlmResponse(null!);
        var result2 = MessageCleaningService.CleanLlmResponse("");
        var result3 = MessageCleaningService.CleanLlmResponse("   ");

        // Assert
        result1.Should().Be("No response received from LLM.");
        result2.Should().Be("No response received from LLM.");
        result3.Should().Be("No response received from LLM.");
    }

    [Fact]
    public void CleanCommitMessage_WorksIdenticallyToCleanLlmResponse()
    {
        // Arrange
        var input = "<think>Planning commit</think>Fixed bug in authentication";

        // Act
        var commitResult = MessageCleaningService.CleanCommitMessage(input);
        var llmResult = MessageCleaningService.CleanLlmResponse(input);

        // Assert
        commitResult.Should().Be(llmResult);
        commitResult.Should().Be("Fixed bug in authentication");
    }
}