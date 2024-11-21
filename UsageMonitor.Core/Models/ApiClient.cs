namespace UsageMonitor.Core.Models;

public record UpdatePaymentRequest(decimal AdditionalAmount);

public class ApiClient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal UnitPrice { get; set; }
    public int UsageLimit => (int)(AmountPaid / UnitPrice);
    public DateTime UsageCycle { get; set; }
    public List<RequestLog>? RequestLogs { get; set; }
}