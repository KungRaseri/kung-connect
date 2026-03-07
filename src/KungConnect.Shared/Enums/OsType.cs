using System.Text.Json.Serialization;

namespace KungConnect.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OsType
{
    Windows,
    MacOs,
    Linux,
    Browser
}
