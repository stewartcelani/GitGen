using System.Globalization;
using GitGen.Configuration;
using GitGen.Helpers;
using GitGen.Models;

namespace GitGen.Services;

/// <summary>
///     Provides an interactive menu-driven interface for viewing usage statistics and reports.
/// </summary>
public class UsageMenuService
{
    private readonly IConsoleLogger _logger;
    private readonly IUsageReportingService _reportingService;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly IConsoleInput _consoleInput;
    private readonly IConsoleOutput _consoleOutput;
    
    public UsageMenuService(
        IConsoleLogger logger,
        IUsageReportingService reportingService,
        ISecureConfigurationService secureConfig,
        IConsoleInput consoleInput,
        IConsoleOutput consoleOutput)
    {
        _logger = logger;
        _reportingService = reportingService;
        _secureConfig = secureConfig;
        _consoleInput = consoleInput;
        _consoleOutput = consoleOutput;
    }
    
    /// <summary>
    ///     Runs the main usage statistics menu.
    /// </summary>
    public async Task RunAsync()
    {
        while (true)
        {
            _consoleOutput.Clear();
            await DisplayMainMenu();
            
            var key = _consoleInput.ReadKey(true);
            
            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }
            
            switch (key.KeyChar)
            {
                case '1':
                    await ViewTodayUsage();
                    break;
                case '2':
                    await ViewYesterdayUsage();
                    break;
                case '3':
                    await ViewThisWeekUsage();
                    break;
                case '4':
                    await ViewThisMonthUsage();
                    break;
                case '5':
                    await ViewLastMonthUsage();
                    break;
                case '6':
                    await ViewCustomDateRange();
                    break;
                case '7':
                    await ViewDetailedRequests();
                    break;
                case '8':
                    await ExportReports();
                    break;
                default:
                    _logger.Warning("Invalid choice. Please try again.");
                    await Task.Delay(1500);
                    break;
            }
        }
    }
    
    private async Task DisplayMainMenu()
    {
        _logger.Information("╔════════════════════════════════════════╗");
        _logger.Information("║       GitGen Usage Statistics          ║");
        _logger.Information("╚════════════════════════════════════════╝");
        _consoleOutput.WriteLine();
        
        // Show quick stats
        var todayStats = await GetQuickStats(DateTime.Today, DateTime.Today);
        
        // Calculate start of week (Monday)
        var today = DateTime.Today;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff);
        var weekStats = await GetQuickStats(startOfWeek, today);
        
        var monthStats = await GetQuickStats(
            new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            DateTime.Today);
        
        // All time stats - use a reasonable early date (e.g., 2020-01-01)
        var allStats = await GetQuickStats(new DateTime(2020, 1, 1), DateTime.Today);
        
        _logger.Information($"Today: {todayStats.TotalCalls} calls, {CostCalculationService.FormatCurrency(todayStats.TotalCost, "USD")}");
        _logger.Information($"Week:  {weekStats.TotalCalls} calls, {CostCalculationService.FormatCurrency(weekStats.TotalCost, "USD")}");
        _logger.Information($"Month: {monthStats.TotalCalls} calls, {CostCalculationService.FormatCurrency(monthStats.TotalCost, "USD")}");
        _logger.Information($"All:   {allStats.TotalCalls} calls, {CostCalculationService.FormatCurrency(allStats.TotalCost, "USD")}");
        _consoleOutput.WriteLine();
        
        _logger.Information("1. Today's usage");
        _logger.Information("2. Yesterday's usage");
        _logger.Information("3. This week");
        _logger.Information("4. This month");
        _logger.Information("5. Last month");
        _logger.Information("6. Custom date range");
        _logger.Information("7. Detailed request history");
        _logger.Information("8. Export reports");
        
        _consoleOutput.WriteLine();
        _logger.Muted("Press ESC to go back...");
    }
    
    private async Task<QuickStats> GetQuickStats(DateTime startDate, DateTime endDate)
    {
        var entries = await _reportingService.GetUsageEntriesAsync(startDate, endDate);
        var entriesList = entries.ToList();
        
        return new QuickStats
        {
            TotalCalls = entriesList.Count,
            TotalCost = entriesList.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount)
        };
    }
    
    private class QuickStats
    {
        public int TotalCalls { get; set; }
        public decimal TotalCost { get; set; }
    }
    
    private async Task ViewTodayUsage()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Today's Usage ═══");
        _consoleOutput.WriteLine();
        
        var entries = await _reportingService.GetUsageEntriesAsync(DateTime.Today, DateTime.Today);
        DisplayUsageSummary(entries, "Today");
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ViewYesterdayUsage()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Yesterday's Usage ═══");
        _consoleOutput.WriteLine();
        
        var yesterday = DateTime.Today.AddDays(-1);
        var entries = await _reportingService.GetUsageEntriesAsync(yesterday, yesterday);
        DisplayUsageSummary(entries, "Yesterday");
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ViewThisWeekUsage()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ This Week's Usage ═══");
        _consoleOutput.WriteLine();
        
        // Calculate start of week (Monday)
        var today = DateTime.Today;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff);
        
        var entries = await _reportingService.GetUsageEntriesAsync(startOfWeek, today);
        DisplayUsageSummary(entries, $"Week of {DateTimeHelper.ToLocalDateString(startOfWeek)}");
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ViewThisMonthUsage()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ This Month's Usage ═══");
        _consoleOutput.WriteLine();
        
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var entries = await _reportingService.GetUsageEntriesAsync(startOfMonth, DateTime.Today);
        DisplayMonthlyReport(entries, DateTime.Today.Year, DateTime.Today.Month);
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ViewLastMonthUsage()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Last Month's Usage ═══");
        _consoleOutput.WriteLine();
        
        var lastMonth = DateTime.Today.AddMonths(-1);
        var startOfMonth = new DateTime(lastMonth.Year, lastMonth.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        
        var entries = await _reportingService.GetUsageEntriesAsync(startOfMonth, endOfMonth);
        DisplayMonthlyReport(entries, lastMonth.Year, lastMonth.Month);
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ViewCustomDateRange()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Custom Date Range ═══");
        _consoleOutput.WriteLine();
        
        DateTime startDate, endDate;
        
        // Get start date
        while (true)
        {
            _consoleOutput.Write($"Enter start date (YYYY-MM-DD) [{DateTime.Today.AddDays(-30):yyyy-MM-dd}]: ");
            var input = _consoleInput.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                startDate = DateTime.Today.AddDays(-30);
                break;
            }
            
            if (DateTime.TryParse(input, out startDate))
            {
                break;
            }
            
            _logger.Warning("Invalid date format. Please use YYYY-MM-DD.");
        }
        
        // Get end date
        while (true)
        {
            _consoleOutput.Write($"Enter end date (YYYY-MM-DD) [{DateTime.Today:yyyy-MM-dd}]: ");
            var input = _consoleInput.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                endDate = DateTime.Today;
                break;
            }
            
            if (DateTime.TryParse(input, out endDate))
            {
                if (endDate >= startDate)
                {
                    break;
                }
                _logger.Warning("End date must be after start date.");
            }
            else
            {
                _logger.Warning("Invalid date format. Please use YYYY-MM-DD.");
            }
        }
        
        _consoleOutput.WriteLine();
        var entries = await _reportingService.GetUsageEntriesAsync(startDate, endDate);
        DisplayUsageSummary(entries, $"{DateTimeHelper.ToLocalDateString(startDate)} to {DateTimeHelper.ToLocalDateString(endDate)}");
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ViewDetailedRequests()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Detailed Request History ═══");
        _consoleOutput.WriteLine();
        
        _logger.Information("1. Last 10 requests");
        _logger.Information("2. Today's requests");
        _logger.Information("3. Filter by model");
        _logger.Information("4. Filter by date");
        _logger.Information("0. Back");
        
        _consoleOutput.WriteLine();
        _consoleOutput.Write("Select option: ");
        
        var choice = _consoleInput.ReadLine()?.Trim();
        
        switch (choice)
        {
            case "1":
                await ShowLast10Requests();
                break;
            case "2":
                await ShowTodayRequests();
                break;
            case "3":
                await ShowRequestsByModel();
                break;
            case "4":
                await ShowRequestsByDate();
                break;
        }
    }
    
    private async Task ShowLast10Requests()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Last 10 Requests ═══");
        _consoleOutput.WriteLine();
        
        // Get last 30 days of entries and take last 10
        var entries = await _reportingService.GetUsageEntriesAsync(
            DateTime.Today.AddDays(-30), 
            DateTime.Today.AddDays(1)); // Include today
        
        var last10 = entries.OrderByDescending(e => e.Timestamp).Take(10).ToList();
        
        if (!last10.Any())
        {
            _logger.Information("No requests found in the last 30 days.");
        }
        else
        {
            DisplayDetailedRequests(last10);
        }
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ShowTodayRequests()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Today's Requests ═══");
        _consoleOutput.WriteLine();
        
        var entries = await _reportingService.GetUsageEntriesAsync(DateTime.Today, DateTime.Today);
        var entriesList = entries.OrderByDescending(e => e.Timestamp).ToList();
        
        if (!entriesList.Any())
        {
            _logger.Information("No requests today.");
        }
        else
        {
            DisplayDetailedRequests(entriesList);
        }
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ShowRequestsByModel()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Filter by Model ═══");
        _consoleOutput.WriteLine();
        
        // Get available models from recent usage
        var recentEntries = await _reportingService.GetUsageEntriesAsync(
            DateTime.Today.AddDays(-30), 
            DateTime.Today);
        
        var models = recentEntries
            .Select(e => e.Model.Name)
            .Distinct()
            .OrderBy(m => m)
            .ToList();
        
        if (!models.Any())
        {
            _logger.Information("No models found in recent usage.");
            await Task.Delay(1500);
            return;
        }
        
        // Display models
        for (int i = 0; i < models.Count; i++)
        {
            _logger.Information($"{i + 1}. {models[i]}");
        }
        
        _consoleOutput.WriteLine();
        _consoleOutput.Write("Select model number: ");
        
        if (int.TryParse(_consoleInput.ReadLine(), out int selection) && 
            selection > 0 && selection <= models.Count)
        {
            var selectedModel = models[selection - 1];
            _consoleOutput.Clear();
            _logger.Information($"═══ Requests for {selectedModel} ═══");
            _consoleOutput.WriteLine();
            
            var modelEntries = recentEntries
                .Where(e => e.Model.Name == selectedModel)
                .OrderByDescending(e => e.Timestamp)
                .Take(20)
                .ToList();
            
            DisplayDetailedRequests(modelEntries);
        }
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ShowRequestsByDate()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Filter by Date ═══");
        _consoleOutput.WriteLine();
        
        _consoleOutput.Write($"Enter date (YYYY-MM-DD) [{DateTime.Today:yyyy-MM-dd}]: ");
        var input = _consoleInput.ReadLine()?.Trim();
        
        DateTime date = string.IsNullOrEmpty(input) ? DateTime.Today : DateTime.Parse(input);
        
        var entries = await _reportingService.GetUsageEntriesAsync(date, date);
        var entriesList = entries.OrderByDescending(e => e.Timestamp).ToList();
        
        _consoleOutput.Clear();
        _logger.Information($"═══ Requests for {DateTimeHelper.ToLocalDateString(date)} ═══");
        _consoleOutput.WriteLine();
        
        if (!entriesList.Any())
        {
            _logger.Information("No requests on this date.");
        }
        else
        {
            DisplayDetailedRequests(entriesList);
        }
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private void DisplayDetailedRequests(List<UsageEntry> entries)
    {
        // Table headers
        _consoleOutput.WriteLine("┌────────────────┬──────────────────────┬──────────┬──────────┬──────────┬────────┬────────────┐");
        _consoleOutput.WriteLine("│ Time           │ Model                │ Input    │ Output   │ Total    │ Time   │ Cost       │");
        _consoleOutput.WriteLine("├────────────────┼──────────────────────┼──────────┼──────────┼──────────┼────────┼────────────┤");
        
        foreach (var entry in entries)
        {
            var time = DateTimeHelper.ToLocalTimeString(entry.Timestamp);
            var date = DateTimeHelper.ToLocalDateString(entry.Timestamp);
            var timeStr = entry.Timestamp.Date == DateTime.Today ? time : $"{date} {time}";
            
            var model = TruncateString(entry.Model.Name, 20);
            var inputTokens = FormatTokenCount(entry.Tokens.Input);
            var outputTokens = FormatTokenCount(entry.Tokens.Output);
            var totalTokens = FormatTokenCount(entry.Tokens.Total);
            var duration = $"{entry.Duration:F1}s";
            var cost = entry.Cost != null 
                ? CostCalculationService.FormatCurrency(entry.Cost.Amount, entry.Cost.Currency)
                : "N/A";
            
            _consoleOutput.WriteLine($"│ {timeStr,-14} │ {model,-20} │ {inputTokens,8} │ {outputTokens,8} │ {totalTokens,8} │ {duration,6} │ {cost,10} │");
        }
        
        _consoleOutput.WriteLine("└────────────────┴──────────────────────┴──────────┴──────────┴──────────┴────────┴────────────┘");
    }
    
    private void DisplayUsageSummary(IEnumerable<UsageEntry> entries, string periodName)
    {
        var entriesList = entries.ToList();
        
        if (!entriesList.Any())
        {
            _logger.Information($"No usage recorded for {periodName}.");
            return;
        }
        
        // Group by model
        var modelGroups = entriesList
            .GroupBy(e => e.Model.Name)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        // Calculate totals
        var totalCalls = entriesList.Count;
        var totalInputTokens = entriesList.Sum(e => e.Tokens.Input);
        var totalOutputTokens = entriesList.Sum(e => e.Tokens.Output);
        var totalTokens = entriesList.Sum(e => e.Tokens.Total);
        var totalCost = entriesList.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
        var overallAvgTime = entriesList.Average(e => e.Duration);
        
        // Summary header
        _logger.Information($"Period: {periodName}");
        _logger.Information($"Total calls: {totalCalls:N0}");
        _logger.Information($"Total cost: {CostCalculationService.FormatCurrency(totalCost, "USD")}");
        _logger.Information($"Average response time: {overallAvgTime:F1}s");
        _consoleOutput.WriteLine();
        
        // Table
        _consoleOutput.WriteLine("┌──────────────────────┬──────────┬────────────┬────────────┬────────────┬──────────┬────────────┐");
        _consoleOutput.WriteLine("│ Model                │ Calls    │ Input      │ Output     │ Total      │ Avg Time │ Cost       │");
        _consoleOutput.WriteLine("├──────────────────────┼──────────┼────────────┼────────────┼────────────┼──────────┼────────────┤");
        
        foreach (var modelGroup in modelGroups)
        {
            var calls = modelGroup.Count();
            var inputTokens = modelGroup.Sum(e => e.Tokens.Input);
            var outputTokens = modelGroup.Sum(e => e.Tokens.Output);
            var modelTotalTokens = modelGroup.Sum(e => e.Tokens.Total);
            var cost = modelGroup.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
            var avgTime = modelGroup.Average(e => e.Duration);
            var currency = modelGroup.FirstOrDefault(e => e.Cost != null)?.Cost?.Currency ?? "USD";
            
            var modelName = TruncateString(modelGroup.Key, 20);
            var costStr = CostCalculationService.FormatCurrency(cost, currency);
            
            _consoleOutput.WriteLine($"│ {modelName,-20} │ {calls,8:N0} │ {FormatTokenCount(inputTokens),10} │ {FormatTokenCount(outputTokens),10} │ {FormatTokenCount(modelTotalTokens),10} │ {avgTime,8:F1}s │ {costStr,10} │");
        }
        
        _consoleOutput.WriteLine("├──────────────────────┼──────────┼────────────┼────────────┼────────────┼──────────┼────────────┤");
        
        // Totals row
        var totalCostStr = CostCalculationService.FormatCurrency(totalCost, "USD");
        _consoleOutput.WriteLine($"│ {"TOTAL",-20} │ {totalCalls,8:N0} │ {FormatTokenCount(totalInputTokens),10} │ {FormatTokenCount(totalOutputTokens),10} │ {FormatTokenCount(totalTokens),10} │ {overallAvgTime,8:F1}s │ {totalCostStr,10} │");
        _consoleOutput.WriteLine("└──────────────────────┴──────────┴────────────┴────────────┴────────────┴──────────┴────────────┘");
        
        // Additional stats
        _consoleOutput.WriteLine();
        var sessions = entriesList.GroupBy(e => e.SessionId).Count();
        _logger.Muted($"Sessions: {sessions}");
        
        // Show top project if available
        var topProject = entriesList
            .Where(e => !string.IsNullOrEmpty(e.ProjectPath))
            .GroupBy(e => Path.GetFileName(e.ProjectPath))
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        
        if (topProject != null)
        {
            _logger.Muted($"Most active project: {topProject.Key} ({topProject.Count()} calls)");
        }
    }
    
    private void DisplayMonthlyReport(IEnumerable<UsageEntry> entries, int year, int month)
    {
        var entriesList = entries.ToList();
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");
        
        if (!entriesList.Any())
        {
            _logger.Information($"No usage recorded for {monthName}.");
            return;
        }
        
        // Daily summary first
        _logger.Information($"Month: {monthName}");
        _consoleOutput.WriteLine();
        
        var dailyGroups = entriesList
            .GroupBy(e => e.Timestamp.Date)
            .OrderBy(g => g.Key)
            .ToList();
        
        _consoleOutput.WriteLine("Daily Summary:");
        _consoleOutput.WriteLine("┌────────────┬──────────┬────────────┬────────────┬──────────┬────────────┐");
        _consoleOutput.WriteLine("│ Date       │ Calls    │ Input      │ Output     │ Avg Time │ Cost       │");
        _consoleOutput.WriteLine("├────────────┼──────────┼────────────┼────────────┼──────────┼────────────┤");
        
        foreach (var dayGroup in dailyGroups)
        {
            var date = DateTimeHelper.ToLocalDateString(dayGroup.Key);
            var calls = dayGroup.Count();
            var inputTokens = dayGroup.Sum(e => e.Tokens.Input);
            var outputTokens = dayGroup.Sum(e => e.Tokens.Output);
            var avgTime = dayGroup.Average(e => e.Duration);
            var cost = dayGroup.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
            
            _consoleOutput.WriteLine($"│ {date,-10} │ {calls,8:N0} │ {FormatTokenCount(inputTokens),10} │ {FormatTokenCount(outputTokens),10} │ {avgTime,8:F1}s │ {CostCalculationService.FormatCurrency(cost, "USD"),10} │");
        }
        
        _consoleOutput.WriteLine("└────────────┴──────────┴────────────┴────────────┴──────────┴────────────┘");
        
        // Then show model summary
        _consoleOutput.WriteLine();
        DisplayUsageSummary(entriesList, monthName);
    }
    
    private async Task ExportReports()
    {
        _consoleOutput.Clear();
        _logger.Information("═══ Export Reports ═══");
        _consoleOutput.WriteLine();
        
        _logger.Information("1. Export as JSON");
        _logger.Information("2. Export as CSV");
        _logger.Information("3. Export as Markdown");
        _logger.Information("0. Back");
        
        _consoleOutput.WriteLine();
        _consoleOutput.Write("Select format: ");
        
        var format = _consoleInput.ReadLine()?.Trim();
        if (format == "0" || string.IsNullOrEmpty(format))
            return;
        
        // Get date range
        _consoleOutput.WriteLine();
        _consoleOutput.Write($"Start date (YYYY-MM-DD) [{DateTime.Today.AddDays(-30):yyyy-MM-dd}]: ");
        var startInput = _consoleInput.ReadLine()?.Trim();
        var startDate = string.IsNullOrEmpty(startInput) ? DateTime.Today.AddDays(-30) : DateTime.Parse(startInput);
        
        _consoleOutput.Write($"End date (YYYY-MM-DD) [{DateTime.Today:yyyy-MM-dd}]: ");
        var endInput = _consoleInput.ReadLine()?.Trim();
        var endDate = string.IsNullOrEmpty(endInput) ? DateTime.Today : DateTime.Parse(endInput);
        
        var entries = await _reportingService.GetUsageEntriesAsync(startDate, endDate);
        
        switch (format)
        {
            case "1":
                await ExportAsJson(entries, startDate, endDate);
                break;
            case "2":
                await ExportAsCsv(entries, startDate, endDate);
                break;
            case "3":
                await ExportAsMarkdown(entries, startDate, endDate);
                break;
        }
        
        _consoleOutput.WriteLine();
        _logger.Information("Press any key to continue...");
        _consoleInput.ReadKey(true);
    }
    
    private async Task ExportAsJson(IEnumerable<UsageEntry> entries, DateTime startDate, DateTime endDate)
    {
        var filename = $"gitgen-usage-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.json";
        var json = await _reportingService.GenerateCustomReportAsync(startDate, endDate, null, true);
        
        await File.WriteAllTextAsync(filename, json);
        _logger.Success($"✅ Exported to {filename}");
    }
    
    private async Task ExportAsCsv(IEnumerable<UsageEntry> entries, DateTime startDate, DateTime endDate)
    {
        var filename = $"gitgen-usage-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv";
        var csv = new System.Text.StringBuilder();
        
        // Headers
        csv.AppendLine("Timestamp,SessionId,Model,Provider,InputTokens,OutputTokens,TotalTokens,Duration,Cost,Currency,Success,Project,Branch");
        
        // Data
        foreach (var entry in entries.OrderBy(e => e.Timestamp))
        {
            csv.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.SessionId},{entry.Model.Name},{entry.Model.Provider},{entry.Tokens.Input},{entry.Tokens.Output},{entry.Tokens.Total},{entry.Duration:F1},{entry.Cost?.Amount ?? 0},{entry.Cost?.Currency ?? "USD"},{entry.Success},{entry.ProjectPath},{entry.GitBranch}");
        }
        
        await File.WriteAllTextAsync(filename, csv.ToString());
        _logger.Success($"✅ Exported to {filename}");
    }
    
    private async Task ExportAsMarkdown(IEnumerable<UsageEntry> entries, DateTime startDate, DateTime endDate)
    {
        var filename = $"gitgen-usage-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.md";
        var md = new System.Text.StringBuilder();
        
        md.AppendLine($"# GitGen Usage Report");
        md.AppendLine($"**Period**: {DateTimeHelper.ToLocalDateString(startDate)} to {DateTimeHelper.ToLocalDateString(endDate)}");
        md.AppendLine();
        
        var entriesList = entries.ToList();
        var totalCost = entriesList.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
        
        md.AppendLine("## Summary");
        md.AppendLine($"- **Total Calls**: {entriesList.Count:N0}");
        md.AppendLine($"- **Total Cost**: {CostCalculationService.FormatCurrency(totalCost, "USD")}");
        md.AppendLine($"- **Average Response Time**: {entriesList.Average(e => e.Duration):F1}s");
        md.AppendLine();
        
        md.AppendLine("## Usage by Model");
        md.AppendLine("| Model | Calls | Input Tokens | Output Tokens | Total Tokens | Avg Time | Cost |");
        md.AppendLine("|-------|-------|--------------|---------------|--------------|----------|------|");
        
        var modelGroups = entriesList.GroupBy(e => e.Model.Name).OrderByDescending(g => g.Count());
        foreach (var group in modelGroups)
        {
            var calls = group.Count();
            var inputTokens = group.Sum(e => e.Tokens.Input);
            var outputTokens = group.Sum(e => e.Tokens.Output);
            var totalTokens = group.Sum(e => e.Tokens.Total);
            var avgTime = group.Average(e => e.Duration);
            var cost = group.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
            
            md.AppendLine($"| {group.Key} | {calls:N0} | {FormatTokenCount(inputTokens)} | {FormatTokenCount(outputTokens)} | {FormatTokenCount(totalTokens)} | {avgTime:F1}s | {CostCalculationService.FormatCurrency(cost, "USD")} |");
        }
        
        await File.WriteAllTextAsync(filename, md.ToString());
        _logger.Success($"✅ Exported to {filename}");
    }
    
    private static string FormatTokenCount(int tokens)
    {
        if (tokens >= 1_000_000)
            return $"{tokens / 1_000_000.0:F1}M";
        if (tokens >= 1_000)
            return $"{tokens / 1_000.0:F1}k";
        return tokens.ToString();
    }
    
    private static string TruncateString(string str, int maxLength)
    {
        if (str.Length <= maxLength)
            return str;
        return str.Substring(0, maxLength - 3) + "...";
    }
}