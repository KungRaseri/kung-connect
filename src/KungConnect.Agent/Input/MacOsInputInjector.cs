using KungConnect.Shared.Enums;
using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Input;

/// <summary>
/// macOS input injection via CoreGraphics CGEvent P/Invoke.
/// TODO: Phase 2 – implement P/Invoke to CGEventCreateMouseEvent / CGEventPost.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOsInputInjector(ILogger<MacOsInputInjector> logger) : IInputInjector
{
    public void MoveMouse(int x, int y)      => logger.LogDebug("TODO macOS: MoveMouse({X},{Y})", x, y);
    public void MouseDown(MouseButton b)     => logger.LogDebug("TODO macOS: MouseDown({B})", b);
    public void MouseUp(MouseButton b)       => logger.LogDebug("TODO macOS: MouseUp({B})", b);
    public void Scroll(int dx, int dy)       => logger.LogDebug("TODO macOS: Scroll({Dx},{Dy})", dx, dy);
    public void KeyDown(int keyCode)         => logger.LogDebug("TODO macOS: KeyDown({K})", keyCode);
    public void KeyUp(int keyCode)           => logger.LogDebug("TODO macOS: KeyUp({K})", keyCode);
    public void TypeText(string text)        => logger.LogDebug("TODO macOS: TypeText");
}
