using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UsageMonitor.Core.Config;
using UsageMonitor.Core.Data;
using UsageMonitor.Core.Models;
using UsageMonitor.Core.Services;

namespace UsageMonitor.Core.Extensions;

public static class UsageMonitorExtensions
{
    public static IEndpointRouteBuilder MapUsageMonitorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/usage-monitor");

        // Logs endpoints
        group.MapGet("/logs", async (
            IUsageMonitorService service,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int pageSize = 20) =>
        {
            (IEnumerable<RequestLog> logs, int totalCount) = await service.GetLogsAsync(from, to, page, pageSize);
            return Results.Ok(new { logs, totalCount, currentPage = page });
        });

        group.MapGet("/logs/errors", async (
            IUsageMonitorService service,
            DateTime? from,
            DateTime? to) =>
        {
            return await service.GetErrorLogsAsync(from, to);
        });

        // API Client endpoints
        group.MapGet("/clients", async (IUsageMonitorService service) =>
        {
            return await service.GetApiClientsAsync();
        });

        group.MapPost("/clients", async (
            IUsageMonitorService service,
            ApiClient client) =>
        {
            return await service.CreateApiClientAsync(client);
        });

        // Admin Authentication endpoints
        group.MapGet("/admin/exists", async (IUsageMonitorService service) =>
        {
            return await service.HasAdminAccountAsync();
        });

        group.MapPost("/admin/setup", async (
            IUsageMonitorService service,
            AdminSetupRequest request) =>
        {
            var success = await service.SetupAdminAccountAsync(request.Username, request.Password);
            if (!success)
                return Results.BadRequest(new { error = "Admin account already exists" });
            return Results.Ok();
        });

        group.MapPost("/admin/login", async (
            IUsageMonitorService service,
            AdminLoginRequest request,
            HttpContext context) =>
        {
            var isValid = await service.ValidateAdminLoginAsync(request.Username, request.Password);
            if (!isValid)
                return Results.Unauthorized();

            context.Session.SetString("AdminAuthenticated", "true");
            return Results.Ok();
        });

        // Client management endpoints
        group.MapGet("/clients/{apiKey}/usage", async (
            IUsageMonitorService service,
            string apiKey) =>
        {
            var count = await service.GetTotalRequestCountAsync(apiKey);
            return Results.Ok(new { count });
        });

        group.MapPut("/clients/{id}/limit", async (
            IUsageMonitorService service,
            int id,
            UpdateLimitRequest request) =>
        {
            var success = await service.UpdateClientLimitAsync(id, request.UsageLimit);
            if (!success)
                return Results.NotFound();
            return Results.Ok();
        });

        group.MapPut("/clients/{id}", async (
            IUsageMonitorService service,
            int id,
            ApiClient client) =>
        {
            var success = await service.UpdateClientAsync(id, client);
            if (!success)
                return Results.NotFound();
            return Results.Ok();
        });

        // Analytics endpoints
        group.MapGet("/analytics/usage", async (IUsageMonitorService service) =>
        {
            var stats = await service.GetMonthlyUsageStatsAsync();
            return Results.Ok(stats);
        });

        group.MapGet("/analytics/errors", async (IUsageMonitorService service) =>
        {
            var stats = await service.GetErrorRatesAsync();
            return Results.Ok(stats);
        });

        // API endpoints continue here...

        return endpoints;
    }

    public static IEndpointRouteBuilder MapUsageMonitorPages(this IEndpointRouteBuilder endpoints)
    {
        // HTML Pages
        endpoints.MapGet("/usage", async context =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "Usage.html");
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        endpoints.MapGet("/admin", async context =>
        {
            var isAuthenticated = context.Session.GetString("AdminAuthenticated");
            if (string.IsNullOrEmpty(isAuthenticated))
            {
                context.Response.Redirect("/login");
                return;
            }

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "Admin.html");
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        endpoints.MapGet("/login", async context =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "Login.html");
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        // CSS Files
        endpoints.MapGet("/css/core.css", async context =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "css", "core.css");
            context.Response.ContentType = "text/css";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        endpoints.MapGet("/css/theme.css", async context =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "css", "theme.css");
            context.Response.ContentType = "text/css";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        // JavaScript Files
        endpoints.MapGet("/js/chart.js", async context =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "js", "chart.js");
            context.Response.ContentType = "application/javascript";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        endpoints.MapGet("/js/bootstrap.js", async context =>
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages", "js", "bootstrap.js");
            context.Response.ContentType = "application/javascript";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();



        return endpoints;
    }
}
