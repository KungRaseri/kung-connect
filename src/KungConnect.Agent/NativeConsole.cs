#if WINDOWS
using System.Runtime.InteropServices;

namespace KungConnect.Agent;

/// <summary>
/// Manages a Win32 console window on demand.
/// Required by WinExe applications that need to show an interactive console temporarily
/// (e.g. the first-run setup wizard) without leaving a console open permanently.
/// </summary>
internal static class NativeConsole
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    /// <summary>
    /// Allocates a new console window (no-op if the process already has one)
    /// and re-attaches the standard streams so Console I/O works correctly.
    /// </summary>
    public static void Alloc()
    {
        bool allocated = AllocConsole();
        if (allocated)
        {
            // Re-connect the standard streams to the newly created console.
            Console.SetOut  (new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetIn   (new StreamReader(Console.OpenStandardInput()));
            Console.SetError(new StreamWriter(Console.OpenStandardError())  { AutoFlush = true });
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
    }

    /// <summary>Detaches (and closes) the current console window.</summary>
    public static void Free() => FreeConsole();
}
#endif
