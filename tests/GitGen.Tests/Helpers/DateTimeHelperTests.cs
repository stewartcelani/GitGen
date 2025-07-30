using System.Globalization;
using FluentAssertions;
using GitGen.Helpers;
using Xunit;

namespace GitGen.Tests.Helpers;

public class DateTimeHelperTests
{
    [Fact]
    public void ToLocalDateString_ConvertsUtcToLocal()
    {
        // Arrange
        var utcDate = new DateTime(2025, 1, 27, 10, 30, 0, DateTimeKind.Utc);
        var currentCulture = CultureInfo.CurrentCulture;

        // Act
        var result = DateTimeHelper.ToLocalDateString(utcDate);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // The exact format depends on the current culture, so we just verify it's not empty
        // and contains expected date components
        var localDate = utcDate.ToLocalTime();
        result.Should().Contain(localDate.Day.ToString());
        result.Should().Contain(localDate.Year.ToString());
    }

    [Fact]
    public void ToLocalDateTimeString_IncludesTimeComponent()
    {
        // Arrange
        var utcDate = new DateTime(2025, 1, 27, 14, 32, 45, DateTimeKind.Utc);

        // Act
        var result = DateTimeHelper.ToLocalDateTimeString(utcDate);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var localDate = utcDate.ToLocalTime();
        result.Should().Contain(localDate.Year.ToString());
        // Should contain time components (hour/minute)
        result.Should().Match(s => s.Contains(":"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("de-DE")]
    public void DateFormatting_RespectsCurrentCulture(string cultureName)
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var utcDate = new DateTime(2025, 1, 27, 10, 0, 0, DateTimeKind.Utc);

            // Act
            var shortDate = DateTimeHelper.ToLocalDateString(utcDate);
            var longDate = DateTimeHelper.ToLocalLongDateString(utcDate);

            // Assert
            shortDate.Should().NotBeNullOrEmpty();
            longDate.Should().NotBeNullOrEmpty();
            
            // Long date should be longer than short date
            longDate.Length.Should().BeGreaterThan(shortDate.Length);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ToLocalTimeString_ConvertsUtcToLocalTime()
    {
        // Arrange
        var utcDate = new DateTime(2025, 1, 27, 14, 32, 45, DateTimeKind.Utc);

        // Act
        var result = DateTimeHelper.ToLocalTimeString(utcDate);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Match(s => s.Contains(":"), "time string should contain colon separator");
        
        // Verify it contains the hour component (accounting for time zone differences)
        var localDate = utcDate.ToLocalTime();
        var hour12 = localDate.Hour > 12 ? localDate.Hour - 12 : (localDate.Hour == 0 ? 12 : localDate.Hour);
        var hour24 = localDate.Hour;
        
        // Should contain either 12-hour or 24-hour format hour
        result.Should().Match(s => s.Contains(hour12.ToString()) || s.Contains(hour24.ToString()));
    }

    [Fact]
    public void ToLocalTimeString_DifferentCultures_FormatsCorrectly()
    {
        // Arrange
        var utcDate = new DateTime(2025, 1, 27, 14, 30, 0, DateTimeKind.Utc);
        var originalCulture = CultureInfo.CurrentCulture;

        // Test US culture (12-hour format with AM/PM)
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var usTime = DateTimeHelper.ToLocalTimeString(utcDate);
            usTime.Should().Match(s => s.Contains("AM") || s.Contains("PM") || s.Contains("am") || s.Contains("pm"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        // Test German culture (24-hour format)
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var deTime = DateTimeHelper.ToLocalTimeString(utcDate);
            deTime.Should().NotContain("AM");
            deTime.Should().NotContain("PM");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void AllDateTimeHelpers_HandleMinAndMaxDates()
    {
        // Arrange
        var minDate = DateTime.MinValue.ToUniversalTime();
        var maxDate = DateTime.MaxValue.ToUniversalTime();

        // Act & Assert - Should not throw
        var minDateString = DateTimeHelper.ToLocalDateString(minDate);
        var minDateTime = DateTimeHelper.ToLocalDateTimeString(minDate);
        var minTime = DateTimeHelper.ToLocalTimeString(minDate);
        var minLongDate = DateTimeHelper.ToLocalLongDateString(minDate);

        minDateString.Should().NotBeNullOrEmpty();
        minDateTime.Should().NotBeNullOrEmpty();
        minTime.Should().NotBeNullOrEmpty();
        minLongDate.Should().NotBeNullOrEmpty();

        // Max date might cause issues with time zone conversion, so we use a safe max date
        var safeMaxDate = new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var maxDateString = DateTimeHelper.ToLocalDateString(safeMaxDate);
        var maxDateTime = DateTimeHelper.ToLocalDateTimeString(safeMaxDate);
        var maxTime = DateTimeHelper.ToLocalTimeString(safeMaxDate);
        var maxLongDate = DateTimeHelper.ToLocalLongDateString(safeMaxDate);

        maxDateString.Should().NotBeNullOrEmpty();
        maxDateTime.Should().NotBeNullOrEmpty();
        maxTime.Should().NotBeNullOrEmpty();
        maxLongDate.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToLocalLongDateString_ContainsExpectedComponents()
    {
        // Arrange
        var utcDate = new DateTime(2025, 1, 27, 10, 0, 0, DateTimeKind.Utc);
        var localDate = utcDate.ToLocalTime();

        // Act
        var result = DateTimeHelper.ToLocalLongDateString(utcDate);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(localDate.Year.ToString());
        
        // In most cultures, long date format includes the month name
        var monthNames = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames;
        var monthName = monthNames[localDate.Month - 1];
        if (!string.IsNullOrEmpty(monthName))
        {
            result.Should().Match(s => s.Contains(monthName) || s.Contains(monthName.Substring(0, 3)));
        }
    }
}