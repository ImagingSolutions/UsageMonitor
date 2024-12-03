using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UsageMonitor.Core.Models;
using System.Net.Http;
using UsageMonitor.Core.Config;

namespace UsageMonitor.Core.Services;

public interface IReportGenerationService
{
    Task<byte[]> GenerateClientUsageReportAsync(DateTime? startDate = null, DateTime? endDate = null, bool detailed = false);
    Task<byte[]> GenerateUsageSummaryReportAsync(DateTime? startDate = null, DateTime? endDate = null);
}

public class ReportGenerationService : IReportGenerationService
{
    private readonly IUsageMonitorService _usageMonitorService;
    private readonly string? _brandingImageUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private byte[]? _cachedLogo;

    private static readonly string PRIMARY_COLOR = "#2c3e50";
    private static readonly string SECONDARY_COLOR = "#34495e";
    private static readonly string ACCENT_COLOR = "#3498db";

    public ReportGenerationService(
        IUsageMonitorService usageMonitorService,
        IHttpClientFactory httpClientFactory,
        UsageMonitorOptions options)
    {
        _usageMonitorService = usageMonitorService;
        _httpClientFactory = httpClientFactory;
        _brandingImageUrl = options.BrandingImageUrl;
    }

    private byte[] GetLogoImageAsync()
    {
        if (_cachedLogo != null) return _cachedLogo;
        if (string.IsNullOrEmpty(_brandingImageUrl)) return null;

        try
        {
            using var client = _httpClientFactory.CreateClient();
            _cachedLogo = client.GetByteArrayAsync(_brandingImageUrl).Result;
            return _cachedLogo;
        }
        catch (Exception)
        {
            // Silently fail and continue without logo
            return null;
        }
    }

