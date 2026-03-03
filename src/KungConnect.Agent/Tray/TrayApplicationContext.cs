#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KungConnect.Agent.Tray;

/// <summary>
/// Drives the Windows system tray icon for the KungConnect Agent.
/// Polls <see cref="AgentConnectionStatus"/> every second and updates the icon and
/// tooltip to reflect the current connection state.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IHost                   _host;
    private readonly AgentConnectionStatus   _status;
    private readonly NotifyIcon              _notifyIcon;
    private readonly ToolStripMenuItem       _statusItem;
    private readonly System.Windows.Forms.Timer _pollTimer;

    // Pre-built icons keyed by state — created once at startup and reused.
    private readonly Dictionary<AgentState, Icon> _icons = new()
    {
        [AgentState.Starting]     = MakeCircleIcon(Color.SteelBlue),
        [AgentState.Connecting]   = MakeCircleIcon(Color.DodgerBlue),
        [AgentState.Connected]    = MakeCircleIcon(Color.LimeGreen),
        [AgentState.Disconnected] = MakeCircleIcon(Color.DimGray),
    };

    private AgentState _lastState = (AgentState)(-1);

    public TrayApplicationContext(IHost host, AgentConnectionStatus status)
    {
        _host   = host;
        _status = status;

        _statusItem = new ToolStripMenuItem("Starting...") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("KungConnect Agent")
        {
            Enabled = false,
            Font    = new Font(SystemFonts.MenuFont ?? SystemFonts.DefaultFont, FontStyle.Bold),
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Icon             = _icons[AgentState.Starting],
            Text             = "KungConnect Agent",
            Visible          = true,
            ContextMenuStrip = menu,
        };

        // If the host stops itself (e.g. unhandled fatal error), exit the tray loop too.
        host.Services
            .GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopped
            .Register(() => Application.Exit());

        _pollTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _pollTimer.Tick += (_, _) => Poll();
        _pollTimer.Start();
    }

    // ── Polling ────────────────────────────────────────────────────────────────

    private void Poll()
    {
        var state = _status.State;
        if (state == _lastState) return;
        _lastState = state;

        var label = state switch
        {
            AgentState.Starting     => "Starting...",
            AgentState.Connecting   => "Connecting...",
            AgentState.Connected    => "Connected",
            AgentState.Disconnected => "Disconnected — retrying...",
            _                       => "Unknown",
        };

        _statusItem.Text = label;
        // NotifyIcon.Text is capped at 63 characters.
        _notifyIcon.Text = Truncate($"KungConnect Agent — {label}", 63);
        _notifyIcon.Icon = _icons.GetValueOrDefault(state, _icons[AgentState.Starting]);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    // ── Exit ───────────────────────────────────────────────────────────────────

    private async void OnExit(object? sender, EventArgs e)
    {
        _pollTimer.Stop();
        _notifyIcon.Visible = false;
        try { await _host.StopAsync(TimeSpan.FromSeconds(5)); } catch { /* best effort */ }
        ExitThread();
    }

    // ── Disposal ───────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Dispose();
            _notifyIcon.Dispose();
            foreach (var (_, icon) in _icons)
                FreeIconHandle(icon);
        }
        base.Dispose(disposing);
    }

    // ── Icon creation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a 16×16 coloured circle icon using GDI+.
    /// Icons created via <see cref="Icon.FromHandle"/> are NOT owned by the managed wrapper —
    /// call <see cref="FreeIconHandle"/> when you are done with them.
    /// </summary>
    private static Icon MakeCircleIcon(Color fill)
    {
        using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            // Subtle drop shadow
            using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillEllipse(shadow, 2, 3, 12, 12);
            // Main circle
            using (var body = new SolidBrush(fill))
                g.FillEllipse(body, 1, 1, 13, 13);
            // Highlight glint
            using (var glint = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
                g.FillEllipse(glint, 3, 2, 7, 5);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint handle);

    private static void FreeIconHandle(Icon icon)
    {
        nint handle = icon.Handle;
        icon.Dispose();
        DestroyIcon(handle);
    }
}
#endif
