using System.Text.Json;
using System.Text.Json.Nodes;

namespace KungConnect.Agent.Setup;

/// <summary>
/// Interactive first-run setup wizard.
/// Prompts for the server URL and machine name, tests connectivity,
/// then writes the configuration to appsettings.json.
/// Only runs when Agent.ServerUrl is not yet configured.
/// </summary>
internal static class AgentInstaller
{
    public static async Task RunAsync(string settingsPath)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Println();
        PrintLine(ConsoleColor.Cyan,   "  ╔══════════════════════════════════════════════════════╗");
        PrintLine(ConsoleColor.Cyan,   "  ║       KungConnect Agent  ·  First-Run Setup          ║");
        PrintLine(ConsoleColor.Cyan,   "  ╚══════════════════════════════════════════════════════╝");
        Println();
        Console.WriteLine("  Welcome! This wizard will connect the agent to your KungConnect server.");
        Console.WriteLine("  Settings are saved to appsettings.json — you won't be asked again.");
        Println();

        // ── Server URL ────────────────────────────────────────────────────────
        var serverUrl = await PromptServerUrlAsync();

        // ── Machine name ──────────────────────────────────────────────────────
        var defaultAlias = Environment.MachineName;
        Console.Write($"  Machine name [{defaultAlias}]: ");
        var alias = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(alias)) alias = defaultAlias;

        // ── Persist ───────────────────────────────────────────────────────────
        PersistSettings(settingsPath, serverUrl, alias);

        Println();
        PrintLine(ConsoleColor.Green, "  ✓ Configuration saved.");
        Println();

        // ── Service install ───────────────────────────────────────────────────
        await PromptServiceInstallAsync();
    }

    private static async Task PromptServiceInstallAsync()
    {
        Console.Write("  Install as a background service (starts automatically)? [Y/n]: ");
        var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
        if (answer == "N")
        {
            Println();
#if WINDOWS
            Console.WriteLine("  Skipped — the agent will start in the system tray when launched manually.");
#else
            Console.WriteLine("  Skipped — start the agent manually or set up a service later.");
#endif
            Println();
            Console.Write("  Press any key to continue...");
            Console.ReadKey(intercept: true);
            Println();
            return;
        }

        Console.Write("  Installing service... ");
        var exePath = Environment.ProcessPath
                   ?? Path.Combine(AppContext.BaseDirectory, "KungConnect.Agent");
        var result = await ServiceInstaller.InstallAsync(exePath);

        if (result.Success)
            PrintLine(ConsoleColor.Green,  $"✓  {result.Message}");
        else
            PrintLine(ConsoleColor.Yellow, $"✗  {result.Message}");

        Println();
        Console.Write("  Press any key to continue...");
        Console.ReadKey(intercept: true);
        Println();
    }

    private static async Task<string> PromptServerUrlAsync()
    {
        while (true)
        {
            Console.Write("  Server URL (e.g. https://my-server.com): ");
            var input = (Console.ReadLine() ?? "").Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(input))
            {
                PrintLine(ConsoleColor.Yellow, "  ✗  URL cannot be empty.");
                continue;
            }

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                PrintLine(ConsoleColor.Yellow, "  ✗  Must be a valid http:// or https:// URL.");
                continue;
            }

            Console.Write("  Connecting to server... ");
            var (reachable, error) = await PingAsync(input);
            if (reachable)
            {
                PrintLine(ConsoleColor.Green, "✓");
                return input;
            }

            PrintLine(ConsoleColor.Yellow, $"✗  {error}");
            Console.Write("  Use this URL anyway? [y/N]: ");
            var answer = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            if (answer == "Y") return input;

            Println();
        }
    }

    private static async Task<(bool ok, string error)> PingAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            // Any HTTP response (including 404) confirms the server is reachable.
            await http.GetAsync($"{url}/health");
            return (true, "");
        }
        catch (HttpRequestException ex) { return (false, ex.Message); }
        catch (TaskCanceledException)   { return (false, "Connection timed out"); }
        catch (Exception ex)            { return (false, ex.Message); }
    }

    /// <summary>
    /// Writes server URL and machine alias directly to appsettings.json.
    /// Called by platform installers via the <c>--configure</c> CLI flag
    /// so they can write config without running the interactive wizard.
    /// </summary>
    public static void WriteSettings(string settingsPath, string serverUrl, string machineAlias)
        => PersistSettings(settingsPath, serverUrl, machineAlias);

    /// <summary>
    /// Called by platform uninstallers (MSI CA_NotifyUninstall) just before files are removed.
    /// Reads the current config, POSTs to the server to set the machine status to Uninstalled,
    /// and returns silently — any failure is logged but does not block the uninstall.
    /// </summary>
    public static async Task NotifyUninstallAsync(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            var root   = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
            var agent  = root?["Agent"] as JsonObject;
            var serverUrl     = agent?["ServerUrl"]?.GetValue<string>() ?? "";
            var machineSecret = agent?["MachineSecret"]?.GetValue<string>() ?? "";

            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(machineSecret))
            {
                Console.WriteLine("[KungConnect Agent] notify-uninstall: no config found, skipping.");
                return;
            }

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url     = $"{serverUrl.TrimEnd('/')}/api/machines/notify-uninstall";
            var payload = System.Text.Json.JsonSerializer.Serialize(new { MachineSecret = machineSecret });
            var content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var resp = await http.PostAsync(url, content);
            Console.WriteLine($"[KungConnect Agent] notify-uninstall: server responded {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            // Best-effort — never block the uninstall
            Console.WriteLine($"[KungConnect Agent] notify-uninstall failed (non-fatal): {ex.Message}");
        }
    }

    private static void PersistSettings(string path, string serverUrl, string alias)
    {
        JsonObject root;
        try
        {
            root = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch { root = new JsonObject(); }

        if (root["Agent"] is not JsonObject agent)
        {
            agent = new JsonObject();
            root["Agent"] = agent;
        }

        agent["ServerUrl"]    = serverUrl;
        agent["MachineAlias"] = alias;
        // MachineSecret is intentionally left empty here —
        // Worker auto-generates a stable identity on first connect.

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void PrintLine(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private static void Println() => Console.WriteLine();
}
