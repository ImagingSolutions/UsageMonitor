namespace UsageMonitor.Core.Config;

public class UsageMonitorOptions
{
    public string ConnectionString { get; set; }
    public DatabaseProvider DatabaseProvider { get; set; }
    public string ApiKeyHeader { get; set; } = "X-API-Key";
}

public enum DatabaseProvider
{
    SQLite,
    PostgreSQL
}
