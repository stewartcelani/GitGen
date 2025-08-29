using System.Globalization;

namespace GitGen.Helpers;

/// <summary>
///     Provides culture-aware date and time formatting helpers.
/// </summary>
public static class DateTimeHelper
{
    /// <summary>
    ///     Converts a UTC date to local time and formats it as a short date string
    ///     using the current culture (e.g., 1/27/2025 for US, 27/01/2025 for AU).
    /// </summary>
    /// <param name="utcDate">The UTC date to format.</param>
    /// <returns>A culture-specific short date string.</returns>
    public static string ToLocalDateString(DateTime utcDate)
        => utcDate.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

    /// <summary>
    ///     Converts a UTC date to local time and formats it as a short date/time string
    ///     using the current culture (e.g., 1/27/2025 2:32 PM for US).
    /// </summary>
    /// <param name="utcDate">The UTC date to format.</param>
    /// <returns>A culture-specific short date/time string.</returns>
    public static string ToLocalDateTimeString(DateTime utcDate)
        => utcDate.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    /// <summary>
    ///     Converts a UTC date to local time and formats it as a short time string
    ///     using the current culture (e.g., 2:32 PM for US, 14:32 for many EU countries).
    /// </summary>
    /// <param name="utcDate">The UTC date to format.</param>
    /// <returns>A culture-specific short time string.</returns>
    public static string ToLocalTimeString(DateTime utcDate)
        => utcDate.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    /// <summary>
    ///     Converts a UTC date to local time and formats it as a long date string
    ///     using the current culture (e.g., Monday, January 27, 2025 for US).
    /// </summary>
    /// <param name="utcDate">The UTC date to format.</param>
    /// <returns>A culture-specific long date string.</returns>
    public static string ToLocalLongDateString(DateTime utcDate)
        => utcDate.ToLocalTime().ToString("D", CultureInfo.CurrentCulture);
}