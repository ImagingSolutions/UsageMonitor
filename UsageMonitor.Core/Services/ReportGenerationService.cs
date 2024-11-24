using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Services;

public interface IReportGenerationService
{
    Task<byte[]> GenerateClientUsageReportAsync(DateTime? startDate = null, DateTime? endDate = null);
}

public class ReportGenerationService : IReportGenerationService
{
    private readonly IUsageMonitorService _usageMonitorService;

    public ReportGenerationService(IUsageMonitorService usageMonitorService)
    {
        _usageMonitorService = usageMonitorService;
    }

    public async Task<byte[]> GenerateClientUsageReportAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var client = await _usageMonitorService.GetApiClientAsync();

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

        var logs = await _usageMonitorService.GetLogsAsync(startDate.Value, endDate.Value);
        var payments = await _usageMonitorService.GetAllPaymentStatsAsync();
        var dailyStats = GetDailyStats(logs);

        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeContent(content, client, payments, dailyStats, logs, startDate.Value, endDate.Value));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private class DailyStats
    {
        public DateTime Date { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
    }

    private List<DailyStats> GetDailyStats(IEnumerable<RequestLog> logs)
    {
        return logs.GroupBy(l => l.RequestTime.Date)
            .Select(g => new DailyStats
            {
                Date = g.Key,
                TotalRequests = g.Count(),
                SuccessfulRequests = g.Count(l => l.StatusCode < 400),
                FailedRequests = g.Count(l => l.StatusCode >= 400)
            })
            .OrderBy(s => s.Date)
            .ToList();
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

    private void ComposeContent(IContainer container, ApiClient client, IEnumerable<PaymentUsageStats> payments, 
        List<DailyStats> dailyStats, IEnumerable<RequestLog> logs, DateTime startDate, DateTime endDate)
    {
        container.Column(column =>
        {
            // Client Summary Section
            column.Item().Padding(10).Column(summary =>
            {
                summary.Item().Text("Client Summary").FontSize(16).SemiBold();
                summary.Item().Text($"Client: {client.Name}").FontSize(12);
                summary.Item().Text($"Email: {client.Email}");
                summary.Item().Text($"Account Created: {client.CreatedAt:yyyy-MM-dd}");
            });

            // Payments Summary Section
            column.Item().Padding(10).Column(paymentsSummary =>
            {
                paymentsSummary.Item().Text("Payments Summary").FontSize(16).SemiBold();
                paymentsSummary.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Date").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Amount").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Unit Price").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Total Requests").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Used").SemiBold();
                    });

                    foreach (var payment in payments)
                    {
                        table.Cell().Padding(5).Text(payment.CreatedAt.ToString("yyyy-MM-dd"));
                        table.Cell().Padding(5).Text($"${payment.Amount:F2}");
                        table.Cell().Padding(5).Text($"${payment.UnitPrice:F2}");
                        table.Cell().Padding(5).Text(payment.TotalRequests.ToString());
                        table.Cell().Padding(5).Text(payment.UsedRequests.ToString());
                    }
                });
            });

            // Daily Usage Statistics
            column.Item().Padding(10).Column(dailyUsage =>
            {
                dailyUsage.Item().Text("Daily Usage Statistics").FontSize(16).SemiBold();
                dailyUsage.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Date").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Total").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Successful").SemiBold();
                        header.Cell().Background("#f0f0f0").Padding(5).Text("Failed").SemiBold();
                    });

                    foreach (var stat in dailyStats)
                    {
                        table.Cell().Padding(5).Text(stat.Date.ToString("yyyy-MM-dd"));
                        table.Cell().Padding(5).Text(stat.TotalRequests.ToString());
                        table.Cell().Padding(5).Text(stat.SuccessfulRequests.ToString());
                        table.Cell().Padding(5).Text(stat.FailedRequests.ToString());
                    }
                });
            });

            // Report Period
            column.Item().Padding(10).Column(period =>
            {
                period.Item().Text("Report Period").FontSize(16).SemiBold();
                period.Item().Text($"From: {startDate:yyyy-MM-dd HH:mm:ss}");
                period.Item().Text($"To: {endDate:yyyy-MM-dd HH:mm:ss}");
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
