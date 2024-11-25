using System.Text.Json.Serialization;

namespace UsageMonitor.Core.Models;
public class Payment
{
    public int Id { get; set; }
    public int ApiClientId { get; set; }
    public decimal Amount { get; set; }
    public decimal UnitPrice { get; set; }
    public int TotalRequests { get; set; }
    public int UsedRequests { get; set; }
    public int RemainingRequests { get; set; }
    public bool IsFullyUtilized { get; set; }
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public ApiClient? ApiClient { get; set; }

    [JsonIgnore]
    public ICollection<RequestLog>? Requests { get; set; }
}