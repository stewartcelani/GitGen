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
}