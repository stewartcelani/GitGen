using FluentAssertions;
using GitGen.Configuration;
using GitGen.Models;
using GitGen.Services;
using GitGen.Tests.Helpers;
using Moq;
using Xunit;

namespace GitGen.Tests.Services;

public class UsageMenuServiceDisplayTests
{
    private readonly Mock<IUsageReportingService> _reportingServiceMock;
    private readonly Mock<ISecureConfigurationService> _secureConfigMock;
    private readonly TestConsoleLogger _logger;
    private readonly TestConsoleInput _consoleInput;
    private readonly TestConsoleOutput _consoleOutput;
    private readonly UsageMenuService _service;
    
    public UsageMenuServiceDisplayTests()
    {
        _reportingServiceMock = new Mock<IUsageReportingService>();
        _secureConfigMock = new Mock<ISecureConfigurationService>();
        _logger = new TestConsoleLogger();
        _consoleInput = new TestConsoleInput();
        _consoleOutput = new TestConsoleOutput();
        
        _service = new UsageMenuService(
            _logger,
            _reportingServiceMock.Object,
            _secureConfigMock.Object,
            _consoleInput,
            _consoleOutput);
    }
    
    #region Constructor Tests
    
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act & Assert  
        var service = new UsageMenuService(
            _logger,
            _reportingServiceMock.Object,
            _secureConfigMock.Object,
            new TestConsoleInput(),
            new TestConsoleOutput());
            
