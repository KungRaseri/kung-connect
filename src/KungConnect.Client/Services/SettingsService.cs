using System.Text.Json;

namespace KungConnect.Client.Services;

/// <summary>
/// Persists user-scoped application settings to
/// <c>%APPDATA%\KungConnect\settings.json</c> (or platform equivalent).
/// All methods are synchronous and swallow I/O errors — this is a best-effort store.
/// </summary>
public static class SettingsService
{
    private static readonly string _dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KungConnect");

    private static readonly string _path = Path.Combine(_dir, "settings.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), _opts) ?? new();
        }
        catch { /* ignore corrupt/missing file */ }
        return new();
    }

    public static void Save(string serverUrl)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(new AppSettings { ServerUrl = serverUrl }, _opts));
        }
        catch { /* non-fatal */ }
    }
}

public record AppSettings
{
    public string? ServerUrl { get; init; }
}
