using Microsoft.EntityFrameworkCore;
using UsageMonitor.Core.Config;

namespace UsageMonitor.Core.Data;

public interface IDatabaseInitializer
{
    void Initialize();

}

public class DatabaseInitializer : IDatabaseInitializer
{

    private readonly UsageMonitorDbContext _context;
    private readonly UsageMonitorOptions _options;

    public DatabaseInitializer(UsageMonitorDbContext context, UsageMonitorOptions options)
    {
        _context = context;
        _options = options;
    }

    public void Initialize()
    {
        _context.Database.EnsureCreated();

        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = GetInitialMigrationScript(_options.DatabaseProvider);

        if (_context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            _context.Database.GetDbConnection().Open();

        try
        {
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Migration error: {ex.Message}");
        }
        
    }

    private static string GetInitialMigrationScript(DatabaseProvider provider)
    {
        switch (provider)
        {
            case DatabaseProvider.SQLite:
                return @"
                    CREATE TABLE IF NOT EXISTS ApiClients (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ApiKey TEXT NOT NULL,
                        Name TEXT NOT NULL,
                        Email TEXT,
                        CreatedAt DATETIME NOT NULL,
                        UsageLimit INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS RequestLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ApiKey TEXT NOT NULL,
                        StatusCode INTEGER NOT NULL,
                        RequestTime DATETIME NOT NULL,
                        ResponseTime DATETIME NOT NULL,
                        ApiClientId INTEGER NOT NULL,
                        FOREIGN KEY (ApiClientId) REFERENCES ApiClients(Id)
                    );

                    CREATE TABLE IF NOT EXISTS Admins (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        CreatedAt DATETIME NOT NULL
                    );";

            case DatabaseProvider.PostgreSQL:
                return @"
                    CREATE TABLE IF NOT EXISTS ""ApiClients"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""ApiKey"" TEXT NOT NULL,
                        ""Name"" TEXT NOT NULL,
                        ""Email"" TEXT,
                        ""CreatedAt"" TIMESTAMP NOT NULL,
                        ""UsageLimit"" INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS ""RequestLogs"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""ApiKey"" TEXT NOT NULL,
                        ""StatusCode"" INTEGER NOT NULL,
                        ""RequestTime"" TIMESTAMP NOT NULL,
                        ""ResponseTime"" TIMESTAMP NOT NULL,
                        ""ApiClientId"" INTEGER NOT NULL,
                        FOREIGN KEY (""ApiClientId"") REFERENCES ""ApiClients""(""Id"")
                    );

                    CREATE TABLE IF NOT EXISTS ""Admins"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Username"" TEXT NOT NULL UNIQUE,
                        ""PasswordHash"" TEXT NOT NULL,
                        ""CreatedAt"" TIMESTAMP NOT NULL
                    );";

            default:
                throw new ArgumentException("Unsupported database provider");
        }
    }

}