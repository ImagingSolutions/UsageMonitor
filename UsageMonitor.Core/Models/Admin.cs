using System.ComponentModel.DataAnnotations;

namespace UsageMonitor.Core.Models;

// Request models for admin authentication
public record AdminLoginRequest(string Username, string Password);
public record AdminSetupRequest(string Username, string Password);

public class Admin
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
}
