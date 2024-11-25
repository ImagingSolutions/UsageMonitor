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
                        Name TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS Payments (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ApiClientId INTEGER NOT NULL,
                        Amount DECIMAL(18,2) NOT NULL,
                        UnitPrice DECIMAL(18,2) NOT NULL,
                        UsedRequests INTEGER DEFAULT 0,
                        TotalRequests INTEGER GENERATED ALWAYS AS (CAST(Amount / UnitPrice AS INTEGER)) STORED,
                        RemainingRequests INTEGER GENERATED ALWAYS AS (
                            CAST(Amount / UnitPrice AS INTEGER) - UsedRequests
                        ) STORED,
                        IsFullyUtilized INTEGER GENERATED ALWAYS AS (
                            CASE WHEN UsedRequests >= CAST(Amount / UnitPrice AS INTEGER) THEN 1 ELSE 0 END
                        ) STORED,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ApiClientId) REFERENCES ApiClients(Id) ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS RequestLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ApiClientId INTEGER NOT NULL,
                        PaymentId INTEGER,
                        Path TEXT NOT NULL,
                        Method TEXT NOT NULL,
                        StatusCode INTEGER NOT NULL,
                        Duration INTEGER NOT NULL,
                        RequestTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ApiClientId) REFERENCES ApiClients(Id) ON DELETE CASCADE,
                        FOREIGN KEY (PaymentId) REFERENCES Payments(Id) ON DELETE SET NULL
                    );

                    CREATE TABLE IF NOT EXISTS Admins (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE INDEX IF NOT EXISTS idx_requestlogs_apiclientid ON RequestLogs(ApiClientId);
                    CREATE INDEX IF NOT EXISTS idx_requestlogs_paymentid ON RequestLogs(PaymentId);
                    CREATE INDEX IF NOT EXISTS idx_payments_apiclientid ON Payments(ApiClientId);";

            case DatabaseProvider.PostgreSQL:
                return @"
                    CREATE TABLE IF NOT EXISTS ""ApiClients"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Name"" TEXT NOT NULL,
                        ""Email"" TEXT NOT NULL,
                        ""CreatedAt"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS ""Payments"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""ApiClientId"" INTEGER NOT NULL,
                        ""Amount"" DECIMAL(18,2) NOT NULL,
                        ""UnitPrice"" DECIMAL(18,2) NOT NULL,
                        ""UsedRequests"" INTEGER DEFAULT 0,
                        ""TotalRequests"" INTEGER GENERATED ALWAYS AS (
                            FLOOR(""Amount"" / ""UnitPrice"")::INTEGER
                        ) STORED,
                        ""RemainingRequests"" INTEGER GENERATED ALWAYS AS (
                            FLOOR(""Amount"" / ""UnitPrice"")::INTEGER - ""UsedRequests""
                        ) STORED,
                        ""IsFullyUtilized"" BOOLEAN GENERATED ALWAYS AS (
                            ""UsedRequests"" >= FLOOR(""Amount"" / ""UnitPrice"")::INTEGER
                        ) STORED,
                        ""CreatedAt"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT ""FK_Payments_ApiClients"" FOREIGN KEY (""ApiClientId"") 
                            REFERENCES ""ApiClients""(""Id"") ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS ""RequestLogs"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""ApiClientId"" INTEGER NOT NULL,
                        ""PaymentId"" INTEGER,
                        ""Path"" TEXT NOT NULL,
                        ""Method"" TEXT NOT NULL,
                        ""StatusCode"" INTEGER NOT NULL,
                        ""Duration"" BIGINT NOT NULL,
                        ""RequestTime"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT ""FK_RequestLogs_ApiClients"" FOREIGN KEY (""ApiClientId"") 
                            REFERENCES ""ApiClients""(""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_RequestLogs_Payments"" FOREIGN KEY (""PaymentId"") 
                            REFERENCES ""Payments""(""Id"") ON DELETE SET NULL
                    );

                    CREATE TABLE IF NOT EXISTS ""Admins"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Username"" TEXT NOT NULL UNIQUE,
                        ""PasswordHash"" TEXT NOT NULL,
                        ""CreatedAt"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE INDEX IF NOT EXISTS idx_requestlogs_apiclientid ON ""RequestLogs""(""ApiClientId"");
                    CREATE INDEX IF NOT EXISTS idx_requestlogs_paymentid ON ""RequestLogs""(""PaymentId"");
                    CREATE INDEX IF NOT EXISTS idx_payments_apiclientid ON ""Payments""(""ApiClientId"");";

            case DatabaseProvider.SQLServer:
                return @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApiClients')
                    BEGIN
                        CREATE TABLE ApiClients (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Name NVARCHAR(255) NOT NULL,
                            Email NVARCHAR(255) NOT NULL,
                            CreatedAt DATETIME2 DEFAULT GETUTCDATE()
                        );
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payments')
                    BEGIN
                        CREATE TABLE Payments (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ApiClientId INT NOT NULL,
                            Amount DECIMAL(18,2) NOT NULL,
                            UnitPrice DECIMAL(18,2) NOT NULL,
                            UsedRequests INT DEFAULT 0,
                            TotalRequests AS CAST(Amount / UnitPrice AS INT) PERSISTED,
                            RemainingRequests AS (
                                CAST(Amount / UnitPrice AS INT) - UsedRequests
                            ) PERSISTED,
                            IsFullyUtilized AS (
                                CASE WHEN UsedRequests >= CAST(Amount / UnitPrice AS INT) 
                                THEN 1 ELSE 0 END
                            ) PERSISTED,
                            CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_Payments_ApiClients FOREIGN KEY (ApiClientId) 
                                REFERENCES ApiClients(Id) ON DELETE CASCADE
                        );
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RequestLogs')
                    BEGIN
                        CREATE TABLE RequestLogs (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ApiClientId INT NOT NULL,
                            PaymentId INT,
                            Path NVARCHAR(MAX) NOT NULL,
                            Method NVARCHAR(10) NOT NULL,
                            StatusCode INT NOT NULL,
                            Duration BIGINT NOT NULL,
                            RequestTime DATETIME2 DEFAULT GETUTCDATE(),
                            CONSTRAINT FK_RequestLogs_ApiClients FOREIGN KEY (ApiClientId) 
                                REFERENCES ApiClients(Id) ON DELETE CASCADE,
                            CONSTRAINT FK_RequestLogs_Payments FOREIGN KEY (PaymentId) 
                                REFERENCES Payments(Id) ON DELETE SET NULL
                        );
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Admins')
                    BEGIN
                        CREATE TABLE Admins (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Username NVARCHAR(450) NOT NULL UNIQUE,
                            PasswordHash NVARCHAR(MAX) NOT NULL,
                            CreatedAt DATETIME2 DEFAULT GETUTCDATE()
                        );
                    END

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestlogs_apiclientid')
                        CREATE INDEX idx_requestlogs_apiclientid ON RequestLogs(ApiClientId);

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requestlogs_paymentid')
                        CREATE INDEX idx_requestlogs_paymentid ON RequestLogs(PaymentId);

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_payments_apiclientid')
                        CREATE INDEX idx_payments_apiclientid ON Payments(ApiClientId);";

            default:
                throw new ArgumentException("Unsupported database provider");
        }
    }

}