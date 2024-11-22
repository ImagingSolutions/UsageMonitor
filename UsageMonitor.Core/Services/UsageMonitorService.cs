using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UsageMonitor.Core.Data;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Services;

public interface IUsageMonitorService
{
    Task LogRequestAsync(RequestLog log);
    Task<(IEnumerable<RequestLog> Logs, int TotalCount)> GetPaginatedLogsAsync(DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20);
    Task<IEnumerable<RequestLog>> GetLogsAsync(DateTime from, DateTime to);
    Task<IEnumerable<RequestLog>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null);
    Task<ApiClient> GetApiClientAsync();
    Task<ApiClient> CreateApiClientAsync(ApiClient client);
    Task<bool> AddClientPaymentAsync(decimal additionalAmount);
    Task<bool> ValidateAdminLoginAsync(string username, string password);
    Task<bool> SetupAdminAccountAsync(string username, string password);
    Task<int> GetTotalRequestCountAsync();
    Task<bool> UpdateClientAsync(ApiClient updatedClient);
    Task<bool> HasAdminAccountAsync();
    Task<Dictionary<string, int>> GetMonthlyUsageAsync();
    Task<bool> LogRequestAsync(RequestLog log);
    Task<PaymentUsageStats> GetPaymentUsageStatsAsync(int paymentId);
    Task<IEnumerable<PaymentUsageStats>> GetAllPaymentStatsAsync();
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

    public async Task<(IEnumerable<RequestLog> Logs, int TotalCount)> GetPaginatedLogsAsync(
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

    public async Task<int> GetTotalRequestCountAsync()
    {
        var client = await _context.ApiClients.FirstOrDefaultAsync();
        return await _context.RequestLogs.Where(rq => rq.RequestTime >= client.UsageCycle)
            .CountAsync();
    }

    public async Task<ApiClient> CreateApiClientAsync(ApiClient client)
    {
        client.CreatedAt = DateTime.UtcNow;

        await _context.ApiClients.AddAsync(client);
        await _context.SaveChangesAsync();

        return client;
    }

    public async Task<ApiClient> GetApiClientAsync()
    {
        var client = await _context.ApiClients.FirstOrDefaultAsync() ?? null;
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

    public async Task<bool> AddClientPaymentAsync(decimal additionalAmount)
    {
        var client = await _context.ApiClients.FirstOrDefaultAsync();
        if (client == null) return false;

        client.AmountPaid += additionalAmount;
        client.UsageCycle = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateClientAsync(ApiClient updatedClient)
    {
        var client = await _context.ApiClients.FirstOrDefaultAsync();
        if (client == null) return false;

        client.Name = updatedClient.Name;
        client.Email = updatedClient.Email;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<string, int>> GetMonthlyUsageAsync()
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

    public async Task<IEnumerable<RequestLog>> GetLogsAsync(DateTime from, DateTime to)
    {
        return await _context.RequestLogs
            .Where(x => x.RequestTime >= from && x.RequestTime <= to)
            .OrderByDescending(x => x.RequestTime)
            .ToListAsync();
    }

    public async Task<bool> LogRequestAsync(RequestLog log)
    {
        var client = await _context.ApiClients
            .Include(c => c.Payments)
            .FirstOrDefaultAsync();
            
        if (client == null) return false;

        // Find the first payment that still has available requests
        var activePayment = client.Payments?
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault(p => !p.IsFullyUtilized);

        if (activePayment == null) return false;

        // Assign the request to this payment and increment its usage
        log.PaymentId = activePayment.Id;
        activePayment.UsedRequests++;

        _context.RequestLogs.Add(log);
        await _context.SaveChangesAsync();
        
        return true;
    }

    public async Task<PaymentUsageStats> GetPaymentUsageStatsAsync(int paymentId)
    {
        var payment = await _context.Payments
            .Include(p => p.Requests)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null) return null;

        return new PaymentUsageStats
        {
            PaymentId = payment.Id,
            Amount = payment.Amount,
            TotalRequests = payment.TotalRequests,
            UsedRequests = payment.UsedRequests,
            RemainingRequests = payment.RemainingRequests,
            CreatedAt = payment.CreatedAt
        };
    }

    public async Task<IEnumerable<PaymentUsageStats>> GetAllPaymentStatsAsync()
    {
        var client = await _context.ApiClients
            .Include(c => c.Payments)
            .FirstOrDefaultAsync();

        if (client?.Payments == null) return Enumerable.Empty<PaymentUsageStats>();

        return client.Payments.Select(p => new PaymentUsageStats
        {
            PaymentId = p.Id,
            Amount = p.Amount,
            TotalRequests = p.TotalRequests,
            UsedRequests = p.UsedRequests,
            RemainingRequests = p.RemainingRequests,
            CreatedAt = p.CreatedAt
        });
    }
}

public class PaymentUsageStats
{
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public int TotalRequests { get; set; }
    public int UsedRequests { get; set; }
    public int RemainingRequests { get; set; }
    public DateTime CreatedAt { get; set; }
}