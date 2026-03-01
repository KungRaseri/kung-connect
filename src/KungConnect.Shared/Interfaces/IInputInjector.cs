using KungConnect.Shared.Enums;

namespace KungConnect.Shared.Interfaces;

/// <summary>
/// Abstracts OS-specific keyboard and mouse injection.
/// Implementations exist for Windows (SendInput), macOS (CGEvent),
/// and Linux (uinput).
/// </summary>
public interface IInputInjector
{
    void MoveMouse(int x, int y);
    void MouseDown(MouseButton button);
    void MouseUp(MouseButton button);
    void Scroll(int deltaX, int deltaY);
    void KeyDown(int keyCode);
    void KeyUp(int keyCode);
    void TypeText(string text);
}
