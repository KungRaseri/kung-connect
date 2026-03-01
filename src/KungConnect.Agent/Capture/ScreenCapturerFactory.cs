using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Capture;

/// <summary>Selects the correct IScreenCapturer for the current OS at runtime.</summary>
public static class ScreenCapturerFactory
{
    public static IScreenCapturer Create(ILoggerFactory loggerFactory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsCapturer(loggerFactory.CreateLogger<WindowsCapturer>());
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOsCapturer(loggerFactory.CreateLogger<MacOsCapturer>());
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxCapturer(loggerFactory.CreateLogger<LinuxCapturer>());

        throw new PlatformNotSupportedException("No screen capturer available for this OS.");
    }
}
