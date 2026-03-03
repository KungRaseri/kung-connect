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
        Console.WriteLine("  The agent will now start in the system tray.");
        Console.WriteLine("  Right-click the tray icon at any time to check status or exit.");
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
