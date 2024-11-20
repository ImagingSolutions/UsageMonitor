namespace UsageMonitor.Core.Models;

public record UpdateLimitRequest(int UsageLimit);

public class ApiClient
{

    public int Id { get; set; }
    public string ApiKey { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UsageLimit { get; set; }
    public List<RequestLog> RequestLogs { get; set; } = new();

}