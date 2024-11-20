using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UsageMonitor.Core.Data;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Services;

public interface IUsageMonitorService
{

    Task LogRequestAsync(RequestLog log);
    Task<(IEnumerable<RequestLog> Logs, int TotalCount)> GetLogsAsync(DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20);
    Task<IEnumerable<RequestLog>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null);
    Task<ApiClient?> GetApiClientByKeyAsync(string apiKey);

    Task<IEnumerable<ApiClient>> GetApiClientsAsync();
    Task<ApiClient> CreateApiClientAsync(ApiClient client);
    Task<bool> UpdateClientLimitAsync(int clientId, int newLimit);
    Task<Dictionary<string, int>> GetMonthlyUsageStatsAsync();
    Task<Dictionary<string, int>> GetErrorRatesAsync();
    Task<bool> ValidateAdminLoginAsync(string username, string password);
    Task<bool> SetupAdminAccountAsync(string username, string password);
    Task<int> GetTotalRequestCountAsync(string apiKey);
    
    Task<bool> UpdateClientAsync(int clientId, ApiClient updatedClient);
    Task<bool> HasAdminAccountAsync();
}

public class UsageMonitorService : IUsageMonitorService
{

    private readonly UsageMonitorDbContext _context;

    public UsageMonitorService(UsageMonitorDbContext context)
    {
        _context = context;
    }

     public async Task LogRequestAsync(RequestLog log)
    {
        await _context.RequestLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }
    
    public async Task<(IEnumerable<RequestLog> Logs, int TotalCount)> GetLogsAsync(
        DateTime? from = null, 
        DateTime? to = null, 
        int page = 1, 
        int pageSize = 20)
    {
        var query = _context.RequestLogs.AsQueryable();
        
        if (from.HasValue)
            query = query.Where(x => x.RequestTime >= from.Value);
        
        if (to.HasValue)
            query = query.Where(x => x.RequestTime <= to.Value);

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(x => x.RequestTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logs, totalCount);
    }

    public async Task<IEnumerable<RequestLog>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.RequestLogs.Where(x => x.StatusCode >= 400);
        
        if (from.HasValue)
            query = query.Where(x => x.RequestTime >= from.Value);
        
        if (to.HasValue)
            query = query.Where(x => x.RequestTime <= to.Value);

        return await query.OrderByDescending(x => x.RequestTime).ToListAsync();
    }

    public async Task<int> GetTotalRequestCountAsync(string apiKey)
    {
        return await _context.RequestLogs
            .CountAsync(x => x.ApiKey == apiKey);
    }

    public async Task<ApiClient?> GetApiClientByKeyAsync(string apiKey)
    {
        return await _context.ApiClients
            .FirstOrDefaultAsync(x => x.ApiKey == apiKey);
    }

    public async Task<IEnumerable<ApiClient>> GetApiClientsAsync()
    {
        return await _context.ApiClients.ToListAsync();
    }

    public async Task<ApiClient> CreateApiClientAsync(ApiClient client)
    {
        client.CreatedAt = DateTime.UtcNow;
        
        await _context.ApiClients.AddAsync(client);
        await _context.SaveChangesAsync();
        
        return client;
    }

    public async Task<bool> ValidateAdminLoginAsync(string username, string password)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Username == username);
        if (admin == null) return false;

        var passwordHasher = new PasswordHasher<Admin>();
        var result = passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
        return result == PasswordVerificationResult.Success;
    }

    public async Task<bool> SetupAdminAccountAsync(string username, string password)
    {
        if (await HasAdminAccountAsync()) return false;

        var passwordHasher = new PasswordHasher<Admin>();
        var admin = new Admin
        {
            Username = username,
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);

        await _context.Admins.AddAsync(admin);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HasAdminAccountAsync()
    {
        return await _context.Admins.AnyAsync();
    }

    public async Task<bool> UpdateClientLimitAsync(int clientId, int newLimit)
    {
        var client = await _context.ApiClients.FindAsync(clientId);
        if (client == null) return false;

        client.UsageLimit += newLimit; // Add to existing limit
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateClientAsync(int clientId, ApiClient updatedClient)
    {
        var client = await _context.ApiClients.FindAsync(clientId);
        if (client == null) return false;

        client.Name = updatedClient.Name;
        client.Email = updatedClient.Email;
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<string, int>> GetMonthlyUsageStatsAsync()
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var stats = await _context.RequestLogs
            .Where(x => x.RequestTime >= startOfMonth)
            .GroupBy(x => x.RequestTime.Day)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderBy(x => x.Day)
            .ToDictionaryAsync(x => x.Day.ToString(), x => x.Count);
        return stats;
    }

    public async Task<Dictionary<string, int>> GetErrorRatesAsync()
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var stats = await _context.RequestLogs
            .Where(x => x.RequestTime >= startOfMonth && x.StatusCode >= 400)
            .GroupBy(x => x.ApiKey)
            .Select(g => new { ApiKey = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ApiKey, x => x.Count);
        return stats;
    }

}   