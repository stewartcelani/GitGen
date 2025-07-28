using FluentAssertions;
using GitGen.Configuration;
using GitGen.Providers;
using GitGen.Services;
using NSubstitute;
using Xunit;

namespace GitGen.Tests.Services;

public class LlmCallTrackerTests : TestBase
{
    private readonly LlmCallTracker _tracker;

    public LlmCallTrackerTests()
    {
        _tracker = new LlmCallTracker(Logger);
    }

    [Fact]
    public async Task TrackCallAsync_WithSuccessfulCall_ReturnsResultWithTiming()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Name = "test-model",
            ModelId = "gpt-4",
            Pricing = new PricingInfo
            {
                InputPer1M = 10m,
                OutputPer1M = 30m,
                CurrencyCode = "USD"
            }
        };
        
        var expectedResult = new CommitMessageResult
        {
            Message = "Test commit message",
            InputTokens = 100,
            OutputTokens = 20,
            TotalTokens = 120
        };

        // Act
        var result = await _tracker.TrackCallAsync(
            "Test operation",
            "Test prompt",
            model,
            async () => 
            {
                await Task.Delay(100); // Simulate API call
                return expectedResult;
            });

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be(expectedResult.Message);
        result.InputTokens.Should().Be(expectedResult.InputTokens);
        result.OutputTokens.Should().Be(expectedResult.OutputTokens);
        result.ElapsedTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(90));
        result.Prompt.Should().Be("Test prompt");
        result.Model.Should().Be(model);
    }

    [Fact]
    public async Task TrackCallAsync_LogsDebugInformation()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Name = "test-model",
            ModelId = "gpt-4"
        };
        
        var prompt = "Generate a commit message for these changes";

        // Act
        await _tracker.TrackCallAsync(
            "Generating commit",
            prompt,
            model,
            async () => new CommitMessageResult { Message = "Result" });

        // Assert
        Logger.Received().Debug(Arg.Is<string>(s => s.Contains("Generating commit")));
        Logger.Received().Debug(Arg.Is<string>(s => s.Contains("Model:")), Arg.Any<object[]>());
        Logger.Received().Debug(Arg.Is<string>(s => s.Contains("Prompt length:")), Arg.Any<object[]>());
    }

    [Fact]
    public async Task TrackCallAsync_WithLongPrompt_ShowsTruncatedPreview()
    {
        // Arrange
        var longPrompt = new string('x', 300);

        // Act
        await _tracker.TrackCallAsync(
            "Test",
            longPrompt,
            null,
            async () => new CommitMessageResult { Message = "Result" });

        // Assert
        Logger.Received().Debug(Arg.Is<string>(s => s.Contains("Prompt preview:")), Arg.Any<object[]>());
    }

    [Fact]
    public async Task TrackCallAsync_WithFailure_LogsErrorAndRethrows()
    {
        // Arrange
        var expectedException = new InvalidOperationException("API error");

        // Act & Assert
        var act = () => _tracker.TrackCallAsync(
            "Test operation",
            "prompt",
            null,
            () => throw expectedException);

        await act.Should().ThrowAsync<InvalidOperationException>();
        Logger.Received().Error(Arg.Is<string>(s => s.Contains("failed after")));
    }

    [Fact]
    public async Task TrackCallAsync_DisplaysCostInformation()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Name = "test-model",
            Pricing = new PricingInfo
            {
                InputPer1M = 3m,
                OutputPer1M = 15m,
                CurrencyCode = "USD"
            }
        };
        
        var result = new CommitMessageResult
        {
            Message = "Test",
            InputTokens = 1000,
            OutputTokens = 100,
            TotalTokens = 1100
        };

        // Act
        await _tracker.TrackCallAsync(
            "Test",
            "prompt",
            model,
            async () => result);

        // Assert
        Logger.Received().Muted(Arg.Is<string>(s => s.Contains("$")));
    }
}