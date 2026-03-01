namespace KungConnect.Shared.DTOs.Join;

/// <summary>Response when a target machine or browser creates a join code.</summary>
public record CreateJoinCodeResponse(
    string Code,
    DateTimeOffset ExpiresAt);

/// <summary>Request sent by the operator client to join via a code.</summary>
public record ConnectWithCodeRequest(string Code, bool ViewOnly = false);
