namespace UsageMonitor.Core.Models;

public record UpdatePaymentRequest(decimal AdditionalAmount);

public class ApiClient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public List<Payment>? Payments { get; set; }
    public List<RequestLog>? RequestLogs { get; set; }
    
    // Calculated properties
    public int TotalUsageLimit => Payments?.Sum(p => p.TotalRequests) ?? 0;
    public int TotalUsedRequests => Payments?.Sum(p => p.UsedRequests) ?? 0;
    public int RemainingRequests => TotalUsageLimit - TotalUsedRequests;
}