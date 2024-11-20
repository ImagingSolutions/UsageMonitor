namespace UsageMonitor.Core.Models;

public class RequestLog{
    public int Id { get; set; }
    public string ApiKey {get;set;}
    public int StatusCode { get; set; }
    public DateTime RequestTime { get; set; }
    public DateTime ResponseTime { get; set; }
    public string TimeSpent => Duration.TotalMinutes >= 1 
        ? $"{Duration.Minutes}m {Duration.Seconds}s {Duration.Milliseconds}ms"
        : $"{Duration.Seconds}s {Duration.Milliseconds}ms";
    public TimeSpan Duration => ResponseTime - RequestTime;
    public int ApiClientId { get; set; }
    public ApiClient ApiClient { get; set; }
}