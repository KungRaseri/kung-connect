using KungConnect.Shared.Enums;
using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace KungConnect.Agent.Input;

public static class InputInjectorFactory
{
    public static IInputInjector Create(ILoggerFactory loggerFactory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsInputInjector(loggerFactory.CreateLogger<WindowsInputInjector>());
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOsInputInjector(loggerFactory.CreateLogger<MacOsInputInjector>());
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxInputInjector(loggerFactory.CreateLogger<LinuxInputInjector>());

        throw new PlatformNotSupportedException("No input injector available for this OS.");
    }
}
