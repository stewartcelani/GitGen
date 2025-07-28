using System.Text.Json;
using FluentAssertions;
using GitGen.Models;
using GitGen.Services;
using GitGen.Tests.Helpers;
using Moq;
using Xunit;

namespace GitGen.Tests.Services;

public class UsageReportingServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestConsoleLogger _logger;
    private readonly UsageReportingService _service;
    
    public UsageReportingServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"gitgen-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(Path.Combine(_testDirectory, ".gitgen", "usage"));
        
        _logger = new TestConsoleLogger();
        
        // Override home directory for testing
        Environment.SetEnvironmentVariable("HOME", _testDirectory);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        _service = new UsageReportingService(_logger);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
    
    #region GetUsageEntriesAsync Tests
    
    [Fact]
    public async Task GetUsageEntriesAsync_NoUsageDirectory_ReturnsEmptyCollection()
    {
        // Arrange
        var usageDir = Path.Combine(_testDirectory, ".gitgen", "usage");
        if (Directory.Exists(usageDir))
        {
            Directory.Delete(usageDir, true);
        }
        
        // Act
        var entries = await _service.GetUsageEntriesAsync(DateTime.Today, DateTime.Today);
        
        // Assert
        entries.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetUsageEntriesAsync_NoMatchingFiles_ReturnsEmptyCollection()
    {
        // Act
        var entries = await _service.GetUsageEntriesAsync(
            new DateTime(2020, 1, 1), 
            new DateTime(2020, 12, 31));
        
        // Assert
        entries.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetUsageEntriesAsync_WithValidData_ReturnsFilteredEntries()
    {
        // Arrange
        var testDate = new DateTime(2025, 7, 15);
        var entries = new[]
        {
            new UsageEntryBuilder().WithTimestamp(testDate).Build(),
            new UsageEntryBuilder().WithTimestamp(testDate.AddDays(1)).Build(),
            new UsageEntryBuilder().WithTimestamp(testDate.AddDays(2)).Build()
        };
        
        await WriteTestEntries(testDate.Year, testDate.Month, entries);
        
        // Act
        var result = await _service.GetUsageEntriesAsync(testDate, testDate.AddDays(1));
        
        // Assert
        result.Should().HaveCount(2);
        result.All(e => e.Timestamp.Date >= testDate.Date && e.Timestamp.Date <= testDate.AddDays(1).Date)
            .Should().BeTrue();
    }
    
    [Fact]
    public async Task GetUsageEntriesAsync_SpansMultipleMonths_ReadsMultipleFiles()
    {
        // Arrange
        var entries1 = new UsageEntryBuilder().WithTimestamp(new DateTime(2025, 6, 30)).Build();
        var entries2 = new UsageEntryBuilder().WithTimestamp(new DateTime(2025, 7, 1)).Build();
        
        await WriteTestEntries(2025, 6, new[] { entries1 });
        await WriteTestEntries(2025, 7, new[] { entries2 });
        
        // Act
        var result = await _service.GetUsageEntriesAsync(
            new DateTime(2025, 6, 30), 
            new DateTime(2025, 7, 1));
        
        // Assert
        result.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task GetUsageEntriesAsync_WithInvalidJson_SkipsInvalidLines()
    {
        // Arrange
        var validEntry = new UsageEntryBuilder().WithTimestamp(DateTime.Today).Build();
        var fileName = $"usage-{DateTime.Today:yyyy-MM}.jsonl";
        var filePath = Path.Combine(_testDirectory, ".gitgen", "usage", fileName);
        
        var lines = new[]
        {
            JsonSerializer.Serialize(validEntry, UsageJsonContext.Default.UsageEntry),
            "{ invalid json",
            "", // empty line
            JsonSerializer.Serialize(validEntry, UsageJsonContext.Default.UsageEntry)
        };
        
        await File.WriteAllLinesAsync(filePath, lines);
        
        // Act
        var result = await _service.GetUsageEntriesAsync(DateTime.Today, DateTime.Today);
        
        // Assert
        result.Should().HaveCount(2);
        _logger.HasMessageOfLevel(LogLevel.Debug, "Skipping invalid JSON line").Should().BeTrue();
    }
    
    [Fact]
    public async Task GetUsageEntriesAsync_ReturnsEntriesOrderedByTimestamp()
    {
        // Arrange
        var baseDate = DateTime.Today;
        var entries = new[]
        {
            new UsageEntryBuilder().WithTimestamp(baseDate.AddHours(3)).Build(),
            new UsageEntryBuilder().WithTimestamp(baseDate.AddHours(1)).Build(),
            new UsageEntryBuilder().WithTimestamp(baseDate.AddHours(2)).Build()
        };
        
        await WriteTestEntries(baseDate.Year, baseDate.Month, entries);
        
        // Act
        var result = (await _service.GetUsageEntriesAsync(baseDate, baseDate)).ToList();
        
        // Assert
        result.Should().HaveCount(3);
        result[0].Timestamp.Should().Be(baseDate.AddHours(1));
        result[1].Timestamp.Should().Be(baseDate.AddHours(2));
        result[2].Timestamp.Should().Be(baseDate.AddHours(3));
    }
    
    #endregion
    
    #region GenerateDailyReportAsync Tests
    
    [Fact]
    public async Task GenerateDailyReportAsync_NoData_ReturnsNoUsageMessage()
    {
        // Act
        var report = await _service.GenerateDailyReportAsync(new DateTime(2025, 1, 1));
        
        // Assert
        report.Should().Contain("GitGen Usage Report - 2025-01-01");
        report.Should().Contain("No usage recorded for this date.");
    }
    
    [Fact]
    public async Task GenerateDailyReportAsync_WithData_GeneratesFormattedTable()
    {
        // Arrange
        var testDate = DateTime.Today;
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithTimestamp(testDate)
                .WithModel("gpt-4", "openai")
                .WithTokens(1000, 500)
                .WithCost(0.045m)
                .WithDuration(2.5)
                .Build(),
            new UsageEntryBuilder()
                .WithTimestamp(testDate)
                .WithModel("gpt-4", "openai")
                .WithTokens(2000, 1000)
                .WithCost(0.090m)
                .WithDuration(3.5)
                .Build(),
            new UsageEntryBuilder()
                .WithTimestamp(testDate)
                .WithModel("claude-3", "anthropic")
                .WithTokens(1500, 750)
                .WithCost(0.060m)
                .WithDuration(2.0)
                .Build()
        };
        
        await WriteTestEntries(testDate.Year, testDate.Month, entries);
        
        // Act
        var report = await _service.GenerateDailyReportAsync(testDate);
        
        // Assert
        report.Should().Contain("Usage by Model:");
        report.Should().Contain("┌──────────────────────┬──────────┬────────────┬────────────┬────────────┬──────────┬────────────┐");
        report.Should().Contain("│ Model                │ Calls    │ Input      │ Output     │ Total      │ Avg Time │ Cost       │");
        report.Should().Contain("├──────────────────────┼──────────┼────────────┼────────────┼────────────┼──────────┼────────────┤");
        report.Should().Contain("│ gpt-4");
        report.Should().Contain("│ claude-3");
        report.Should().Contain("│ TOTAL");
        report.Should().Contain("Sessions: 1");
    }
    
    [Fact]
    public async Task GenerateDailyReportAsync_WithLongModelName_TruncatesName()
    {
        // Arrange
        var testDate = DateTime.Today;
        var entry = new UsageEntryBuilder()
            .WithTimestamp(testDate)
            .WithModel("this-is-a-very-long-model-name-that-exceeds-limit")
            .Build();
        
        await WriteTestEntries(testDate.Year, testDate.Month, new[] { entry });
        
        // Act
        var report = await _service.GenerateDailyReportAsync(testDate);
        
        // Assert
        report.Should().Contain("this-is-a-very-lon...");
    }
    
    [Fact]
    public async Task GenerateDailyReportAsync_FormatsTokenCounts()
    {
        // Arrange
        var testDate = DateTime.Today;
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithTimestamp(testDate)
                .WithTokens(1500, 500)
                .Build(),
            new UsageEntryBuilder()
                .WithTimestamp(testDate)
                .WithTokens(1000000, 500000)
                .Build()
        };
        
        await WriteTestEntries(testDate.Year, testDate.Month, entries);
        
        // Act
        var report = await _service.GenerateDailyReportAsync(testDate);
        
        // Assert
        report.Should().Contain("1.5k");  // 1500 formatted as 1.5k
        report.Should().Contain("1.0M");  // 1000000 formatted as 1.0M
    }
    
    [Fact]
    public async Task GenerateDailyReportAsync_DefaultsToToday()
    {
        // Act
        var report = await _service.GenerateDailyReportAsync();
        
        // Assert
        report.Should().Contain($"GitGen Usage Report - {DateTime.Today:yyyy-MM-dd}");
    }
    
    #endregion
    
    #region GenerateMonthlyReportAsync Tests
    
    [Fact]
    public async Task GenerateMonthlyReportAsync_NoData_ReturnsNoUsageMessage()
    {
        // Act
        var report = await _service.GenerateMonthlyReportAsync(2025, 1);
        
        // Assert
        report.Should().Contain("GitGen Usage Report - January 2025");
        report.Should().Contain("No usage recorded for this month.");
    }
    
    [Fact]
    public async Task GenerateMonthlyReportAsync_WithData_ShowsDailySummary()
    {
        // Arrange
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithTimestamp(new DateTime(2025, 7, 1))
                .WithModel("gpt-4")
                .WithCost(0.10m)
                .Build(),
            new UsageEntryBuilder()
                .WithTimestamp(new DateTime(2025, 7, 1))
                .WithModel("gpt-4")
                .WithCost(0.15m)
                .Build(),
            new UsageEntryBuilder()
                .WithTimestamp(new DateTime(2025, 7, 15))
                .WithModel("claude-3")
                .WithCost(0.20m)
                .Build()
        };
        
        await WriteTestEntries(2025, 7, entries);
        
        // Act
        var report = await _service.GenerateMonthlyReportAsync(2025, 7);
        
        // Assert
        report.Should().Contain("Daily Usage:");
        report.Should().Contain("│ Date       │ Model                │ Calls");
        report.Should().Contain("│ 2025-07-01  │ gpt-4");
        report.Should().Contain("│ 2025-07-15  │ claude-3");
        report.Should().Contain("Monthly Summary:");
        report.Should().Contain("Total calls: 3");
        report.Should().Contain("Top Models by Usage:");
    }
    
    [Fact]
    public async Task GenerateMonthlyReportAsync_DefaultsToCurrentMonth()
    {
        // Act
        var report = await _service.GenerateMonthlyReportAsync();
        
        // Assert
        var currentMonth = DateTime.Today.ToString("MMMM yyyy");
        report.Should().Contain($"GitGen Usage Report - {currentMonth}");
    }
    
    #endregion
    
    #region GenerateCustomReportAsync Tests
    
    [Fact]
    public async Task GenerateCustomReportAsync_WithModelFilter_FiltersResults()
    {
        // Arrange
        var entries = new[]
        {
            new UsageEntryBuilder().WithModel("gpt-4").Build(),
            new UsageEntryBuilder().WithModel("gpt-3.5-turbo").Build(),
            new UsageEntryBuilder().WithModel("claude-3").Build()
        };
        
        await WriteTestEntries(DateTime.Today.Year, DateTime.Today.Month, entries);
        
        // Act
        var report = await _service.GenerateCustomReportAsync(
            DateTime.Today, DateTime.Today, "gpt");
        
        // Assert
        report.Should().Contain("Filter: Model contains 'gpt'");
        report.Should().Contain("gpt-4");
        report.Should().Contain("gpt-3.5-turbo");
        report.Should().NotContain("claude-3");
    }
    
    [Fact]
    public async Task GenerateCustomReportAsync_JsonOutput_ReturnsValidJson()
    {
        // Arrange
        var entry = new UsageEntryBuilder()
            .WithTokens(1000, 500)
            .WithCost(0.05m)
            .Build();
        
        await WriteTestEntries(DateTime.Today.Year, DateTime.Today.Month, new[] { entry });
        
        // Act
        var json = await _service.GenerateCustomReportAsync(
            DateTime.Today, DateTime.Today, null, true);
        
        // Assert
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("TotalCalls").GetInt32().Should().Be(1);
        parsed.RootElement.GetProperty("TotalInputTokens").GetInt32().Should().Be(1000);
        parsed.RootElement.GetProperty("TotalOutputTokens").GetInt32().Should().Be(500);
        parsed.RootElement.GetProperty("TotalCost").GetDecimal().Should().Be(0.05m);
    }
    
    [Fact]
    public async Task GenerateCustomReportAsync_CaseInsensitiveFilter()
    {
        // Arrange
        var entry = new UsageEntryBuilder().WithModel("GPT-4").Build();
        await WriteTestEntries(DateTime.Today.Year, DateTime.Today.Month, new[] { entry });
        
        // Act
        var report = await _service.GenerateCustomReportAsync(
            DateTime.Today, DateTime.Today, "gpt");
        
        // Assert
        report.Should().Contain("GPT-4");
    }
    
    #endregion
    
    #region Helper Methods
    
    private async Task WriteTestEntries(int year, int month, IEnumerable<UsageEntry> entries)
    {
        var fileName = $"usage-{year:0000}-{month:00}.jsonl";
        var filePath = Path.Combine(_testDirectory, ".gitgen", "usage", fileName);
        
        var lines = entries.Select(e => 
            JsonSerializer.Serialize(e, UsageJsonContext.Default.UsageEntry));
        
        await File.WriteAllLinesAsync(filePath, lines);
    }
    
    #endregion
}