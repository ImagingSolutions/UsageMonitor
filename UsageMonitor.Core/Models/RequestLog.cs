namespace UsageMonitor.Core.Models;

public class RequestLog
{
    public int Id { get; set; }
    public int StatusCode { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTime RequestTime { get; set; }
    public double Duration { get; set; }
    public int ApiClientId { get; set; }
    public ApiClient ApiClient { get; set; }
}