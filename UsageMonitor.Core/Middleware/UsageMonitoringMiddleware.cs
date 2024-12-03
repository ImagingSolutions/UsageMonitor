using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UsageMonitor.Core.Config;
using UsageMonitor.Core.Models;
using UsageMonitor.Core.Services;

namespace UsageMonitor.Core.Middleware;

public class UsageMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;
    private readonly UsageMonitorOptions _options;
    private static readonly Stopwatch _stopwatch = new();

    public UsageMonitoringMiddleware(
        RequestDelegate next,
        IServiceProvider serviceProvider,
        UsageMonitorOptions options)
    {
        _next = next;
        _serviceProvider = serviceProvider;
        _options = options;
    }

    private bool IsBusinessException(Exception ex)
    {
        // // First check the predicate if provided
        // if (_options.BusinessExceptionPredicate != null)
        // {
        //     return _options.BusinessExceptionPredicate(ex);
        // }

        return _options.BusinessExceptions.Any(e => e.GetType() == ex.GetType());
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

        var hasAvailableRequests = await _usageService.HasAvailableRequestsAsync();
        if (!hasAvailableRequests)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "No active payment found or request limit reached. Please add more credits to continue.",
                details = "Contact administrator to increase your usage limit."
            });
            return;
        }

        var log = new RequestLog
        {
            RequestTime = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method
        };

        _stopwatch.Start();
        var originalBodyStream = context.Response.Body;

        try
        {
            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            await _next(context);

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);

            log.StatusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            var isBusinessException = IsBusinessException(ex);

            log.StatusCode = isBusinessException
                ? StatusCodes.Status200OK
                : StatusCodes.Status400BadRequest;

            throw;
        }
        finally
        {
            _stopwatch.Stop();
            log.Duration = _stopwatch.Elapsed.TotalSeconds;
            context.Response.Body = originalBodyStream;

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