    private void ComposeHeader(IContainer container)
    {
        var logoBytes = GetLogoImageAsync();

        container.Background(Colors.White).Padding(20).Row(row =>
        {
            if (logoBytes != null)
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("API Usage Report")
                        .FontSize(28)
                        .FontColor(PRIMARY_COLOR)
                        .Bold();
                    column.Item().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                        .FontSize(14)
                        .FontColor(SECONDARY_COLOR);
                });

                row.ConstantItem(120).Height(60).Image(logoBytes)
                    .FitArea();
            }
            else
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("API Usage Report")
                        .FontSize(28)
                        .FontColor(PRIMARY_COLOR)
                        .Bold();
                    column.Item().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                        .FontSize(14)
                        .FontColor(SECONDARY_COLOR);
                });
            }
        });
    }

    public async Task<byte[]> GenerateClientUsageReportAsync(DateTime? startDate = null, DateTime? endDate = null, bool detailed = false)
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

    public async Task<byte[]> GenerateUsageSummaryReportAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var client = await _usageMonitorService.GetApiClientAsync();

        startDate ??= DateTime.UtcNow.AddMonths(-1);
        endDate ??= DateTime.UtcNow;

        var payments = await _usageMonitorService.GetAllPaymentStatsAsync();
        var periodPayments = payments.Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate);

        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeSummaryContent(content, client, periodPayments, startDate.Value, endDate.Value));
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

    private void ComposeSummaryContent(IContainer container, ApiClient client,
        IEnumerable<PaymentUsageStats> payments, DateTime startDate, DateTime endDate)
    {
        container.Padding(20).Column(column =>
        {
            // Client Summary Section
            column.Item().BorderBottom(1).BorderColor(ACCENT_COLOR).PaddingBottom(10).Column(summary =>
            {
                summary.Item().Text("Client Overview")
                    .FontSize(20)
                    .FontColor(PRIMARY_COLOR)
                    .Bold();

                summary.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn();
                        cols.RelativeColumn(2);
                    });

                    table.Cell().Text("Client Name").FontColor(SECONDARY_COLOR);
                    table.Cell().Text(client.Name).Bold();

                    table.Cell().Text("Email").FontColor(SECONDARY_COLOR);
                    table.Cell().Text(client.Email).Bold();

                    table.Cell().Text("Report Period").FontColor(SECONDARY_COLOR);
                    table.Cell().Text($"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}").Bold();
                });
            });

            // Current Active Payment Section
            var currentPayment = payments
                .Where(p => p.RemainingRequests > 0)
                .OrderBy(p => p.CreatedAt)
                .FirstOrDefault();

            if (currentPayment != null)
            {
                column.Item().PaddingVertical(20).Column(current =>
                {
                    current.Item().Column(c =>
                    {
                        c.Item().Text("Current Active Payment")
                            .FontSize(18)
                            .FontColor(PRIMARY_COLOR)
                            .Bold();

                        c.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn(2);
                            });

                            table.Cell().Text("Payment Date").FontColor(SECONDARY_COLOR);
                            table.Cell().Text(currentPayment.CreatedAt.ToString("MMM dd, yyyy")).Bold();

                            table.Cell().Text("Amount Paid").FontColor(SECONDARY_COLOR);
                            table.Cell().Text($"${currentPayment.Amount:N2}").Bold();

                            table.Cell().Text("Unit Price").FontColor(SECONDARY_COLOR);
                            table.Cell().Text($"${currentPayment.UnitPrice:N4}").Bold();

                            table.Cell().Text("Remaining Balance").FontColor(SECONDARY_COLOR);
                            table.Cell().Text($"${currentPayment.RemainingRequests * currentPayment.UnitPrice:N2}").Bold();
                        });

                        c.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                            });

                            // Headers
                            table.Cell().Text("Total Requests").FontColor(SECONDARY_COLOR);
                            table.Cell().Text("Used").FontColor(SECONDARY_COLOR);
                            table.Cell().Text("Remaining").FontColor(SECONDARY_COLOR);
                            table.Cell().Text("Value").FontColor(SECONDARY_COLOR);

                            // Values
                            table.Cell().Text(currentPayment.TotalRequests.ToString("N0")).Bold();
                            table.Cell().Text(currentPayment.UsedRequests.ToString("N0")).Bold();
                            table.Cell().Text(currentPayment.RemainingRequests.ToString("N0")).Bold();
                            table.Cell().Text($"${(currentPayment.RemainingRequests * currentPayment.UnitPrice):N2}").Bold();
                        });
                    });
                });
            }

            // Historical Payments Table
            var historicalPayments = payments
                .Where(p => p.RemainingRequests == 0)
                .OrderByDescending(p => p.CreatedAt);

            if (historicalPayments.Any())
            {
                column.Item().PaddingTop(20).Column(historical =>
                {
                    historical.Item().Text("Payment History")
                        .FontSize(18)
                        .FontColor(PRIMARY_COLOR)
                        .Bold();

                    historical.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(100); // Date
                            cols.RelativeColumn();    // Amount
                            cols.RelativeColumn();    // Unit Price
                            cols.RelativeColumn();    // Requests
                            cols.RelativeColumn();    // Cost/Request
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Padding(10)
                                .Text("Date")
                                .FontColor(PRIMARY_COLOR)
                                .Bold();
                            header.Cell().Padding(10)
                                .Text("Amount")
                                .FontColor(PRIMARY_COLOR)
                                .Bold();
                            header.Cell().Padding(10)
                                .Text("Unit Price")
                                .FontColor(PRIMARY_COLOR)
                                .Bold();
                            header.Cell().Padding(10)
                                .Text("Requests")
                                .FontColor(PRIMARY_COLOR)
                                .Bold();
                            header.Cell().Padding(10)
                                .Text("Cost/Req")
                                .FontColor(PRIMARY_COLOR)
                                .Bold();
                        });

                        foreach (var payment in historicalPayments)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10)
                                .Text(payment.CreatedAt.ToString("MMM dd"));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10)
                                .Text($"${payment.Amount:N2}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10)
                                .Text($"${payment.UnitPrice:N4}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10)
                                .Text(payment.UsedRequests.ToString("N0"));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(10)
                                .Text($"${(payment.Amount / payment.UsedRequests):N4}");
                        }
                    });
                });

                // Summary Statistics
                column.Item().PaddingTop(20).Column(stats =>
                {
                    var totalSpent = payments.Sum(p => p.Amount);
                    var totalRequests = payments.Sum(p => p.UsedRequests);
                    var avgCostPerRequest = totalRequests > 0 ? totalSpent / totalRequests : 0;

                    stats.Item().Text("Overall Statistics")
                        .FontSize(18)
                        .FontColor(PRIMARY_COLOR)
                        .Bold();

                    stats.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn(2);
                        });

                        table.Cell().Text("Total Amount Spent").FontColor(SECONDARY_COLOR);
                        table.Cell().Text($"${totalSpent:N2}").Bold();

                        table.Cell().Text("Total Requests").FontColor(SECONDARY_COLOR);
                        table.Cell().Text(totalRequests.ToString("N0")).Bold();

                        table.Cell().Text("Average Cost per Request").FontColor(SECONDARY_COLOR);
                        table.Cell().Text($"${avgCostPerRequest:N4}").Bold();
                    });
                });
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber().FontColor(ACCENT_COLOR);
                    text.Span(" of ");
                    text.TotalPages().FontColor(ACCENT_COLOR);
                });
                row.RelativeItem().AlignRight().Text("Generated by Usage Monitor")
                    .FontColor(SECONDARY_COLOR);
            });
    }
}
