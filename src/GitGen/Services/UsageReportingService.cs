using System.Globalization;
using System.Text;
using System.Text.Json;
using GitGen.Models;

namespace GitGen.Services;

/// <summary>
///     Implementation of usage reporting that reads from JSONL files and generates reports.
/// </summary>
public class UsageReportingService : IUsageReportingService
{
    private readonly string _usageDirectory;
    private readonly IConsoleLogger _logger;

    public UsageReportingService(IConsoleLogger logger)
    {
        _logger = logger;
        
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _usageDirectory = Path.Combine(homeDir, ".gitgen", "usage");
    }

    public async Task<IEnumerable<UsageEntry>> GetUsageEntriesAsync(DateTime startDate, DateTime endDate)
    {
        var entries = new List<UsageEntry>();
        
        if (!Directory.Exists(_usageDirectory))
        {
            return entries;
        }

        // Determine which monthly files to read based on date range
        var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
        var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

        while (currentDate <= endMonth)
        {
            var fileName = $"usage-{currentDate:yyyy-MM}.jsonl";
            var filePath = Path.Combine(_usageDirectory, fileName);

            if (File.Exists(filePath))
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    try
                    {
                        var entry = JsonSerializer.Deserialize(line, UsageJsonContext.Default.UsageEntry);
                        if (entry != null && 
                            entry.Timestamp.Date >= startDate.Date && 
                            entry.Timestamp.Date <= endDate.Date)
                        {
                            entries.Add(entry);
                        }
                    }
                    catch (JsonException)
                    {
                        _logger.Debug($"Skipping invalid JSON line in {fileName}");
                    }
                }
            }

