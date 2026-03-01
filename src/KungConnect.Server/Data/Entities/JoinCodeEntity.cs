namespace KungConnect.Server.Data.Entities;

public class JoinCodeEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Human-friendly code shown to the target, e.g. "ABC-123-XYZ".</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>SignalR connection ID of the target that created this code.</summary>
    public string TargetConnectionId { get; set; } = string.Empty;
    /// <summary>Set once the operator connects.</summary>
    public string? OperatorConnectionId { get; set; }
    public bool IsConnected { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}
