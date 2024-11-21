
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UsageMonitor.Core.Config;
using UsageMonitor.Core.Models;
using UsageMonitor.Core.Services;


namespace UsageMonitor.Core.Middleware;

public class UsageMonitoringMiddlware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;
    private readonly UsageMonitorOptions _options;
    private static readonly Stopwatch _stopwatch = new();

    public UsageMonitoringMiddlware(
        RequestDelegate next,
        IServiceProvider serviceProvider,
        UsageMonitorOptions options)
    {
        _next = next;
        _serviceProvider = serviceProvider;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {

        if (!ShouldLog(context))
        {
            await _next(context);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var _usageService = scope.ServiceProvider.GetRequiredService<IUsageMonitorService>();

        var apiClient = await _usageService.GetApiClientAsync();
        if (apiClient == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Please create an admin account first" });
            return;
        }

        var totalCount = await _usageService.GetTotalRequestCountAsync();
        if (totalCount >= apiClient.UsageLimit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "Monthly API limit exceeded" });
            return;
        }

        var log = new RequestLog
        {
            RequestTime = DateTime.UtcNow,
            Path = context.Request.Path,
        };

        _stopwatch.Start();

        try
        {
            await _next(context);
            log.StatusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            log.StatusCode = 500;
            throw;
        }
        finally
        {
            log.Duration = _stopwatch.Elapsed.TotalSeconds;
            log.ApiClientId = apiClient.Id;
            await _usageService.LogRequestAsync(log);
            _stopwatch.Reset();
        }
    }

    private static bool ShouldLog(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null) return false;

        return endpoint.Metadata.GetMetadata<MonitorUsageAttribute>() != null;
    }
}
