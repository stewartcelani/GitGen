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

    [Fact]
    public void IsWindows_ReturnsCorrectValue()
    {
        // Act
        var result = PlatformHelper.IsWindows();

        // Assert
        result.Should().Be(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }

    [Fact]
    public void IsMacOS_ReturnsCorrectValue()
    {
        // Act
        var result = PlatformHelper.IsMacOS();

        // Assert
        result.Should().Be(RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
    }

    [Fact]
    public void IsLinux_ReturnsCorrectValue()
    {
        // Act
        var result = PlatformHelper.IsLinux();

        // Assert
        result.Should().Be(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
    }

    [Fact]
    public void PlatformMethods_AreConsistent()
    {
        // Act
        var isWindows = PlatformHelper.IsWindows();
        var isMacOS = PlatformHelper.IsMacOS();
        var isLinux = PlatformHelper.IsLinux();
        var platformName = PlatformHelper.GetPlatformName();

        // Assert - Platform name should match boolean methods
        if (isWindows)
        {
            isMacOS.Should().BeFalse();
            isLinux.Should().BeFalse();
            platformName.Should().Be("Windows");
        }
        else if (isMacOS)
        {
            isWindows.Should().BeFalse();
            isLinux.Should().BeFalse();
            platformName.Should().Be("macOS");
        }
        else if (isLinux)
        {
            isWindows.Should().BeFalse();
            isMacOS.Should().BeFalse();
            platformName.Should().Be("Linux");
        }
        else
        {
            // This should not happen on supported platforms
            platformName.Should().Be("Unknown");
        }
    }

    [Fact]
    public void GetPlatformName_ReturnsUnknown_WhenNoPlatformMatches()
    {
        // This test validates the logic, even though "Unknown" should never occur
        // on supported platforms. The test ensures the method handles all cases.
        
        // Act
        var platformName = PlatformHelper.GetPlatformName();

        // Assert
        if (!PlatformHelper.IsWindows() && !PlatformHelper.IsMacOS() && !PlatformHelper.IsLinux())
        {
            platformName.Should().Be("Unknown");
        }
        else
        {
            platformName.Should().NotBe("Unknown");
        }
    }
}