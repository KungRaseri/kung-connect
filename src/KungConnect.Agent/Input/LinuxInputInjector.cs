using KungConnect.Shared.Enums;
using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Input;

/// <summary>
/// Linux input injection via /dev/uinput kernel virtual device.
/// TODO: Phase 2 – open /dev/uinput, write uinput_user_dev + input_event structs.
/// </summary>
[SupportedOSPlatform("linux")]
public class LinuxInputInjector(ILogger<LinuxInputInjector> logger) : IInputInjector
{
    public void MoveMouse(int x, int y)      => logger.LogDebug("TODO Linux: MoveMouse({X},{Y})", x, y);
    public void MouseDown(MouseButton b)     => logger.LogDebug("TODO Linux: MouseDown({B})", b);
    public void MouseUp(MouseButton b)       => logger.LogDebug("TODO Linux: MouseUp({B})", b);
    public void Scroll(int dx, int dy)       => logger.LogDebug("TODO Linux: Scroll({Dx},{Dy})", dx, dy);
    public void KeyDown(int keyCode)         => logger.LogDebug("TODO Linux: KeyDown({K})", keyCode);
    public void KeyUp(int keyCode)           => logger.LogDebug("TODO Linux: KeyUp({K})", keyCode);
    public void TypeText(string text)        => logger.LogDebug("TODO Linux: TypeText");
}
