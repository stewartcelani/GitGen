using GitGen.Models;

namespace GitGen.Services;

/// <summary>
///     Service for reading and analyzing usage data from JSONL files.
/// </summary>
public interface IUsageReportingService
{
    /// <summary>
    ///     Gets usage entries for a specific date range.
    /// </summary>
    /// <param name="startDate">The start date (inclusive).</param>
    /// <param name="endDate">The end date (inclusive).</param>
    /// <returns>A collection of usage entries.</returns>
    Task<IEnumerable<UsageEntry>> GetUsageEntriesAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    ///     Generates a daily usage report.
    /// </summary>
    /// <param name="date">The date to generate the report for (defaults to today).</param>
    /// <returns>The formatted report string.</returns>
    Task<string> GenerateDailyReportAsync(DateTime? date = null);

    /// <summary>
    ///     Generates a monthly usage report.
    /// </summary>
    /// <param name="year">The year for the report.</param>
    /// <param name="month">The month for the report.</param>
    /// <returns>The formatted report string.</returns>
    Task<string> GenerateMonthlyReportAsync(int? year = null, int? month = null);

    /// <summary>
    ///     Generates a custom usage report for a date range.
    /// </summary>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="modelFilter">Optional model name filter.</param>
    /// <param name="outputJson">Whether to output in JSON format.</param>
    /// <returns>The formatted report string.</returns>
    Task<string> GenerateCustomReportAsync(DateTime startDate, DateTime endDate, string? modelFilter = null, bool outputJson = false);
}