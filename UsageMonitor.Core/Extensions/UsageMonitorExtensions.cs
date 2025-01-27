using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UsageMonitor.Core.Services;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Extensions;

public static class UsageMonitorExtensions
{
    private static string GetPagesPath()
    {
        var assembly = typeof(UsageMonitorExtensions).Assembly;
        var assemblyPath = Path.GetDirectoryName(assembly.Location);

        // Try multiple possible locations
        var possiblePaths = new[]
        {
            Path.Combine(assemblyPath!, "Pages"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pages")
        };

        return possiblePaths.First(path => Directory.Exists(path));
    }

    public static IEndpointRouteBuilder MapUsageMonitorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/usgm");

        group.MapGet("/logs", async (
            IUsageMonitorService service,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int pageSize = 20) =>
        {
            (IEnumerable<RequestLog> logs, int totalCount) = await service.GetPaginatedLogsAsync(from, to, page, pageSize);
            return Results.Ok(new { logs, totalCount, currentPage = page });

        }).ExcludeFromDescription();

        group.MapGet("/logs/errors", async (
            IUsageMonitorService service,
            DateTime? from,
            DateTime? to) =>
        {
            return await service.GetErrorLogsAsync(from, to);
        }).ExcludeFromDescription();


        group.MapPost("/client", async (
            IUsageMonitorService service,
            CreateNewClient clientData) =>
        {
            var newClient = await service.CreateApiClientAsync(clientData);

            return Results.Ok(newClient);

        }).ExcludeFromDescription();


        group.MapGet("/admin/exists", async (IUsageMonitorService service) =>
        {
            return await service.HasAdminAccountAsync();
        }).ExcludeFromDescription();

        group.MapPost("/admin/setup", async (
            IUsageMonitorService service,
            CreateAdminRequest request) =>
        {
            var success = await service.SetupAdminAccountAsync(request.Username, request.Password);
            if (!success)
                return Results.BadRequest(new { error = "Admin account already exists" });
            return Results.Ok();
        }).ExcludeFromDescription();


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

        }).ExcludeFromDescription();

        group.MapGet("/client", async (
           IUsageMonitorService service) =>
       {
           var client = await service.GetApiClientAsync();
           if (client == null) return Results.NotFound();
           return Results.Ok(client);
       }).ExcludeFromDescription();


        group.MapGet("/client/usage", async (
            IUsageMonitorService service) =>
        {
            var usage = await service.GetClientUsageAsync();
            return Results.Ok(usage);
        }).ExcludeFromDescription();


        group.MapPatch("/client/payment", async (
            IUsageMonitorService service,
            UpdatePaymentRequest request) =>
        {
            var success = await service.AddClientPaymentAsync(request.AdditionalAmount, request.unitPrice);
            if (!success)
                return Results.NotFound();
            return Results.Ok();
        }).ExcludeFromDescription();

        group.MapPatch("/client/", async (
            IUsageMonitorService service,
            ApiClient client) =>
        {
            var success = await service.UpdateClientAsync(client);
            if (!success)
                return Results.NotFound();
            return Results.Ok();

        }).ExcludeFromDescription();


        group.MapGet("/analytics/usage", [ApiExplorerSettings(IgnoreApi = true)] async (IUsageMonitorService service) =>
        {
            var stats = await service.GetMonthlyUsageAsync();
            return Results.Ok(stats);
        }).ExcludeFromDescription();

        group.MapGet("/client/report", async (
            IReportGenerationService reportService) =>
        {
            var pdfBytes = await reportService.GenerateClientUsageReportAsync();
            return Results.File(
                pdfBytes,
                "application/pdf",
                $"usage-report-{DateTime.Now:yyyyMMdd}.pdf");
        }).ExcludeFromDescription();

        group.MapGet("/payments", async (IUsageMonitorService service) =>
        {
            return await service.GetAllPaymentStatsAsync();
        }).ExcludeFromDescription();

        group.MapGet("/payments/{id}", async (IUsageMonitorService service, int id) =>
        {
            var stats = await service.GetPaymentUsageStatsAsync(id);
            if (stats == null) return Results.NotFound();
            return Results.Ok(stats);
        }).ExcludeFromDescription();

        // Analytics Overview
        group.MapGet("/analytics/overview", async (IUsageMonitorService service) =>
        {
            var overview = await service.GetAnalyticsOverviewAsync();
            return Results.Ok(overview);
        }).ExcludeFromDescription();

        // Timeline Stats
        group.MapGet("/analytics/timeline", async (
            IUsageMonitorService service,
            [FromQuery] int days = 30) =>
        {
            var timeline = await service.GetTimelineStatsAsync(days);
            return Results.Ok(timeline);
        }).ExcludeFromDescription();

        // Top Endpoints
        group.MapGet("/analytics/top-endpoints", async (
            IUsageMonitorService service,
            [FromQuery] int limit = 10) =>
        {
            var endpoints = await service.GetTopEndpointsAsync(limit);
            return Results.Ok(endpoints);
        }).ExcludeFromDescription();

        // Response Time Stats
        group.MapGet("/analytics/response-times", async (
            IUsageMonitorService service,
            [FromQuery] int days = 30) =>
        {
            var stats = await service.GetResponseTimeStatsAsync(days);
            return Results.Ok(stats);
        }).ExcludeFromDescription();

        // Status Distribution
        group.MapGet("/analytics/status-distribution", async (
            IUsageMonitorService service,
            [FromQuery] int days = 30) =>
        {
            var overview = await service.GetAnalyticsOverviewAsync();
            return Results.Ok(new
            {
                status2xx = overview.Status2xx,
                status4xx = overview.Status4xx,
                status5xx = overview.Status5xx
            });
        }).ExcludeFromDescription();

        group.MapGet("/reports/usage", async (
            IReportGenerationService reportService,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] bool detailed = false) =>
        {
            var pdfBytes = detailed
                ? await reportService.GenerateClientUsageReportAsync(startDate, endDate, true)
                : await reportService.GenerateUsageSummaryReportAsync(startDate, endDate);

            return Results.File(
                pdfBytes,
                "application/pdf",
                $"usage-report-{(detailed ? "detailed" : "summary")}-{DateTime.Now:yyyyMMdd}.pdf");
        }).ExcludeFromDescription();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapUsageMonitorPages(this IEndpointRouteBuilder endpoints)
    {
        var pagesPath = GetPagesPath();

        endpoints.MapGet("/usage", async context =>
        {
            var path = Path.Combine(pagesPath, "Usage.html");
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Usage page not found. Please ensure the package is installed correctly.");
                return;
            }
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

            var path = Path.Combine(pagesPath, "Admin.html");
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Admin page not found. Please ensure the package is installed correctly.");
                return;
            }
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        endpoints.MapGet("/login", async context =>
        {
            var path = Path.Combine(pagesPath, "Login.html");
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Login page not found. Please ensure the package is installed correctly.");
                return;
            }
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        // CSS Files
        endpoints.MapGet("/css/core.css", async context =>
        {
            var path = Path.Combine(pagesPath, "css", "core.css");
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = "text/css";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        endpoints.MapGet("/css/theme.css", async context =>
        {
            var path = Path.Combine(pagesPath, "css", "theme.css");
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = "text/css";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        // JavaScript Files
        endpoints.MapGet("/js/chart.js", async context =>
        {
            var path = Path.Combine(pagesPath, "js", "chart.js");
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = "application/javascript";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        endpoints.MapGet("/js/bootstrap.js", async context =>
        {
            var path = Path.Combine(pagesPath, "js", "bootstrap.js");
            if (!File.Exists(path))
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = "application/javascript";
            await context.Response.SendFileAsync(path);
        }).ExcludeFromDescription();

        return endpoints;
    }
}
