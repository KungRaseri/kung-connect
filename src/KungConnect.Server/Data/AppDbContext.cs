using KungConnect.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KungConnect.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity>    Users    { get; set; } = null!;
    public DbSet<MachineEntity> Machines { get; set; } = null!;
    public DbSet<SessionEntity> Sessions { get; set; } = null!;
    public DbSet<JoinCodeEntity> JoinCodes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<MachineEntity>(e =>
        {
            e.HasIndex(m => m.MachineSecret).IsUnique();
            e.HasOne(m => m.Owner)
             .WithMany(u => u.Machines)
             .HasForeignKey(m => m.OwnerId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.HasOne(s => s.Machine)
             .WithMany(m => m.Sessions)
             .HasForeignKey(s => s.MachineId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.RequestedBy)
             .WithMany(u => u.Sessions)
             .HasForeignKey(s => s.RequestedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JoinCodeEntity>(e =>
        {
            e.HasIndex(j => j.Code).IsUnique();
        });
    }
}
