using System;
using System.Threading.Tasks;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Models;
using GitGen.Providers;
using GitGen.Services;
using Moq;
using Xunit;

namespace GitGen.Tests.Services;

public class LlmCallTrackerTests
{
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IUsageTrackingService> _mockUsageTracking;
    private readonly LlmCallTracker _tracker;

    public LlmCallTrackerTests()
    {
        _mockLogger = new Mock<IConsoleLogger>();
        _mockUsageTracking = new Mock<IUsageTrackingService>();
        _tracker = new LlmCallTracker(_mockLogger.Object, _mockUsageTracking.Object);
    }

    [Fact]
    public async Task TrackCallAsync_Success_ReturnsResultWithTimingInfo()
    {
        // Arrange
        var operation = "Test operation";
        var prompt = "Test prompt";
        var model = new ModelConfiguration
        {
            Name = "test-model",
            ModelId = "test-id",
            Provider = "test-provider"
        };

        var expectedResult = new CommitMessageResult
        {
            Message = "Test message",
            InputTokens = 100,
            OutputTokens = 50,
            TotalTokens = 150
        };

        Func<Task<CommitMessageResult>> apiCall = async () =>
        {
            await Task.Delay(100); // Simulate API delay
            return expectedResult;
        };

        _mockUsageTracking.Setup(x => x.GetSessionId()).Returns("test-session");

        // Act
        var result = await _tracker.TrackCallAsync(operation, prompt, model, apiCall);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be(expectedResult.Message);
        result.InputTokens.Should().Be(expectedResult.InputTokens);
        result.OutputTokens.Should().Be(expectedResult.OutputTokens);
        result.TotalTokens.Should().Be(expectedResult.TotalTokens);
        result.ElapsedTime.Should().BeGreaterThan(TimeSpan.Zero);
        result.Prompt.Should().Be(prompt);
        result.Model.Should().Be(model);

        // Verify logging
        _mockLogger.Verify(x => x.Debug(It.IsAny<string>()), Times.AtLeastOnce);
        _mockLogger.Verify(x => x.Muted(It.IsAny<string>()), Times.Once);

        // Verify usage tracking
        _mockUsageTracking.Verify(x => x.RecordUsageAsync(It.IsAny<UsageEntry>()), Times.Once);
    }

