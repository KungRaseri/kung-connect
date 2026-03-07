using KungConnect.Shared.Enums;
using KungConnect.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace KungConnect.Agent.Input;

/// <summary>
/// Windows input injection using Win32 SendInput API.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsInputInjector(ILogger<WindowsInputInjector> logger) : IInputInjector
{
    // ── Win32 P/Invoke ───────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint Type; public InputUnion Data; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy, mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE    = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE        = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;
    private const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP      = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN  = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP    = 0x0040;
    private const uint MOUSEEVENTF_WHEEL       = 0x0800;
    private const uint KEYEVENTF_KEYUP         = 0x0002;

    // ────────────────────────────────────────────────────────────────────────

    public void MoveMouse(int x, int y)
    {
        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);
        var input = new INPUT
        {
            Type = INPUT_MOUSE,
            Data = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = (int)(x * 65535.0 / screenW),
                    dy = (int)(y * 65535.0 / screenH),
                    dwFlags = (int)(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE)
                }
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    public void MouseDown(MouseButton button)  => SendMouseButton(button, down: true);
    public void MouseUp(MouseButton button)    => SendMouseButton(button, down: false);

    public void Scroll(int deltaX, int deltaY)
    {
        // deltaY is already in Win32 WHEEL_DELTA units (120 per notch) — do not scale further.
        var input = new INPUT
        {
            Type = INPUT_MOUSE,
            Data = new InputUnion { mi = new MOUSEINPUT { mouseData = deltaY, dwFlags = (int)MOUSEEVENTF_WHEEL } }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    public void KeyDown(int keyCode) => SendKey((ushort)keyCode, up: false);
    public void KeyUp(int keyCode)   => SendKey((ushort)keyCode, up: true);

    public void TypeText(string text)
    {
        foreach (var ch in text)
        {
            KeyDown(ch);
            KeyUp(ch);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SendMouseButton(MouseButton button, bool down)
    {
        uint flags = button switch
        {
            MouseButton.Left   => down ? MOUSEEVENTF_LEFTDOWN   : MOUSEEVENTF_LEFTUP,
            MouseButton.Right  => down ? MOUSEEVENTF_RIGHTDOWN  : MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => 0
        };
        var input = new INPUT
        {
            Type = INPUT_MOUSE,
            Data = new InputUnion { mi = new MOUSEINPUT { dwFlags = (int)flags } }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    private void SendKey(ushort vk, bool up)
    {
        var input = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Data = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = (ushort)(up ? KEYEVENTF_KEYUP : 0)
                }
            }
        };
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }
}
