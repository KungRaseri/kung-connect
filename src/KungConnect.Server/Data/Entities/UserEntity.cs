namespace KungConnect.Server.Data.Entities;

public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string[] Roles { get; set; } = ["operator"];
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    // Navigation
    public ICollection<MachineEntity> Machines { get; set; } = [];
    public ICollection<SessionEntity> Sessions { get; set; } = [];
}
