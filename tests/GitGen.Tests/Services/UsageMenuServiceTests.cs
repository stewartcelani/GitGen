using FluentAssertions;
using GitGen.Configuration;
using GitGen.Models;
using GitGen.Services;
using GitGen.Tests.Helpers;
using Moq;
using Xunit;

namespace GitGen.Tests.Services;

public class UsageMenuServiceTests
{
    private readonly Mock<IUsageReportingService> _reportingServiceMock;
    private readonly Mock<ISecureConfigurationService> _secureConfigMock;
    private readonly TestConsoleLogger _logger;
    private readonly UsageMenuService _service;
    
    public UsageMenuServiceTests()
    {
        _reportingServiceMock = new Mock<IUsageReportingService>();
        _secureConfigMock = new Mock<ISecureConfigurationService>();
        _logger = new TestConsoleLogger();
        
        _service = new UsageMenuService(
            _logger,
            _reportingServiceMock.Object,
            _secureConfigMock.Object);
    }
    
    #region Constructor Tests
    
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act & Assert
        var service = new UsageMenuService(
            _logger,
            _reportingServiceMock.Object,
            _secureConfigMock.Object);
            
        service.Should().NotBeNull();
    }
    
    #endregion
    
    #region Main Menu Navigation Tests
    
    [Fact]
    public async Task RunAsync_SelectingExit_ReturnsImmediately()
    {
        // Arrange
        using var console = new ConsoleTestHelper("0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("GitGen Usage Statistics").Should().BeTrue();
        _reportingServiceMock.Verify(x => x.GetUsageEntriesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Exactly(2)); // For quick stats
    }
    
    [Fact]
    public async Task RunAsync_EmptyInput_ExitsMenu()
    {
        // Arrange
        using var console = new ConsoleTestHelper("");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("GitGen Usage Statistics").Should().BeTrue();
    }
    
    [Fact]
    public async Task RunAsync_InvalidChoice_ShowsWarning()
    {
        // Arrange
        using var console = new ConsoleTestHelper("99\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessageOfLevel(LogLevel.Warning, "Invalid choice").Should().BeTrue();
    }
    
    [Theory]
    [InlineData("1", "Today's Usage")]
    [InlineData("2", "Yesterday's Usage")]
    [InlineData("3", "This Week's Usage")]
    [InlineData("4", "This Month's Usage")]
    [InlineData("5", "Last Month's Usage")]
    public async Task RunAsync_SelectingOption_NavigatesToCorrectView(string choice, string expectedTitle)
    {
        // Arrange
        using var console = new ConsoleTestHelper($"{choice}\n\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage(expectedTitle).Should().BeTrue();
    }
    
    #endregion
    
    #region Quick Stats Tests
    
    [Fact]
    public async Task DisplayMainMenu_ShowsQuickStats()
    {
        // Arrange
        using var console = new ConsoleTestHelper("0");
        var todayEntries = UsageEntryBuilder.CreateMultiple(3, (builder, i) =>
        {
            builder.WithTimestamp(DateTime.Today)
                   .WithCost(0.01m * (i + 1));
        });
        
        var monthEntries = UsageEntryBuilder.CreateMultiple(10, (builder, i) =>
        {
            builder.WithTimestamp(DateTime.Today.AddDays(-i))
                   .WithCost(0.05m);
        });
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(todayEntries);
            
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.Is<DateTime>(d => d.Day == 1), 
                DateTime.Today))
            .ReturnsAsync(monthEntries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("Today: 3 calls, $0.06").Should().BeTrue();
        _logger.HasMessage("Month: 10 calls, $0.50").Should().BeTrue();
    }
    
    #endregion
    
    #region Today's Usage Tests
    
    [Fact]
    public async Task ViewTodayUsage_WithData_DisplaysSummary()
    {
        // Arrange
        using var console = new ConsoleTestHelper("1\n\n0");
        var entries = CreateTestEntries(DateTime.Today, 5);
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var output = console.GetOutput();
        output.Should().Contain("Today's Usage");
        output.Should().Contain("Period: Today");
        output.Should().Contain("Total calls: 5");
    }
    
    [Fact]
    public async Task ViewTodayUsage_NoData_ShowsNoUsageMessage()
    {
        // Arrange
        using var console = new ConsoleTestHelper("1\n\n0");
        SetupMockData(new List<UsageEntry>(), DateTime.Today, DateTime.Today);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("No usage recorded for Today").Should().BeTrue();
    }
    
    #endregion
    
    #region Custom Date Range Tests
    
    [Fact]
    public async Task ViewCustomDateRange_DefaultDates_Uses30DayRange()
    {
        // Arrange
        using var console = new ConsoleTestHelper("6\n\n\n\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var expectedStart = DateTime.Today.AddDays(-30);
        _reportingServiceMock.Verify(x => x.GetUsageEntriesAsync(
            It.Is<DateTime>(d => d.Date == expectedStart.Date),
            It.Is<DateTime>(d => d.Date == DateTime.Today.Date)), 
            Times.AtLeastOnce);
    }
    
    [Fact]
    public async Task ViewCustomDateRange_InvalidStartDate_PromptsAgain()
    {
        // Arrange
        using var console = new ConsoleTestHelper("6\ninvalid\n2025-01-01\n2025-01-31\n\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessageOfLevel(LogLevel.Warning, "Invalid date format").Should().BeTrue();
    }
    
    [Fact]
    public async Task ViewCustomDateRange_EndDateBeforeStart_ShowsWarning()
    {
        // Arrange
        using var console = new ConsoleTestHelper("6\n2025-01-31\n2025-01-01\n2025-02-01\n\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessageOfLevel(LogLevel.Warning, "End date must be after start date").Should().BeTrue();
    }
    
    #endregion
    
    #region Detailed Request History Tests
    
    [Fact]
    public async Task ViewDetailedRequests_ShowsSubmenu()
    {
        // Arrange
        using var console = new ConsoleTestHelper("7\n0\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("Detailed Request History").Should().BeTrue();
        _logger.HasMessage("1. Last 10 requests").Should().BeTrue();
        _logger.HasMessage("2. Today's requests").Should().BeTrue();
        _logger.HasMessage("3. Filter by model").Should().BeTrue();
        _logger.HasMessage("4. Filter by date").Should().BeTrue();
    }
    
    [Fact]
    public async Task ShowLast10Requests_WithData_DisplaysDetailedTable()
    {
        // Arrange
        using var console = new ConsoleTestHelper("7\n1\n\n0\n0");
        var entries = CreateTestEntries(DateTime.Today, 15);
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.IsAny<DateTime>(), 
                It.IsAny<DateTime>()))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        var output = console.GetOutput();
        output.Should().Contain("Last 10 Requests");
        output.Should().Contain("│ Time           │ Model                │ Input    │ Output   │ Total    │ Time   │ Cost       │");
    }
    
    [Fact]
    public async Task ShowRequestsByModel_NoModels_ShowsMessage()
    {
        // Arrange
        using var console = new ConsoleTestHelper("7\n3\n\n0\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("No models found in recent usage").Should().BeTrue();
    }
    
    [Fact]
    public async Task ShowRequestsByModel_WithModels_AllowsSelection()
    {
        // Arrange
        using var console = new ConsoleTestHelper("7\n3\n1\n\n0\n0");
        var entries = new[]
        {
            new UsageEntryBuilder().WithModel("gpt-4").Build(),
            new UsageEntryBuilder().WithModel("claude-3").Build()
        };
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.IsAny<DateTime>(), 
                It.IsAny<DateTime>()))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("1. claude-3").Should().BeTrue(); // Sorted alphabetically
        _logger.HasMessage("2. gpt-4").Should().BeTrue();
        _logger.HasMessage("Requests for claude-3").Should().BeTrue();
    }
    
    #endregion
    
    #region Export Tests
    
    [Fact]
    public async Task ExportReports_ShowsFormatOptions()
    {
        // Arrange
        using var console = new ConsoleTestHelper("8\n0\n0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("Export Reports").Should().BeTrue();
        _logger.HasMessage("1. Export as JSON").Should().BeTrue();
        _logger.HasMessage("2. Export as CSV").Should().BeTrue();
        _logger.HasMessage("3. Export as Markdown").Should().BeTrue();
    }
    
    [Fact]
    public async Task ExportAsJson_CreatesFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.Delete(tempFile); // Delete so the service can create it
        
        using var console = new ConsoleTestHelper($"8\n1\n\n\n\n0\n0");
        var entry = new UsageEntryBuilder().Build();
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new[] { entry });
            
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
        _logger.HasMessageOfLevel(LogLevel.Success, "Exported to").Should().BeTrue();
    }
    
    #endregion
    
    #region Helper Methods
    
    private void SetupMockData(
        IEnumerable<UsageEntry> entries, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.Is<DateTime>(d => startDate == null || d == startDate),
                It.Is<DateTime>(d => endDate == null || d == endDate)))
            .ReturnsAsync(entries);
    }
    
    private List<UsageEntry> CreateTestEntries(DateTime baseDate, int count)
    {
        return UsageEntryBuilder.CreateMultiple(count, (builder, i) =>
        {
            builder.WithTimestamp(baseDate.AddMinutes(-i * 30))
                   .WithModel($"model-{i % 3}")
                   .WithTokens(1000 + i * 100, 50 + i * 10)
                   .WithCost(0.01m * (i + 1))
                   .WithDuration(1.5 + i * 0.5);
        });
    }
    
    #endregion
}