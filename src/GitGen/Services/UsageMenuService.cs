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
    
    public UsageMenuService(
        IConsoleLogger logger,
        IUsageReportingService reportingService,
        ISecureConfigurationService secureConfig)
    {
        _logger = logger;
        _reportingService = reportingService;
        _secureConfig = secureConfig;
    }
    
    /// <summary>
    ///     Runs the main usage statistics menu.
    /// </summary>
    public async Task RunAsync()
    {
        while (true)
        {
            Console.Clear();
            await DisplayMainMenu();
            
            var choice = Console.ReadLine()?.Trim();
            
            switch (choice)
            {
                case "1":
                    await ViewTodayUsage();
                    break;
                case "2":
                    await ViewYesterdayUsage();
                    break;
                case "3":
                    await ViewThisWeekUsage();
                    break;
                case "4":
                    await ViewThisMonthUsage();
                    break;
                case "5":
                    await ViewLastMonthUsage();
                    break;
                case "6":
                    await ViewCustomDateRange();
                    break;
                case "7":
                    await ViewDetailedRequests();
                    break;
                case "8":
                    await ExportReports();
                    break;
                case "0":
                case "":
                case null:
                    return;
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
        Console.WriteLine();
        
        // Show quick stats
        var todayStats = await GetQuickStats(DateTime.Today, DateTime.Today);
        var monthStats = await GetQuickStats(
            new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            DateTime.Today);
        
        _logger.Information($"Today: {todayStats.TotalCalls} calls, {CostCalculationService.FormatCurrency(todayStats.TotalCost, "USD")}");
        _logger.Information($"Month: {monthStats.TotalCalls} calls, {CostCalculationService.FormatCurrency(monthStats.TotalCost, "USD")}");
        Console.WriteLine();
        
        _logger.Information("1. Today's usage");
        _logger.Information("2. Yesterday's usage");
        _logger.Information("3. This week");
        _logger.Information("4. This month");
        _logger.Information("5. Last month");
        _logger.Information("6. Custom date range");
        _logger.Information("7. Detailed request history");
        _logger.Information("8. Export reports");
        _logger.Information("0. Back to main menu");
        
        Console.WriteLine();
        Console.Write("Select option: ");
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
        Console.Clear();
        _logger.Information("═══ Today's Usage ═══");
        Console.WriteLine();
        
        var entries = await _reportingService.GetUsageEntriesAsync(DateTime.Today, DateTime.Today);
        DisplayUsageSummary(entries, "Today");
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ViewYesterdayUsage()
    {
        Console.Clear();
        _logger.Information("═══ Yesterday's Usage ═══");
        Console.WriteLine();
        
        var yesterday = DateTime.Today.AddDays(-1);
        var entries = await _reportingService.GetUsageEntriesAsync(yesterday, yesterday);
        DisplayUsageSummary(entries, "Yesterday");
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ViewThisWeekUsage()
    {
        Console.Clear();
        _logger.Information("═══ This Week's Usage ═══");
        Console.WriteLine();
        
        // Calculate start of week (Monday)
        var today = DateTime.Today;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        var startOfWeek = today.AddDays(-diff);
        
        var entries = await _reportingService.GetUsageEntriesAsync(startOfWeek, today);
        DisplayUsageSummary(entries, $"Week of {DateTimeHelper.ToLocalDateString(startOfWeek)}");
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ViewThisMonthUsage()
    {
        Console.Clear();
        _logger.Information("═══ This Month's Usage ═══");
        Console.WriteLine();
        
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var entries = await _reportingService.GetUsageEntriesAsync(startOfMonth, DateTime.Today);
        DisplayMonthlyReport(entries, DateTime.Today.Year, DateTime.Today.Month);
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ViewLastMonthUsage()
    {
        Console.Clear();
        _logger.Information("═══ Last Month's Usage ═══");
        Console.WriteLine();
        
        var lastMonth = DateTime.Today.AddMonths(-1);
        var startOfMonth = new DateTime(lastMonth.Year, lastMonth.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        
        var entries = await _reportingService.GetUsageEntriesAsync(startOfMonth, endOfMonth);
        DisplayMonthlyReport(entries, lastMonth.Year, lastMonth.Month);
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ViewCustomDateRange()
    {
        Console.Clear();
        _logger.Information("═══ Custom Date Range ═══");
        Console.WriteLine();
        
        DateTime startDate, endDate;
        
        // Get start date
        while (true)
        {
            Console.Write($"Enter start date (YYYY-MM-DD) [{DateTime.Today.AddDays(-30):yyyy-MM-dd}]: ");
            var input = Console.ReadLine()?.Trim();
            
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
            Console.Write($"Enter end date (YYYY-MM-DD) [{DateTime.Today:yyyy-MM-dd}]: ");
            var input = Console.ReadLine()?.Trim();
            
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
        
        Console.WriteLine();
        var entries = await _reportingService.GetUsageEntriesAsync(startDate, endDate);
        DisplayUsageSummary(entries, $"{DateTimeHelper.ToLocalDateString(startDate)} to {DateTimeHelper.ToLocalDateString(endDate)}");
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ViewDetailedRequests()
    {
        Console.Clear();
        _logger.Information("═══ Detailed Request History ═══");
        Console.WriteLine();
        
        _logger.Information("1. Last 10 requests");
        _logger.Information("2. Today's requests");
        _logger.Information("3. Filter by model");
        _logger.Information("4. Filter by date");
        _logger.Information("0. Back");
        
        Console.WriteLine();
        Console.Write("Select option: ");
        
        var choice = Console.ReadLine()?.Trim();
        
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
        Console.Clear();
        _logger.Information("═══ Last 10 Requests ═══");
        Console.WriteLine();
        
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
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ShowTodayRequests()
    {
        Console.Clear();
        _logger.Information("═══ Today's Requests ═══");
        Console.WriteLine();
        
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
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ShowRequestsByModel()
    {
        Console.Clear();
        _logger.Information("═══ Filter by Model ═══");
        Console.WriteLine();
        
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
        
        Console.WriteLine();
        Console.Write("Select model number: ");
        
        if (int.TryParse(Console.ReadLine(), out int selection) && 
            selection > 0 && selection <= models.Count)
        {
            var selectedModel = models[selection - 1];
            Console.Clear();
            _logger.Information($"═══ Requests for {selectedModel} ═══");
            Console.WriteLine();
            
            var modelEntries = recentEntries
                .Where(e => e.Model.Name == selectedModel)
                .OrderByDescending(e => e.Timestamp)
                .Take(20)
                .ToList();
            
            DisplayDetailedRequests(modelEntries);
        }
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private async Task ShowRequestsByDate()
    {
        Console.Clear();
        _logger.Information("═══ Filter by Date ═══");
        Console.WriteLine();
        
        Console.Write($"Enter date (YYYY-MM-DD) [{DateTime.Today:yyyy-MM-dd}]: ");
        var input = Console.ReadLine()?.Trim();
        
        DateTime date = string.IsNullOrEmpty(input) ? DateTime.Today : DateTime.Parse(input);
        
        var entries = await _reportingService.GetUsageEntriesAsync(date, date);
        var entriesList = entries.OrderByDescending(e => e.Timestamp).ToList();
        
        Console.Clear();
        _logger.Information($"═══ Requests for {DateTimeHelper.ToLocalDateString(date)} ═══");
        Console.WriteLine();
        
        if (!entriesList.Any())
        {
            _logger.Information("No requests on this date.");
        }
        else
        {
            DisplayDetailedRequests(entriesList);
        }
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
    }
    
    private void DisplayDetailedRequests(List<UsageEntry> entries)
    {
        // Table headers
        Console.WriteLine("┌────────────────┬──────────────────────┬──────────┬──────────┬──────────┬────────┬────────────┐");
        Console.WriteLine("│ Time           │ Model                │ Input    │ Output   │ Total    │ Time   │ Cost       │");
        Console.WriteLine("├────────────────┼──────────────────────┼──────────┼──────────┼──────────┼────────┼────────────┤");
        
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
            
            Console.WriteLine($"│ {timeStr,-14} │ {model,-20} │ {inputTokens,8} │ {outputTokens,8} │ {totalTokens,8} │ {duration,6} │ {cost,10} │");
        }
        
        Console.WriteLine("└────────────────┴──────────────────────┴──────────┴──────────┴──────────┴────────┴────────────┘");
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
        Console.WriteLine();
        
        // Table
        Console.WriteLine("┌──────────────────────┬──────────┬────────────┬────────────┬────────────┬──────────┬────────────┐");
        Console.WriteLine("│ Model                │ Calls    │ Input      │ Output     │ Total      │ Avg Time │ Cost       │");
        Console.WriteLine("├──────────────────────┼──────────┼────────────┼────────────┼────────────┼──────────┼────────────┤");
        
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
            
            Console.WriteLine($"│ {modelName,-20} │ {calls,8:N0} │ {FormatTokenCount(inputTokens),10} │ {FormatTokenCount(outputTokens),10} │ {FormatTokenCount(modelTotalTokens),10} │ {avgTime,8:F1}s │ {costStr,10} │");
        }
        
        Console.WriteLine("├──────────────────────┼──────────┼────────────┼────────────┼────────────┼──────────┼────────────┤");
        
        // Totals row
        var totalCostStr = CostCalculationService.FormatCurrency(totalCost, "USD");
        Console.WriteLine($"│ {"TOTAL",-20} │ {totalCalls,8:N0} │ {FormatTokenCount(totalInputTokens),10} │ {FormatTokenCount(totalOutputTokens),10} │ {FormatTokenCount(totalTokens),10} │ {overallAvgTime,8:F1}s │ {totalCostStr,10} │");
        Console.WriteLine("└──────────────────────┴──────────┴────────────┴────────────┴────────────┴──────────┴────────────┘");
        
        // Additional stats
        Console.WriteLine();
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
        Console.WriteLine();
        
        var dailyGroups = entriesList
            .GroupBy(e => e.Timestamp.Date)
            .OrderBy(g => g.Key)
            .ToList();
        
        Console.WriteLine("Daily Summary:");
        Console.WriteLine("┌────────────┬──────────┬────────────┬────────────┬──────────┬────────────┐");
        Console.WriteLine("│ Date       │ Calls    │ Input      │ Output     │ Avg Time │ Cost       │");
        Console.WriteLine("├────────────┼──────────┼────────────┼────────────┼──────────┼────────────┤");
        
        foreach (var dayGroup in dailyGroups)
        {
            var date = DateTimeHelper.ToLocalDateString(dayGroup.Key);
            var calls = dayGroup.Count();
            var inputTokens = dayGroup.Sum(e => e.Tokens.Input);
            var outputTokens = dayGroup.Sum(e => e.Tokens.Output);
            var avgTime = dayGroup.Average(e => e.Duration);
            var cost = dayGroup.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
            
            Console.WriteLine($"│ {date,-10} │ {calls,8:N0} │ {FormatTokenCount(inputTokens),10} │ {FormatTokenCount(outputTokens),10} │ {avgTime,8:F1}s │ {CostCalculationService.FormatCurrency(cost, "USD"),10} │");
        }
        
        Console.WriteLine("└────────────┴──────────┴────────────┴────────────┴──────────┴────────────┘");
        
        // Then show model summary
        Console.WriteLine();
        DisplayUsageSummary(entriesList, monthName);
    }
    
    private async Task ExportReports()
    {
        Console.Clear();
        _logger.Information("═══ Export Reports ═══");
        Console.WriteLine();
        
        _logger.Information("1. Export as JSON");
        _logger.Information("2. Export as CSV");
        _logger.Information("3. Export as Markdown");
        _logger.Information("0. Back");
        
        Console.WriteLine();
        Console.Write("Select format: ");
        
        var format = Console.ReadLine()?.Trim();
        if (format == "0" || string.IsNullOrEmpty(format))
            return;
        
        // Get date range
        Console.WriteLine();
        Console.Write($"Start date (YYYY-MM-DD) [{DateTime.Today.AddDays(-30):yyyy-MM-dd}]: ");
        var startInput = Console.ReadLine()?.Trim();
        var startDate = string.IsNullOrEmpty(startInput) ? DateTime.Today.AddDays(-30) : DateTime.Parse(startInput);
        
        Console.Write($"End date (YYYY-MM-DD) [{DateTime.Today:yyyy-MM-dd}]: ");
        var endInput = Console.ReadLine()?.Trim();
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
        
        Console.WriteLine();
        _logger.Information("Press any key to continue...");
        Console.ReadKey(true);
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