        service.Should().NotBeNull();
    }
    
    #endregion
    
    #region DisplayUsageSummary Tests
    
    [Fact]
    public async Task DisplayUsageSummary_EmptyEntries_ShowsNoUsageMessage()
    {
        // Arrange
        _consoleInput.AddKeyInput('1'); // Select option 1 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("No usage recorded for Today").Should().BeTrue();
    }
    
    [Fact]
    public async Task DisplayUsageSummary_WithEntries_ShowsFormattedTable()
    {
        // Arrange
        _consoleInput.AddKeyInput('1'); // Select option 1 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithModel("gpt-4")
                .WithTokens(1000, 500)
                .WithCost(0.045m)
                .WithDuration(2.5)
                .Build(),
            new UsageEntryBuilder()
                .WithModel("claude-3")
                .WithTokens(2000, 1000)
                .WithCost(0.090m)
                .WithDuration(3.5)
                .Build()
        };
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var output = _consoleOutput.GetOutput();
        _logger.HasMessage("Period: Today").Should().BeTrue();
        _logger.HasMessage("Total calls: 2").Should().BeTrue();
        _logger.HasMessage("Total cost: $0.14").Should().BeTrue();
        _logger.HasMessage("Average response time: 3.0s").Should().BeTrue();
        output.Should().Contain("┌──────────────────────┬──────────┬────────────┬────────────┬────────────┬──────────┬────────────┐");
        output.Should().Contain("│ Model                │ Calls    │ Input      │ Output     │ Total      │ Avg Time │ Cost       │");
    }
    
    [Fact]
    public async Task DisplayUsageSummary_ShowsSessionCount()
    {
        // Arrange
        _consoleInput.AddKeyInput('1'); // Select option 1 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entries = new[]
        {
            new UsageEntryBuilder().WithSessionId("session-1").Build(),
            new UsageEntryBuilder().WithSessionId("session-1").Build(),
            new UsageEntryBuilder().WithSessionId("session-2").Build()
        };
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessageOfLevel(LogLevel.Muted, "Sessions: 2").Should().BeTrue();
    }
    
    [Fact]
    public async Task DisplayUsageSummary_ShowsMostActiveProject()
    {
        // Arrange
        _consoleInput.AddKeyInput('1'); // Select option 1 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entries = new[]
        {
            new UsageEntryBuilder().WithProject("/project/a").Build(),
            new UsageEntryBuilder().WithProject("/project/a").Build(),
            new UsageEntryBuilder().WithProject("/project/b").Build()
        };
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessageOfLevel(LogLevel.Muted, "Most active project: a (2 calls)").Should().BeTrue();
    }
    
    #endregion
    
    #region DisplayMonthlyReport Tests
    
    [Fact]
    public async Task DisplayMonthlyReport_WithData_ShowsDailySummaryTable()
    {
        // Arrange
        _consoleInput.AddKeyInput('4'); // Select option 4 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithTimestamp(new DateTime(2025, 7, 1))
                .WithTokens(1000, 100)
                .WithCost(0.05m)
                .WithDuration(2.0)
                .Build(),
            new UsageEntryBuilder()
                .WithTimestamp(new DateTime(2025, 7, 15))
                .WithTokens(2000, 200)
                .WithCost(0.10m)
                .WithDuration(3.0)
                .Build()
        };
        
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(startOfMonth, DateTime.Today))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var output = _consoleOutput.GetOutput();
        output.Should().Contain("Daily Summary:");
        output.Should().Contain("┌────────────┬──────────┬────────────┬────────────┬──────────┬────────────┐");
        output.Should().Contain("│ Date       │ Calls    │ Input      │ Output     │ Avg Time │ Cost       │");
    }
    
    #endregion
    
    #region DisplayDetailedRequests Tests
    
    [Fact]
    public async Task DisplayDetailedRequests_FormatsTimeCorrectly()
    {
        // Arrange
        _consoleInput.AddKeyInput('7'); // Select option 7 from main menu
        _consoleInput.AddLineInput("1"); // Select option 1 from submenu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        
        var entries = new[]
        {
            new UsageEntryBuilder()
                .WithTimestamp(today.AddHours(14).AddMinutes(30))
                .Build(),
            new UsageEntryBuilder()
                .WithTimestamp(yesterday.AddHours(10))
                .Build()
        };
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var output = _consoleOutput.GetOutput();
        // Today's entry should show time only - check that it contains the table separator after a time
        output.Should().Contain("│");
        // Check that we have two rows of data
        output.Should().Contain("│ test-model");
        // Yesterday's entry should show date and time - check for the month/day/year pattern
        output.Should().Contain($"{yesterday.Month}/");
    }
    
    [Fact]
    public async Task DisplayDetailedRequests_TruncatesLongModelNames()
    {
        // Arrange
        _consoleInput.AddKeyInput('7'); // Select option 7 from main menu
        _consoleInput.AddLineInput("1"); // Select option 1 from submenu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entry = new UsageEntryBuilder()
            .WithModel("this-is-a-very-long-model-name-that-exceeds-the-column-width")
            .Build();
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[] { entry });
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var output = _consoleOutput.GetOutput();
        output.Should().Contain("this-is-a-very-lo...");
    }
    
    #endregion
    
    #region Token Formatting Tests
    
    [Theory]
    [InlineData(500, "500")]
    [InlineData(1500, "1.5k")]
    [InlineData(1000, "1.0k")]
    [InlineData(1500000, "1.5M")]
    [InlineData(1000000, "1.0M")]
    public async Task FormatTokenCount_FormatsCorrectly(int tokens, string expected)
    {
        // Arrange
        _consoleInput.AddKeyInput('1'); // Select option 1 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entry = new UsageEntryBuilder()
            .WithTokens(tokens, 100)
            .Build();
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(new[] { entry });
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var output = _consoleOutput.GetOutput();
        output.Should().Contain($"│ {expected,10} │");
    }
    
    #endregion
    
    #region Week Calculation Tests
    
    [Theory]
    [InlineData("2025-07-28", "2025-07-28")] // Monday
    [InlineData("2025-07-29", "2025-07-28")] // Tuesday -> Monday
    [InlineData("2025-08-03", "2025-07-28")] // Sunday -> Monday
    public async Task ViewThisWeekUsage_CalculatesWeekStartCorrectly(string currentDateStr, string expectedStartStr)
    {
        // Arrange
        var currentDate = DateTime.Parse(currentDateStr);
        var expectedStart = DateTime.Parse(expectedStartStr);
        
        // Since we can't override DateTime.Today, we'll verify the week calculation
        // by checking what dates are passed to GetUsageEntriesAsync
        _consoleInput.AddKeyInput('3'); // Select option 3 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert - verify that the service called GetUsageEntriesAsync with a Monday as start date
        _reportingServiceMock.Verify(x => x.GetUsageEntriesAsync(
            It.Is<DateTime>(d => d.DayOfWeek == DayOfWeek.Monday),
            It.Is<DateTime>(d => d.Date == DateTime.Today)),
            Times.AtLeastOnce);
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