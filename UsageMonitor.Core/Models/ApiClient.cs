namespace UsageMonitor.Core.Models;

public record UpdateLimitRequest(int UsageLimit);

public class ApiClient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UsageLimit { get; set; }
    public DateTime UsageCycle { get; set; }
    public List<RequestLog>? RequestLogs { get; set; }
}