namespace KungConnect.Shared.Enums;

public enum MachineStatus
{
    Offline,
    Online,
    InSession,
    /// <summary>Agent was cleanly uninstalled. Machine record is retained; reinstall restores Online.</summary>
    Uninstalled
}
