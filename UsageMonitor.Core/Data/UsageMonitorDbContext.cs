using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Data;

public class UsageMonitorDbContext : DbContext, IUsageMonitorDbContext
{
    public DbSet<Admin> Admins { get; set; } = null!;
    public DbSet<ApiClient> ApiClients { get; set; } = null!;
    public DbSet<RequestLog> RequestLogs { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    public UsageMonitorDbContext(DbContextOptions<UsageMonitorDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ApiClient configuration
        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Configure relationship with Payments
            entity.HasMany(e => e.Payments)
                  .WithOne(p => p.ApiClient)
                  .HasForeignKey(p => p.ApiClientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UsedRequests).HasDefaultValue(0);

            // Configure relationship with RequestLogs
            entity.HasMany(e => e.Requests)
                  .WithOne(r => r.Payment)
                  .HasForeignKey(r => r.PaymentId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // RequestLog configuration
        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).IsRequired();
            entity.Property(e => e.Method).IsRequired();
            entity.Property(e => e.RequestTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Configure relationship with ApiClient
            entity.HasOne(e => e.ApiClient)
                  .WithMany(c => c.RequestLogs)
                  .HasForeignKey(e => e.ApiClientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Admin configuration
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
        });
    }
}