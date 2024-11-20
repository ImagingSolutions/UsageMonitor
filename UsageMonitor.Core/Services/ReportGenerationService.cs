using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Services;

public interface IReportGenerationService
{
    Task<byte[]> GenerateClientUsageReportAsync(string apiKey, DateTime? startDate = null, DateTime? endDate = null);
}

public class ReportGenerationService : IReportGenerationService
{
    private readonly IUsageMonitorService _usageMonitorService;

    public ReportGenerationService(IUsageMonitorService usageMonitorService)
    {
        _usageMonitorService = usageMonitorService;
    }

    public async Task<byte[]> GenerateClientUsageReportAsync(string apiKey, DateTime? startDate = null, DateTime? endDate = null)
    {
        var client = await _usageMonitorService.GetApiClientByKeyAsync(apiKey);
        if (client == null) throw new ArgumentException("Invalid API key");

        // Validate date range
        if (startDate.HasValue && startDate.Value < client.CreatedAt)
        {
            startDate = client.CreatedAt;
        }
        
        if (!startDate.HasValue)
        {
            startDate = DateTime.UtcNow.AddMonths(-1);
        }
        
        if (!endDate.HasValue)
        {
            endDate = DateTime.UtcNow;
        }

        var totalRequests = await _usageMonitorService.GetTotalRequestCountAsync(apiKey);
        var logs = await _usageMonitorService.GetRequestLogsAsync(apiKey, startDate.Value, endDate.Value);

        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeContent(content, client, totalRequests, logs, startDate.Value, endDate.Value));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("API Usage Report")
                    .FontSize(20)
                    .SemiBold();
                column.Item().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                    .FontSize(12);
            });
        });
    }

    private void ComposeContent(IContainer container, ApiClient client, int totalRequests, IEnumerable<RequestLog> logs, DateTime startDate, DateTime endDate)
    {
        container.Column(column =>
        {
            // Client Summary Section
            column.Item().Padding(10).Column(summary =>
            {
                summary.Item().Text("Client Summary").FontSize(16).SemiBold();
                summary.Item().Text($"Client: {client.Name}").FontSize(12);
                summary.Item().Text($"Email: {client.Email}");
                summary.Item().Text($"Total Requests: {totalRequests}");
                summary.Item().Text($"Usage Limit: {client.UsageLimit}");
                summary.Item().Text($"Utilization: {(totalRequests * 100.0 / client.UsageLimit):F1}%");
            });

            // Report Period
            column.Item().Padding(10).Column(period =>
            {
                period.Item().Text("Report Period").FontSize(16).SemiBold();
                period.Item().Text($"From: {startDate:yyyy-MM-dd HH:mm:ss}");
                period.Item().Text($"To: {endDate:yyyy-MM-dd HH:mm:ss}");
            });

            // Logs Table
            column.Item().Padding(10).Column(logsSection =>
            {
                logsSection.Item().Text("Request Logs").FontSize(16).SemiBold();
                
                logsSection.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    // Table Header
                    table.Header(header =>
                    {
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Timestamp").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Duration").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Status").SemiBold();
                    });

                    foreach (var log in logs)
                    {
                        table.Cell().Padding(5).Text(log.RequestTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        table.Cell().Padding(5).Text(log.TimeSpent);
                        table.Cell().Padding(5).Text(log.StatusCode.ToString());
                    }
                });
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
            row.RelativeItem().AlignRight().Text("Generated by Usage Monitor");
        });
    }
}
