using FluentAssertions;
using GitGen.Configuration;
using Xunit;

namespace GitGen.Tests.Configuration;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_NewInstance_HasCorrectDefaults()
    {
        // Arrange & Act
        var settings = new AppSettings();

        // Assert
        settings.ShowTokenUsage.Should().BeTrue();
        settings.CopyToClipboard.Should().BeTrue();
        settings.ConfigPath.Should().BeEmpty();
        settings.EnablePartialAliasMatching.Should().BeTrue();
        settings.MinimumAliasMatchLength.Should().Be(2);
        settings.RequirePromptConfirmation.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_RequirePromptConfirmation_DefaultsToTrue()
    {
        // This is a specific test to ensure the new feature defaults to true
        // Arrange & Act
        var settings = new AppSettings();

        // Assert
        settings.RequirePromptConfirmation.Should().BeTrue("RequirePromptConfirmation should default to true for safety");
    }

    [Fact]
    public void AppSettings_AllPropertiesCanBeSet()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        settings.ShowTokenUsage = false;
        settings.CopyToClipboard = false;
        settings.ConfigPath = "/test/path";
        settings.EnablePartialAliasMatching = false;
        settings.MinimumAliasMatchLength = 5;
        settings.RequirePromptConfirmation = false;

        // Assert
        settings.ShowTokenUsage.Should().BeFalse();
        settings.CopyToClipboard.Should().BeFalse();
        settings.ConfigPath.Should().Be("/test/path");
        settings.EnablePartialAliasMatching.Should().BeFalse();
        settings.MinimumAliasMatchLength.Should().Be(5);
        settings.RequirePromptConfirmation.Should().BeFalse();
    }
}