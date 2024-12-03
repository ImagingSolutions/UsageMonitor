namespace UsageMonitor.Core.Config;
using System;
using System.Collections.Generic;

public class UsageMonitorOptions
{
    public string ConnectionString { get; set; }
    public DatabaseProvider DatabaseProvider { get; set; }
    public string? BrandingImageUrl { get; set; }
    public List<Exception> BusinessExceptions { get; set; } = [];
    // public Func<Exception, bool>? BusinessExceptionPredicate { get; set; }
}

public enum DatabaseProvider
{
    SQLite,
    PostgreSQL,
    SQLServer
}
