namespace UsageMonitor.Core.Models;
public class Payment
{
    public int Id { get; set; }
    public int ApiClientId { get; set; }
    public decimal Amount { get; set; }
    public decimal UnitPrice { get; set; }
    public int TotalRequests => (int)(Amount / UnitPrice);
    public int UsedRequests { get; set; }
    public int RemainingRequests => TotalRequests - UsedRequests;
    public DateTime CreatedAt { get; set; }
    public bool IsFullyUtilized => UsedRequests >= TotalRequests;
    
    public ApiClient? ApiClient { get; set; }
    public ICollection<RequestLog>? Requests { get; set; }
} 