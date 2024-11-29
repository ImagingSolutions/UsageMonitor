using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using UsageMonitor.Core.Config;
using UsageMonitor.Core.Middleware;

namespace UsageMonitor.Core.Extensions;

public static class AppBuilderExtensions
{

    public static IApplicationBuilder UseUsageMonitor(this IApplicationBuilder app)
    {
        app.UseAuthorization();
        app.UseDefaultFiles();
        app.UseSession();
        app.UseStaticFiles();
        var options = app.ApplicationServices.GetRequiredService<UsageMonitorOptions>();

        return app.UseMiddleware<UsageMonitoringMiddlware>(options);
    }

}