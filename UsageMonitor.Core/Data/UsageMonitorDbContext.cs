using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Data;

public class UsageMonitorDbContext : DbContext
{
    public UsageMonitorDbContext(DbContextOptions<UsageMonitorDbContext> options)
        : base(options)
    {

    }

    public DbSet<RequestLog> RequestLogs { get; set; }
    public DbSet<ApiClient> ApiClients { get; set; }
    public DbSet<Admin> Admins { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestTime).IsRequired();
            entity.Property(e => e.Path).IsRequired();
            entity.Property(e => e.Duration).IsRequired();
            entity.HasOne(e => e.ApiClient)
                    .WithMany(a => a.RequestLogs)
                    .HasForeignKey(e => e.ApiClientId)
                    .IsRequired();
        });

        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.AmountPaid).IsRequired();
            entity.Property(e => e.UnitPrice).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}