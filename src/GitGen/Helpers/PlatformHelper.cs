using System.Runtime.InteropServices;

namespace GitGen.Helpers;

/// <summary>
///     Provides helper methods for determining the current operating system platform.
///     Used for platform-specific operations and configuration management.
/// </summary>
public static class PlatformHelper
{
    /// <summary>
    ///     Determines if the current operating system is Windows.
    /// </summary>
    /// <returns><c>true</c> if the OS is Windows; otherwise, <c>false</c>.</returns>
    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    /// <summary>
    ///     Determines if the current operating system is macOS.
    /// </summary>
    /// <returns><c>true</c> if the OS is macOS; otherwise, <c>false</c>.</returns>
    public static bool IsMacOS()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    /// <summary>
    ///     Determines if the current operating system is Linux.
    /// </summary>
    /// <returns><c>true</c> if the OS is Linux; otherwise, <c>false</c>.</returns>
    public static bool IsLinux()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    /// <summary>
    ///     Gets a user-friendly name for the current operating system platform.
    /// </summary>
    /// <returns>The name of the platform (e.g., "Windows", "macOS", "Linux", or "Unknown").</returns>
    public static string GetPlatformName()
    {
        if (IsWindows()) return "Windows";
        if (IsMacOS()) return "macOS";
        if (IsLinux()) return "Linux";
        return "Unknown";
    }
}