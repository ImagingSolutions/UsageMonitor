using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UsageMonitor.Core.Services;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Extensions;

public static class UsageMonitorExtensions
{
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
            ApiClient clientData) =>
        {
            return await service.CreateApiClientAsync(clientData);

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
            var count = await service.GetTotalRequestCountAsync();
            return Results.Ok(new { count });
        }).ExcludeFromDescription();


        group.MapPatch("/client/payment", async (
            IUsageMonitorService service,
            UpdatePaymentRequest request) =>
        {
            var success = await service.AddClientPaymentAsync(request.AdditionalAmount);
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

        return endpoints;
    }

    public static IEndpointRouteBuilder MapUsageMonitorPages(this IEndpointRouteBuilder endpoints)
    {

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
