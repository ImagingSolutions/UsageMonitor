# UsageMonitor

UsageMonitor is a .NET library that provides API usage monitoring, rate limiting, and analytics capabilities for ASP.NET Core applications. It allows you to easily track API usage, implement rate limiting, and provide usage insights through a built-in dashboard.

## Features

- ⚡ Rate Limiting per API Client
- 📊 Usage Analytics Dashboard
- 📈 Request Logging
- 🕒 Response Time Tracking
- 💾 Multiple Database Support (SQLite, PostgreSQL, SQL Server)
- 🔍 Built-in Admin Interface
- 📱 Usage Monitoring UI

## Installation

```bash
dotnet add package UsageMonitor.Core
```

## Quick Start

1. Add UsageMonitor to your services in `Program.cs`:

```csharp
builder.Services.AddUsageMonitor(options =>
{
    options.ConnectionString = "Data Source=requests.db";
    options.DatabaseProvider = DatabaseProvider.SQLite;
});
```

2. Configure the middleware and endpoints:

```csharp
app.UseUsageMonitor();
app.MapUsageMonitorEndpoints();
app.MapUsageMonitorPages();
```

3. Add the `[MonitorUsage]` attribute to controllers or actions you want to monitor:

```csharp
[ApiController]
[Route("[controller]")]
[MonitorUsage]
public class WeatherForecastController : ControllerBase
{
    // Your controller code
}
```

## Configuration

### Database Providers

UsageMonitor supports SQLite, PostgreSQL, and SQL Server:

```csharp
// For SQLite
options.DatabaseProvider = DatabaseProvider.SQLite;
options.ConnectionString = "Data Source=usagemonitor.db";

// For PostgreSQL
options.DatabaseProvider = DatabaseProvider.PostgreSQL;
options.ConnectionString = "Host=localhost;Database=usagemonitor;Username=user;Password=password";

// For SQL Server
options.DatabaseProvider = DatabaseProvider.SQLServer;
options.ConnectionString = "Server=localhost;Database=usagemonitor;Trusted_Connection=True;TrustServerCertificate=True";
```

## Features

### Rate Limiting
- Configure usage limits per API client
- Automatic rate limit enforcement
- Monthly usage tracking

### Usage Monitoring
- Request/response logging
- Response time tracking
- Error rate monitoring
- Usage statistics

### Admin Dashboard
- View all API clients
- Manage usage limits
- Monitor API usage
- Generate usage reports

### Client Usage Page
- Self-service usage monitoring
- Usage statistics visualization
- Request history

## Security

- Secure admin interface with username/password authentication

## Example Implementation

Check out the `UsageMonitor.TestApi` project in the solution for a complete example of how to integrate and use the library.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
