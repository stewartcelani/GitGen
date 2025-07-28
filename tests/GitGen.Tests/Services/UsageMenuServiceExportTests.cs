using FluentAssertions;
using GitGen.Configuration;
using GitGen.Models;
using GitGen.Services;
using GitGen.Tests.Helpers;
using Moq;
using System.Text.Json;
using Xunit;

namespace GitGen.Tests.Services;

public class UsageMenuServiceExportTests : IDisposable
{
    private readonly Mock<IUsageReportingService> _reportingServiceMock;
    private readonly Mock<ISecureConfigurationService> _secureConfigMock;
    private readonly TestConsoleLogger _logger;
    private readonly UsageMenuService _service;
    private readonly string _testDirectory;
    
    public UsageMenuServiceExportTests()
    {
        _reportingServiceMock = new Mock<IUsageReportingService>();
        _secureConfigMock = new Mock<ISecureConfigurationService>();
        _logger = new TestConsoleLogger();
        
        _service = new UsageMenuService(
            _logger,
            _reportingServiceMock.Object,
            _secureConfigMock.Object);
            
        _testDirectory = Path.Combine(Path.GetTempPath(), $"gitgen-export-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        Directory.SetCurrentDirectory(_testDirectory);
    }
    
    public void Dispose()
    {
        // Reset to original directory
        Directory.SetCurrentDirectory(Path.GetTempPath());
        
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
    
    #region Export Navigation Tests
    
    [Fact]
    public async Task ExportReports_CancelSelection_ReturnsToMenu()
    {
        // Arrange
        using var console = new ConsoleTestHelper("8\n0\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("Export Reports").Should().BeTrue();
        // Should not attempt any exports
        _reportingServiceMock.Verify(x => x.GenerateCustomReportAsync(
            It.IsAny<DateTime>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<string>(), 
            It.IsAny<bool>()), 
            Times.Never);
    }
    
    #endregion
    
    #region JSON Export Tests
    
    [Fact]
    public async Task ExportAsJson_CreatesFileWithCorrectName()
    {
        // Arrange
        var startDate = new DateTime(2025, 7, 1);
        var endDate = new DateTime(2025, 7, 31);
        using var console = new ConsoleTestHelper($"8\n1\n{startDate:yyyy-MM-dd}\n{endDate:yyyy-MM-dd}\n\n0\n0");
        
        var entry = new UsageEntryBuilder().Build();
        SetupMockData(new[] { entry });
        
        _reportingServiceMock
            .Setup(x => x.GenerateCustomReportAsync(
                It.IsAny<DateTime>(), 
                It.IsAny<DateTime>(), 
                null, 
                true))
            .ReturnsAsync("{ \"test\": true }");
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var expectedFileName = $"gitgen-usage-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.json";
        _logger.HasMessageOfLevel(LogLevel.Success, $"Exported to {expectedFileName}").Should().BeTrue();
        
        var filePath = Path.Combine(_testDirectory, expectedFileName);
        File.Exists(filePath).Should().BeTrue();
        
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Be("{ \"test\": true }");
    }
    
    [Fact]
    public async Task ExportAsJson_UsesReportingService()
    {
        // Arrange
        var startDate = new DateTime(2025, 7, 1);
        var endDate = new DateTime(2025, 7, 31);
        using var console = new ConsoleTestHelper($"8\n1\n{startDate:yyyy-MM-dd}\n{endDate:yyyy-MM-dd}\n\n0\n0");
        
        SetupMockData(new List<UsageEntry>());
        _reportingServiceMock
            .Setup(x => x.GenerateCustomReportAsync(startDate, endDate, null, true))
            .ReturnsAsync("{}");
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _reportingServiceMock.Verify(x => x.GenerateCustomReportAsync(
            startDate, endDate, null, true), 
            Times.Once);
    }
    
    #endregion
    
    #region CSV Export Tests
    
    [Fact]
    public async Task ExportAsCsv_CreatesValidCsvFile()
    {
        // Arrange
        using var console = new ConsoleTestHelper("8\n2\n\n\n\n0\n0");
        
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithTimestamp(new DateTime(2025, 7, 15, 10, 30, 0))
                .WithSessionId("session-123")
                .WithModel("gpt-4", "openai")
                .WithTokens(1000, 500)
                .WithCost(0.045m, "USD")
                .WithDuration(2.5)
                .WithProject("/test/project", "main")
                .Build()
        };
        
        SetupMockData(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.csv");
        files.Should().HaveCount(1);
        
        var content = await File.ReadAllTextAsync(files[0]);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        
        // Check header
        lines[0].Should().Be("Timestamp,SessionId,Model,Provider,InputTokens,OutputTokens,TotalTokens,Duration,Cost,Currency,Success,Project,Branch");
        
        // Check data
        lines[1].Should().Contain("2025-07-15 10:30:00");
        lines[1].Should().Contain("session-123");
        lines[1].Should().Contain("gpt-4");
        lines[1].Should().Contain("openai");
        lines[1].Should().Contain("1000");
        lines[1].Should().Contain("500");
        lines[1].Should().Contain("1500");
        lines[1].Should().Contain("2.5");
        lines[1].Should().Contain("0.045");
        lines[1].Should().Contain("USD");
        lines[1].Should().Contain("True");
        lines[1].Should().Contain("/test/project");
        lines[1].Should().Contain("main");
    }
    
    [Fact]
    public async Task ExportAsCsv_HandlesNullCost()
    {
        // Arrange
        using var console = new ConsoleTestHelper("8\n2\n\n\n\n0\n0");
        
        var entry = new UsageEntryBuilder().Build();
        entry.Cost = null;
        
        SetupMockData(new[] { entry });
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.csv");
        var content = await File.ReadAllTextAsync(files[0]);
        
        content.Should().Contain(",0,USD,"); // Default values for null cost
    }
    
    #endregion
    
    #region Markdown Export Tests
    
    [Fact]
    public async Task ExportAsMarkdown_CreatesValidMarkdownFile()
    {
        // Arrange
        using var console = new ConsoleTestHelper("8\n3\n\n\n\n0\n0");
        
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithModel("gpt-4")
                .WithTokens(2000, 1000)
                .WithCost(0.090m)
                .WithDuration(3.5)
                .Build(),
            new UsageEntryBuilder()
                .WithModel("claude-3")
                .WithTokens(1500, 750)
                .WithCost(0.060m)
                .WithDuration(2.0)
                .Build()
        };
        
        SetupMockData(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.md");
        files.Should().HaveCount(1);
        
        var content = await File.ReadAllTextAsync(files[0]);
        
        // Check markdown structure
        content.Should().Contain("# GitGen Usage Report");
        content.Should().Contain("**Period**:");
        content.Should().Contain("## Summary");
        content.Should().Contain("- **Total Calls**: 2");
        content.Should().Contain("- **Total Cost**: $0.15");
        content.Should().Contain("- **Average Response Time**: 2.8s");
        content.Should().Contain("## Usage by Model");
        content.Should().Contain("| Model | Calls | Input Tokens | Output Tokens | Total Tokens | Avg Time | Cost |");
        content.Should().Contain("|-------|-------|--------------|---------------|--------------|----------|------|");
        content.Should().Contain("| gpt-4");
        content.Should().Contain("| claude-3");
    }
    
    [Fact]
    public async Task ExportAsMarkdown_FormatsTokensCorrectly()
    {
        // Arrange
        using var console = new ConsoleTestHelper("8\n3\n\n\n\n0\n0");
        
        var entry = new UsageEntryBuilder()
            .WithTokens(1500000, 500000)
            .Build();
        
        SetupMockData(new[] { entry });
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var files = Directory.GetFiles(_testDirectory, "*.md");
        var content = await File.ReadAllTextAsync(files[0]);
        
        content.Should().Contain("| 1.5M |"); // Input tokens
        content.Should().Contain("| 500.0k |"); // Output tokens
        content.Should().Contain("| 2.0M |"); // Total tokens
    }
    
    #endregion
    
    #region Date Input Tests
    
    [Fact]
    public async Task Export_DefaultDates_UsesLast30DaysToToday()
    {
        // Arrange
        using var console = new ConsoleTestHelper("8\n1\n\n\n\n0\n0");
        
        DateTime capturedStart = default;
        DateTime capturedEnd = default;
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<DateTime, DateTime>((start, end) =>
            {
                capturedStart = start;
                capturedEnd = end;
            })
            .ReturnsAsync(new List<UsageEntry>());
            
        _reportingServiceMock
            .Setup(x => x.GenerateCustomReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, true))
            .ReturnsAsync("{}");
        
        // Act
        await _service.RunAsync();
        
        // Assert
        capturedStart.Date.Should().Be(DateTime.Today.AddDays(-30).Date);
        capturedEnd.Date.Should().Be(DateTime.Today.Date);
    }
    
    #endregion
    
    #region Helper Methods
    
    private void SetupMockData(IEnumerable<UsageEntry> entries)
    {
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(entries);
    }
    
    #endregion
}