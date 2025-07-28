using System.Runtime.InteropServices;
using FluentAssertions;
using GitGen.Helpers;
using Xunit;

namespace GitGen.Tests.Helpers;

public class PlatformHelperTests
{
    [Fact]
    public void PlatformDetection_ReturnsCorrectPlatform()
    {
        // Act
        var isWindows = PlatformHelper.IsWindows();
        var isMacOS = PlatformHelper.IsMacOS();
        var isLinux = PlatformHelper.IsLinux();

        // Assert - Exactly one should be true
        var platformCount = new[] { isWindows, isMacOS, isLinux }.Count(p => p);
        platformCount.Should().Be(1, "exactly one platform should be detected");

        // Verify against RuntimeInformation
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            isWindows.Should().BeTrue();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            isMacOS.Should().BeTrue();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            isLinux.Should().BeTrue();
    }

    [Fact]
    public void GetPlatformName_ReturnsExpectedName()
    {
        // Act
        var platformName = PlatformHelper.GetPlatformName();

        // Assert
        platformName.Should().NotBeNullOrEmpty();
        platformName.Should().BeOneOf("Windows", "macOS", "Linux", "Unknown");

        // Verify it matches the detection methods
        if (PlatformHelper.IsWindows())
            platformName.Should().Be("Windows");
        else if (PlatformHelper.IsMacOS())
            platformName.Should().Be("macOS");
        else if (PlatformHelper.IsLinux())
            platformName.Should().Be("Linux");
    }
}