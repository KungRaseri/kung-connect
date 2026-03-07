using System.Text.Json.Serialization;

namespace KungConnect.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MachineStatus
{
    Offline,
    Online,
    InSession,
    /// <summary>Agent was cleanly uninstalled. Machine record is retained; reinstall restores Online.</summary>
    Uninstalled
}