    [Fact]
    public async Task TrackCallAsync_WithCostInfo_CalculatesAndDisplaysCost()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Name = "gpt-4",
            ModelId = "gpt-4",
            Provider = "openai",
            Pricing = new PricingInfo
            {
                InputPer1M = 30,
                OutputPer1M = 60,
                CurrencyCode = "USD"
            }
        };

        var expectedResult = new CommitMessageResult
        {
            Message = "Result",
            InputTokens = 1000,
            OutputTokens = 500,
            TotalTokens = 1500
        };

        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(expectedResult);

        // Act
        var result = await _tracker.TrackCallAsync("test", "prompt", model, apiCall);

        // Assert
        result.Should().NotBeNull();
        
        // Verify cost display in status line
        _mockLogger.Verify(x => x.Muted(It.Is<string>(s => s.Contains("$") && s.Contains("0.06"))), Times.Once);
    }

    [Fact]
    public async Task TrackCallAsync_LongPrompt_TruncatesInDebugLog()
    {
        // Arrange
        var longPrompt = new string('x', 300);
        var model = new ModelConfiguration { Name = "test-model" };

        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(new CommitMessageResult { Message = "Result" });

        // Act
        await _tracker.TrackCallAsync("test", longPrompt, model, apiCall);

        // Assert
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("...") && s.Length < 250)), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TrackCallAsync_ShortPrompt_ShowsFullPromptInDebug()
    {
        // Arrange
        var shortPrompt = "Short prompt";
        var model = new ModelConfiguration { Name = "test-model" };

        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(new CommitMessageResult { Message = "Result" });

        // Act
        await _tracker.TrackCallAsync("test", shortPrompt, model, apiCall);

        // Assert
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains(shortPrompt))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TrackCallAsync_ApiCallThrows_LogsErrorAndRethrows()
    {
        // Arrange
        var expectedException = new InvalidOperationException("API Error");
        Func<Task<CommitMessageResult>> apiCall = () => throw expectedException;

        // Act & Assert
        var act = async () => await _tracker.TrackCallAsync("test", "prompt", null, apiCall);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("API Error");

        // Verify error logging
        _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("failed after"))), Times.Once);
    }

    [Fact]
    public async Task TrackCallAsync_WithNullModel_HandlesGracefully()
    {
        // Arrange
        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(new CommitMessageResult { Message = "Result" });

        // Act
        var result = await _tracker.TrackCallAsync("test", "prompt", null, apiCall);

        // Assert
        result.Should().NotBeNull();
        result.Model.Should().BeNull();
        
        // Verify "Unknown" is used for null model
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("Unknown"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TrackCallAsync_RecordsUsageWithGitInfo()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Name = "test-model",
            ModelId = "test-id",
            Provider = "test-provider"
        };

        var expectedResult = new CommitMessageResult
        {
            Message = "Result",
            InputTokens = 100,
            OutputTokens = 50,
            TotalTokens = 150
        };

        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(expectedResult);
        _mockUsageTracking.Setup(x => x.GetSessionId()).Returns("session-123");

        // Act
        await _tracker.TrackCallAsync("test", "prompt", model, apiCall);

        // Assert
        _mockUsageTracking.Verify(x => x.RecordUsageAsync(It.Is<UsageEntry>(entry =>
            entry.SessionId == "session-123" &&
            entry.Model.Name == "test-model" &&
            entry.Tokens.Input == 100 &&
            entry.Tokens.Output == 50 &&
            entry.Success == true &&
            entry.Operation == "commit_message_generation"
        )), Times.Once);
    }

    [Fact]
    public async Task TrackCallAsync_UsageTrackingFails_DoesNotThrow()
    {
        // Arrange
        var model = new ModelConfiguration { Name = "test-model" };
        var expectedResult = new CommitMessageResult { Message = "Result" };

        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(expectedResult);
        
        _mockUsageTracking
            .Setup(x => x.RecordUsageAsync(It.IsAny<UsageEntry>()))
            .ThrowsAsync(new Exception("Storage error"));

        // Act & Assert - Should not throw
        var result = await _tracker.TrackCallAsync("test", "prompt", model, apiCall);
        result.Should().NotBeNull();
        
        // Verify error was logged
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("Failed to record usage"))), Times.Once);
    }

    [Fact]
    public async Task TrackCallAsync_WithIndentation_AddsIndentToLogs()
    {
        // Arrange
        var indent = "  ";
        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(new CommitMessageResult { Message = "Result" });

        // Act
        await _tracker.TrackCallAsync("test", "prompt", null, apiCall, indent);

        // Assert
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.StartsWith(indent))), Times.AtLeastOnce);
        _mockLogger.Verify(x => x.Muted(It.Is<string>(s => s.StartsWith(indent))), Times.Once);
    }

    [Fact]
    public async Task TrackCallAsync_LongResponse_TruncatesInDebugLog()
    {
        // Arrange
        var longMessage = new string('x', 150);
        var expectedResult = new CommitMessageResult { Message = longMessage };

        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(expectedResult);

        // Act
        await _tracker.TrackCallAsync("test", "prompt", null, apiCall);

        // Assert
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("Response preview:") && s.Contains("..."))), Times.Once);
    }

    [Fact]
    public async Task TrackCallAsync_EmptyResponse_HandlesGracefully()
    {
        // Arrange
        var expectedResult = new CommitMessageResult { Message = "" };
        Func<Task<CommitMessageResult>> apiCall = () => Task.FromResult(expectedResult);

        // Act
        var result = await _tracker.TrackCallAsync("test", "prompt", null, apiCall);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().BeEmpty();
    }
}