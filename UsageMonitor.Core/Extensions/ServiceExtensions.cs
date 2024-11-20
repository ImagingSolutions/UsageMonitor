using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UsageMonitor.Core.Config;
using UsageMonitor.Core.Data;
using UsageMonitor.Core.Services;

namespace UsageMonitor.Core.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddUsageMonitor(this IServiceCollection services, Action<UsageMonitorOptions> configOptions)
    {
        var options = new UsageMonitorOptions();
        configOptions(options);

        services.AddSingleton(options);

        switch (options.DatabaseProvider)
        {
            case DatabaseProvider.SQLite:
                services.AddDbContext<UsageMonitorDbContext>((serviceProvider, opt) =>
                    opt.UseSqlite(options.ConnectionString));
                break;

            case DatabaseProvider.PostgreSQL:
                services.AddDbContext<UsageMonitorDbContext>((serviceProvider, opt) =>
                    opt.UseNpgsql(options.ConnectionString));
                break;

            default:
                throw new ArgumentException("Unsupported database provider");
        }

        services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IUsageMonitorService, UsageMonitorService>();

        services.AddDirectoryBrowser();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        initializer.Initialize();

        return services;
    }
}