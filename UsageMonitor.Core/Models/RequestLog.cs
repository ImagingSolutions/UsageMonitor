using System.Text.Json.Serialization;

namespace UsageMonitor.Core.Models;

public class RequestLog
{
    public int Id { get; set; }
    public int? PaymentId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public double Duration { get; set; }
    public DateTime RequestTime { get; set; }
    
    [JsonIgnore]
    public Payment? Payment { get; set; }
}