using System.ComponentModel.DataAnnotations;

namespace UsageMonitor.Core.Models;

public record CreateAdminRequest(string Username, string Password);
public record AdminLoginRequest(string Username, string Password);

public class Admin
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
