using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Models;
using GitGen.Providers;
using GitGen.Services;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace GitGen.Tests.Integration;

/// <summary>
/// Integration test to verify that token usage display is properly aligned
/// when testing multiple models in ConfigurationMenuService.
/// </summary>
public class TokenDisplayAlignmentTest
{
    private readonly ITestOutputHelper _output;
    
    public TokenDisplayAlignmentTest(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public async Task LlmCallTracker_WithIndent_ShouldAlignTokenUsageCorrectly()
    {
        // Arrange
        var capturedOutput = new StringBuilder();
        var logger = new TestLogger(capturedOutput);
        var usageTracking = Substitute.For<IUsageTrackingService>();
        usageTracking.GetSessionId().Returns("test-session-id");
        var tracker = new LlmCallTracker(logger, usageTracking);
        
        var model = new ModelConfiguration
        {
            Name = "Test Model",
            ModelId = "test-model",
            Type = "openai-compatible",
            Pricing = new PricingInfo { InputPer1M = 0.1m, OutputPer1M = 0.2m, CurrencyCode = "USD" }
        };
        
        // Act - Test with indentation
        var indent = "  ";
        await tracker.TrackCallAsync(
            "Testing connection",
            "test prompt",
            model,
            async () => 
            {
                await Task.Delay(100); // Simulate API call
                return new CommitMessageResult
                {
                    Message = "Test response",
                    InputTokens = 10,
                    OutputTokens = 20,
                    TotalTokens = 30
                };
            },
            indent);
        
        // Assert
        var output = capturedOutput.ToString();
        _output.WriteLine("Captured output:");
        _output.WriteLine(output);
        
        // Verify that the status line (containing 📊) is properly indented
        output.Should().Contain($"{indent}📊", "the token usage line should be indented");
        
        // Verify the format includes tokens and cost
        output.Should().Contain("10 → 20 tokens (30 total)");
        output.Should().Contain("~$");
        
        // All lines should start with the indent
        var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                line.Should().StartWith(indent, $"line '{line}' should be indented");
            }
        }
    }
    
    [Fact]
    public async Task LlmCallTracker_WithoutIndent_ShouldNotIndentOutput()
    {
        // Arrange
        var capturedOutput = new StringBuilder();
        var logger = new TestLogger(capturedOutput);
        var usageTracking = Substitute.For<IUsageTrackingService>();
        usageTracking.GetSessionId().Returns("test-session-id");
        var tracker = new LlmCallTracker(logger, usageTracking);
        
        var model = new ModelConfiguration
        {
            Name = "Test Model",
            ModelId = "test-model",
            Type = "openai-compatible"
        };
        
        // Act - Test without indentation
        await tracker.TrackCallAsync(
            "Testing connection",
            "test prompt", 
            model,
            async () =>
            {
                await Task.Delay(50);
                return new CommitMessageResult
                {
                    Message = "Test response",
                    InputTokens = 5,
                    OutputTokens = 10,
                    TotalTokens = 15
                };
            });
        
        // Assert
        var output = capturedOutput.ToString();
        _output.WriteLine("Captured output:");
        _output.WriteLine(output);
        
        // Verify the status line is not indented
        output.Should().Contain("📊 5 → 10 tokens (15 total)");
        output.Should().NotContain("  📊", "the token usage line should not be indented");
    }
    
    private class TestLogger : IConsoleLogger
    {
        private readonly StringBuilder _output;
        
        public TestLogger(StringBuilder output)
        {
            _output = output;
        }
        
        public void Debug(string message, params object[] args)
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _output.AppendLine(formatted);
        }
        
        public void Error(string message, params object[] args)
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _output.AppendLine(formatted);
        }
        
        public void Error(Exception ex, string message, params object[] args)
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _output.AppendLine($"{formatted}: {ex.Message}");
        }
        
        public void Highlight(string message, ConsoleColor color)
        {
            _output.AppendLine(message);
        }
        
        public void Information(string message, params object[] args)
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _output.AppendLine(formatted);
        }
        
        public void Muted(string message, params object[] args)
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _output.AppendLine(formatted);
        }
        
        public void Success(string message, params object[] args)
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _output.AppendLine(formatted);
        }
        
        public void Warning(string message, params object[] args)
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _output.AppendLine(formatted);
        }
    }
}