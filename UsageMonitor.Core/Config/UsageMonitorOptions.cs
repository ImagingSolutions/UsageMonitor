namespace UsageMonitor.Core.Config;

public class UsageMonitorOptions
{
    public string ConnectionString { get; set; }
    public DatabaseProvider DatabaseProvider { get; set; }
    public string? BrandingImageUrl { get; set; }
}

public enum DatabaseProvider
{
    SQLite,
    PostgreSQL,
    SQLServer
}
