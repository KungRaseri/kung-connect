using System.Text.Json.Serialization;

namespace KungConnect.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionState
{
    Pending,
    Approved,
    Rejected,
    Active,
    Terminated
}
