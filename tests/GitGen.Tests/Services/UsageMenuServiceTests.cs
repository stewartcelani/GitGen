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
    private readonly TestConsoleInput _consoleInput;
    private readonly TestConsoleOutput _consoleOutput;
    private readonly UsageMenuService _service;
    
    public UsageMenuServiceTests()
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
    
    #region Main Menu Navigation Tests
    
    [Fact]
    public async Task RunAsync_SelectingExit_ReturnsImmediately()
    {
        // Arrange
        _consoleInput.AddLineInput("0");
        SetupMockData(new List<UsageEntry>());
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("GitGen Usage Statistics").Should().BeTrue();
        _reportingServiceMock.Verify(x => x.GetUsageEntriesAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Exactly(4)); // For quick stats (Today, Week, Month, All)
    }
    
    [Fact]
    public async Task RunAsync_EmptyInput_ExitsMenu()
    {
        // Arrange
        _consoleInput.AddLineInput("");
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
        _consoleInput.AddLineInputs("99", "0");
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
        _consoleInput.AddLineInputs(choice, "", "0");
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
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
        _consoleInput.AddLineInput("0");
        var todayEntries = UsageEntryBuilder.CreateMultiple(3, (builder, i) =>
        {
            builder.WithTimestamp(DateTime.Today)
                   .WithCost(0.01m * (i + 1));
        });
        
        // Calculate start of week (Monday)
        var today = DateTime.Today;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff);
        
        var weekEntries = UsageEntryBuilder.CreateMultiple(5, (builder, i) =>
        {
            builder.WithTimestamp(DateTime.Today.AddDays(-i))
                   .WithCost(0.02m);
        });
        
        var monthEntries = UsageEntryBuilder.CreateMultiple(10, (builder, i) =>
        {
            builder.WithTimestamp(DateTime.Today.AddDays(-i))
                   .WithCost(0.05m);
        });
        
        var allEntries = UsageEntryBuilder.CreateMultiple(20, (builder, i) =>
        {
            builder.WithTimestamp(DateTime.Today.AddDays(-i * 10))
                   .WithCost(0.10m);
        });
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(todayEntries);
            
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.Is<DateTime>(d => d == startOfWeek), 
                DateTime.Today))
            .ReturnsAsync(weekEntries);
            
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.Is<DateTime>(d => d.Day == 1), 
                DateTime.Today))
            .ReturnsAsync(monthEntries);
            
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.Is<DateTime>(d => d.Year == 2020 && d.Month == 1 && d.Day == 1), 
                DateTime.Today))
            .ReturnsAsync(allEntries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("Today: 3 calls, $0.06").Should().BeTrue();
        _logger.HasMessage("Week:  5 calls, $0.10").Should().BeTrue();
        _logger.HasMessage("Month: 10 calls, $0.50").Should().BeTrue();
        _logger.HasMessage("All:   20 calls, $2.00").Should().BeTrue();
    }
    
    #endregion
    
    #region Today's Usage Tests
    
    [Fact]
    public async Task ViewTodayUsage_WithData_DisplaysSummary()
    {
        // Arrange
        _consoleInput.AddKeyInput('1'); // Select option 1 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entries = CreateTestEntries(DateTime.Today, 5);
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(DateTime.Today, DateTime.Today))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("Today's Usage").Should().BeTrue();
        _logger.HasMessage("Period: Today").Should().BeTrue();
        _logger.HasMessage("Total calls: 5").Should().BeTrue();
    }
    
    [Fact]
    public async Task ViewTodayUsage_NoData_ShowsNoUsageMessage()
    {
        // Arrange
        _consoleInput.AddKeyInput('1'); // Select option 1 from main menu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        _consoleInput.AddKeyInput('6'); // Select option 6 from main menu
        _consoleInput.AddLineInputs("", ""); // Empty inputs for default dates
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        _consoleInput.AddKeyInput('6'); // Select option 6 from main menu
        _consoleInput.AddLineInputs("invalid", "2025-01-01", "2025-01-31", ""); // Date inputs
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        _consoleInput.AddKeyInput('6'); // Select option 6 from main menu
        _consoleInput.AddLineInputs("2025-01-31", "2025-01-01", "2025-02-01", ""); // Date inputs
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        _consoleInput.AddKeyInput('7'); // Select option 7 from main menu
        _consoleInput.AddLineInput("0"); // Back from submenu
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        _consoleInput.AddKeyInput('7'); // Select option 7 from main menu
        _consoleInput.AddLineInput("1"); // Select option 1 from submenu
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
        var entries = CreateTestEntries(DateTime.Today, 15);
        
        _reportingServiceMock
            .Setup(x => x.GetUsageEntriesAsync(
                It.IsAny<DateTime>(), 
                It.IsAny<DateTime>()))
            .ReturnsAsync(entries);
        
        // Act
        await _service.RunAsync();
        
        // Assert
        _logger.HasMessage("Last 10 Requests").Should().BeTrue();
        var output = _consoleOutput.GetOutput();
        output.Should().Contain("│ Time           │ Model                │ Input    │ Output   │ Total    │ Time   │ Cost       │");
    }
    
    [Fact]
    public async Task ShowRequestsByModel_NoModels_ShowsMessage()
    {
        // Arrange
        _consoleInput.AddKeyInput('7'); // Select option 7 from main menu
        _consoleInput.AddLineInput("3"); // Select option 3 from submenu
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        _consoleInput.AddKeyInput('7'); // Select option 7 from main menu
        _consoleInput.AddLineInputs("3", "1"); // Select option 3, then model 1
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        _consoleInput.AddKeyInput('8'); // Select option 8 from main menu
        _consoleInput.AddLineInput("0"); // Back from export menu
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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
        
        _consoleInput.AddKeyInput('8'); // Select option 8 from main menu
        _consoleInput.AddLineInputs("1", "", ""); // Select JSON format, default dates
        _consoleInput.AddKeyInput('\r'); // For "Press any key to continue"
        _consoleInput.AddKeyInput(ConsoleKey.Escape); // Exit main menu
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