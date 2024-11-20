
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
        var loggerService = scope.ServiceProvider.GetRequiredService<IUsageMonitorService>();

        var apiKey = context.Request.Headers[_options.ApiKeyHeader].ToString();
        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
            return;
        }

        // Check rate limit
        var apiClient = await loggerService.GetApiClientByKeyAsync(apiKey);
        if (apiClient == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        var totalCount = await loggerService.GetTotalRequestCountAsync(apiKey);
        if (totalCount >= apiClient.UsageLimit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "Monthly API limit exceeded" });
            return;
        }


        var log = new RequestLog
        {
            RequestTime = DateTime.UtcNow,
        };


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
            log.ResponseTime = DateTime.UtcNow;

            log.ApiKey = apiKey;
            log.ApiClientId = apiClient.Id;

            await loggerService.LogRequestAsync(log);
        }
    }

    private static bool ShouldLog(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null) return false;

        return endpoint.Metadata.GetMetadata<MonitorUsageAttribute>() != null;
    }
}
