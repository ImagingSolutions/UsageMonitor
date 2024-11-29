using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Data;

public class UsageMonitorDbContext : DbContext
{
    public DbSet<Admin> Admins { get; set; } = null!;
    public DbSet<ApiClient> ApiClients { get; set; } = null!;
    public DbSet<RequestLog> RequestLogs { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    public UsageMonitorDbContext(DbContextOptions<UsageMonitorDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ApiClients table
        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.ToTable("ApiClients");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Payments table
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.UsedRequests)
                .HasDefaultValue(0);

            entity.Property(e => e.TotalRequests)
                .HasComputedColumnSql("CAST(FLOOR(Amount / UnitPrice) AS INT)", stored: true);

            entity.Property(e => e.RemainingRequests)
                .HasComputedColumnSql("CAST(FLOOR(Amount / UnitPrice) AS INT) - UsedRequests", stored: true);

            entity.Property(e => e.IsFullyUtilized)
                .HasComputedColumnSql("CAST(CASE WHEN UsedRequests >= CAST(FLOOR(Amount / UnitPrice) AS INT) THEN 1 ELSE 0 END AS BIT)", stored: true);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.ApiClient)
                .WithMany(a => a.Payments)
                .HasForeignKey(e => e.ApiClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RequestLogs table
        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.ToTable("RequestLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Path)
                .IsRequired();

            entity.Property(e => e.Method)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.StatusCode)
                .IsRequired();

            entity.Property(e => e.Duration)
                .IsRequired();

            entity.Property(e => e.RequestTime)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Payment)
                .WithMany(p => p.Requests)
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.PaymentId).HasDatabaseName("idx_requestlogs_paymentid");
        });

        // Admins table
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.ToTable("Admins");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Username)
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Indexes for Payments
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.ApiClientId)
            .HasDatabaseName("idx_payments_apiclientid");
    }
}