            currentDate = currentDate.AddMonths(1);
        }

        return entries.OrderBy(e => e.Timestamp);
    }

    public async Task<string> GenerateDailyReportAsync(DateTime? date = null)
    {
        var reportDate = date?.Date ?? DateTime.Today;
        var entries = await GetUsageEntriesAsync(reportDate, reportDate);

        var report = new StringBuilder();
        report.AppendLine($"GitGen Usage Report - {reportDate:yyyy-MM-dd}");
        report.AppendLine(new string('=', 50));

        if (!entries.Any())
        {
            report.AppendLine("No usage recorded for this date.");
            return report.ToString();
        }

        // Group by model
        var modelGroups = entries.GroupBy(e => e.Model.Name).OrderByDescending(g => g.Count());

        report.AppendLine("\nUsage by Model:");
        report.AppendLine(new string('-', 50));
        report.AppendLine($"{"Model",-20} {"Calls",8} {"Input",10} {"Output",10} {"Total",10} {"Cost",10}");
        report.AppendLine(new string('-', 50));

        decimal totalCost = 0;
        int totalCalls = 0;
        int totalInputTokens = 0;
        int totalOutputTokens = 0;

        foreach (var modelGroup in modelGroups)
        {
            var calls = modelGroup.Count();
            var inputTokens = modelGroup.Sum(e => e.Tokens.Input);
            var outputTokens = modelGroup.Sum(e => e.Tokens.Output);
            var totalTokens = modelGroup.Sum(e => e.Tokens.Total);
            var cost = modelGroup.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
            var currency = modelGroup.FirstOrDefault(e => e.Cost != null)?.Cost?.Currency ?? "USD";

            totalCalls += calls;
            totalInputTokens += inputTokens;
            totalOutputTokens += outputTokens;
            totalCost += cost;

            var costStr = CostCalculationService.FormatCurrency(cost, currency);
            report.AppendLine($"{modelGroup.Key,-20} {calls,8} {FormatTokenCount(inputTokens),10} {FormatTokenCount(outputTokens),10} {FormatTokenCount(totalTokens),10} {costStr,10}");
        }

        report.AppendLine(new string('-', 50));
        var totalCostStr = CostCalculationService.FormatCurrency(totalCost, "USD");
        report.AppendLine($"{"TOTAL",-20} {totalCalls,8} {FormatTokenCount(totalInputTokens),10} {FormatTokenCount(totalOutputTokens),10} {FormatTokenCount(totalInputTokens + totalOutputTokens),10} {totalCostStr,10}");

        // Add session summary
        var sessions = entries.GroupBy(e => e.SessionId).Count();
        report.AppendLine($"\nSessions: {sessions}");
        
        // Add average response time
        var avgDuration = entries.Average(e => e.Duration);
        report.AppendLine($"Average response time: {avgDuration:F1}s");

        return report.ToString();
    }

    public async Task<string> GenerateMonthlyReportAsync(int? year = null, int? month = null)
    {
        var reportYear = year ?? DateTime.Today.Year;
        var reportMonth = month ?? DateTime.Today.Month;
        var startDate = new DateTime(reportYear, reportMonth, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var entries = await GetUsageEntriesAsync(startDate, endDate);

        var report = new StringBuilder();
        report.AppendLine($"GitGen Usage Report - {startDate:MMMM yyyy}");
        report.AppendLine(new string('=', 70));

        if (!entries.Any())
        {
            report.AppendLine("No usage recorded for this month.");
            return report.ToString();
        }

        // Daily summary
        report.AppendLine("\nDaily Usage:");
        report.AppendLine(new string('-', 70));
        report.AppendLine($"{"Date",-12} {"Model",-20} {"Calls",8} {"Input",10} {"Output",10} {"Total",10} {"Cost",10}");
        report.AppendLine(new string('-', 70));

        var dailyGroups = entries.GroupBy(e => e.Timestamp.Date).OrderBy(g => g.Key);
        decimal monthlyTotalCost = 0;

        foreach (var dayGroup in dailyGroups)
        {
            var modelGroups = dayGroup.GroupBy(e => e.Model.Name);
            
            foreach (var modelGroup in modelGroups)
            {
                var calls = modelGroup.Count();
                var inputTokens = modelGroup.Sum(e => e.Tokens.Input);
                var outputTokens = modelGroup.Sum(e => e.Tokens.Output);
                var modelTotalTokens = modelGroup.Sum(e => e.Tokens.Total);
                var cost = modelGroup.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
                var currency = modelGroup.FirstOrDefault(e => e.Cost != null)?.Cost?.Currency ?? "USD";

                monthlyTotalCost += cost;

                var costStr = CostCalculationService.FormatCurrency(cost, currency);
                report.AppendLine($"{dayGroup.Key:yyyy-MM-dd}  {modelGroup.Key,-20} {calls,8} {FormatTokenCount(inputTokens),10} {FormatTokenCount(outputTokens),10} {FormatTokenCount(modelTotalTokens),10} {costStr,10}");
            }
        }

        report.AppendLine(new string('-', 70));

        // Monthly summary
        report.AppendLine("\nMonthly Summary:");
        report.AppendLine(new string('-', 50));
        
        var totalCalls = entries.Count();
        var totalInputTokens = entries.Sum(e => e.Tokens.Input);
        var totalOutputTokens = entries.Sum(e => e.Tokens.Output);
        var totalTokens = entries.Sum(e => e.Tokens.Total);
        
        report.AppendLine($"Total calls: {totalCalls:N0}");
        report.AppendLine($"Total tokens: {totalTokens:N0} (Input: {totalInputTokens:N0}, Output: {totalOutputTokens:N0})");
        report.AppendLine($"Total cost: {CostCalculationService.FormatCurrency(monthlyTotalCost, "USD")}");
        
        // Top models
        report.AppendLine("\nTop Models by Usage:");
        var topModels = entries.GroupBy(e => e.Model.Name)
            .Select(g => new { Model = g.Key, Count = g.Count(), Cost = g.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount) })
            .OrderByDescending(x => x.Count)
            .Take(5);
        
        foreach (var model in topModels)
        {
            report.AppendLine($"  {model.Model}: {model.Count} calls, {CostCalculationService.FormatCurrency(model.Cost, "USD")}");
        }

        return report.ToString();
    }

    public async Task<string> GenerateCustomReportAsync(DateTime startDate, DateTime endDate, string? modelFilter = null, bool outputJson = false)
    {
        var entries = await GetUsageEntriesAsync(startDate, endDate);

        // Apply model filter if specified
        if (!string.IsNullOrWhiteSpace(modelFilter))
        {
            entries = entries.Where(e => e.Model.Name.Contains(modelFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (outputJson)
        {
            var summary = new
            {
                StartDate = startDate.ToString("yyyy-MM-dd"),
                EndDate = endDate.ToString("yyyy-MM-dd"),
                ModelFilter = modelFilter,
                TotalCalls = entries.Count(),
                TotalInputTokens = entries.Sum(e => e.Tokens.Input),
                TotalOutputTokens = entries.Sum(e => e.Tokens.Output),
                TotalTokens = entries.Sum(e => e.Tokens.Total),
                TotalCost = entries.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount),
                Currency = "USD",
                ModelBreakdown = entries.GroupBy(e => e.Model.Name)
                    .Select(g => new
                    {
                        Model = g.Key,
                        Calls = g.Count(),
                        InputTokens = g.Sum(e => e.Tokens.Input),
                        OutputTokens = g.Sum(e => e.Tokens.Output),
                        TotalTokens = g.Sum(e => e.Tokens.Total),
                        Cost = g.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount)
                    })
                    .OrderByDescending(x => x.Calls)
            };

            return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        }

        // Generate text report
        var report = new StringBuilder();
        report.AppendLine($"GitGen Usage Report - Custom Range");
        report.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(modelFilter))
        {
            report.AppendLine($"Filter: Model contains '{modelFilter}'");
        }
        report.AppendLine(new string('=', 50));

        if (!entries.Any())
        {
            report.AppendLine("No usage recorded for this period.");
            return report.ToString();
        }

        // Summary statistics
        var totalCalls = entries.Count();
        var totalInputTokens = entries.Sum(e => e.Tokens.Input);
        var totalOutputTokens = entries.Sum(e => e.Tokens.Output);
        var totalTokens = entries.Sum(e => e.Tokens.Total);
        var totalCost = entries.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
        var avgDuration = entries.Average(e => e.Duration);

        report.AppendLine("\nSummary:");
        report.AppendLine($"  Total calls: {totalCalls:N0}");
        report.AppendLine($"  Total tokens: {totalTokens:N0} (Input: {totalInputTokens:N0}, Output: {totalOutputTokens:N0})");
        report.AppendLine($"  Total cost: {CostCalculationService.FormatCurrency(totalCost, "USD")}");
        report.AppendLine($"  Average response time: {avgDuration:F1}s");

        // Model breakdown
        report.AppendLine("\nBreakdown by Model:");
        report.AppendLine(new string('-', 50));
        report.AppendLine($"{"Model",-20} {"Calls",8} {"Tokens",12} {"Cost",10}");
        report.AppendLine(new string('-', 50));

        var modelGroups = entries.GroupBy(e => e.Model.Name).OrderByDescending(g => g.Count());
        foreach (var modelGroup in modelGroups)
        {
            var calls = modelGroup.Count();
            var tokens = modelGroup.Sum(e => e.Tokens.Total);
            var cost = modelGroup.Where(e => e.Cost != null).Sum(e => e.Cost!.Amount);
            var currency = modelGroup.FirstOrDefault(e => e.Cost != null)?.Cost?.Currency ?? "USD";

            var costStr = CostCalculationService.FormatCurrency(cost, currency);
            report.AppendLine($"{modelGroup.Key,-20} {calls,8} {FormatTokenCount(tokens),12} {costStr,10}");
        }

        return report.ToString();
    }

    private static string FormatTokenCount(int tokens)
    {
        if (tokens >= 1_000_000)
        {
            return $"{tokens / 1_000_000.0:F1}M";
        }
        if (tokens >= 1_000)
        {
            return $"{tokens / 1_000.0:F1}k";
        }
        return tokens.ToString();
    }
}