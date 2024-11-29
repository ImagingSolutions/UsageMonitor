using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UsageMonitor.Core.Data;
using UsageMonitor.Core.Models;

namespace UsageMonitor.Core.Services;

public interface IUsageMonitorService
{

    Task<(IEnumerable<RequestLog> Logs, int TotalCount)> GetPaginatedLogsAsync(DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20);
    Task<IEnumerable<RequestLog>> GetLogsAsync(DateTime from, DateTime to);
    Task<IEnumerable<RequestLog>> GetErrorLogsAsync(DateTime? from = null, DateTime? to = null);
    Task<ApiClient> GetApiClientAsync();
    Task<ApiClient> CreateApiClientAsync(CreateNewClient client);
    Task<bool> AddClientPaymentAsync(decimal additionalAmount, decimal unitPrice);
    Task<bool> ValidateAdminLoginAsync(string username, string password);
    Task<bool> SetupAdminAccountAsync(string username, string password);
    Task<ClientUsageStats> GetClientUsageAsync();
    Task<bool> UpdateClientAsync(ApiClient updatedClient);
    Task<bool> HasAdminAccountAsync();
    Task<Dictionary<string, int>> GetMonthlyUsageAsync();
    Task<bool> LogRequestAsync(RequestLog log);
    Task<PaymentUsageStats> GetPaymentUsageStatsAsync(int paymentId);
    Task<IEnumerable<PaymentUsageStats>> GetAllPaymentStatsAsync();
    Task<bool> HasAvailableRequestsAsync();
    Task<AnalyticsOverview> GetAnalyticsOverviewAsync();
    Task<TimelineStats> GetTimelineStatsAsync(int days = 30);
    Task<IEnumerable<EndpointStats>> GetTopEndpointsAsync(int limit = 10);
    Task<ResponseTimeStats> GetResponseTimeStatsAsync(int days = 30);
}

public class UsageMonitorService : IUsageMonitorService
{

    private readonly UsageMonitorDbContext _context;

    public UsageMonitorService(UsageMonitorDbContext context)
    {
        _context = context;
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

    public async Task<ClientUsageStats> GetClientUsageAsync()
    {
        var payments = await _context.Payments.Select(p => new PaymentUsageStats
        {
            TotalRequests = p.TotalRequests,
            UsedRequests = p.UsedRequests,
            RemainingRequests = p.RemainingRequests,
        }).ToListAsync();

        var usage = new ClientUsageStats
        {
            TotalRequests = payments.Sum(x => x.TotalRequests),
            UsedRequests = payments.Sum(x => x.UsedRequests),
            RemainingRequests = payments.Sum(x => x.RemainingRequests)
        };

        return usage;

    }

    public async Task<ApiClient> CreateApiClientAsync(CreateNewClient clientData)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var newClient = new ApiClient
            {
                CreatedAt = DateTime.UtcNow,
                Email = clientData.Email,
                Name = clientData.Name,
                Payments =
                [
                    new() {
                        Amount = clientData.AmountPaid,
                        UnitPrice = clientData.UnitPrice,
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            };

            await _context.ApiClients.AddAsync(newClient);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return newClient;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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

    public async Task<bool> AddClientPaymentAsync(decimal additionalAmount, decimal unitPrice)
    {
        var client = await _context.ApiClients.FirstOrDefaultAsync();
        if (client == null) return false;

        var payment = new Payment
        {
            Amount = additionalAmount,
            ApiClientId = client.Id,
            UnitPrice = unitPrice,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Payments.AddAsync(payment);

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
        var activePayment = await _context.Payments
            .Where(p => p.RemainingRequests > 0)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (activePayment == null) return false;

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
        var payments = await _context.Payments.Select(p => new PaymentUsageStats
        {
            PaymentId = p.Id,
            Amount = p.Amount,
            TotalRequests = p.TotalRequests,
            UsedRequests = p.UsedRequests,
            UnitPrice = p.UnitPrice,
            RemainingRequests = p.RemainingRequests,
            CreatedAt = p.CreatedAt
        }).ToListAsync();

        if (payments == null) return Enumerable.Empty<PaymentUsageStats>();

        return payments;
    }

    public async Task<bool> HasAvailableRequestsAsync()
    {
        return await _context.Payments
            .AnyAsync(p => p.RemainingRequests > 0);
    }

    public async Task<AnalyticsOverview> GetAnalyticsOverviewAsync()
    {
        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-30); // Last 30 days

        var logs = await _context.RequestLogs
            .Where(l => l.RequestTime >= startDate)
            .ToListAsync();

        var totalRequests = logs.Count;
        var successRequests = logs.Count(l => l.StatusCode < 400);
        var errorRequests = logs.Count(l => l.StatusCode >= 400);

        return new AnalyticsOverview
        {
            TotalRequests = totalRequests,
            SuccessRate = totalRequests > 0 ? (successRequests * 100.0 / totalRequests) : 0,
            ErrorRate = totalRequests > 0 ? (errorRequests * 100.0 / totalRequests) : 0,
            AvgResponseTime = logs.Any() ? logs.Average(l => l.Duration * 1000) : 0, // Convert to ms
            Status2xx = logs.Count(l => l.StatusCode >= 200 && l.StatusCode < 300),
            Status4xx = logs.Count(l => l.StatusCode >= 400 && l.StatusCode < 500),
            Status5xx = logs.Count(l => l.StatusCode >= 500)
        };
    }

    public async Task<TimelineStats> GetTimelineStatsAsync(int days = 30)
    {
        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-days);

        var dailyCounts = await _context.RequestLogs
            .Where(l => l.RequestTime >= startDate)
            .GroupBy(l => l.RequestTime.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // Fill in missing dates with zero counts
        var allDates = Enumerable.Range(0, days)
            .Select(i => startDate.AddDays(i).Date)
            .ToList();

        var timeline = allDates.Select(date => new
        {
            Date = date,
            Count = dailyCounts.FirstOrDefault(x => x.Date == date)?.Count ?? 0
        }).ToList();

        return new TimelineStats
        {
            Dates = timeline.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList(),
            Counts = timeline.Select(x => x.Count).ToList()
        };
    }

    public async Task<IEnumerable<EndpointStats>> GetTopEndpointsAsync(int limit = 10)
    {
        return await _context.RequestLogs
            .GroupBy(l => l.Path)
            .Select(g => new EndpointStats
            {
                Path = g.Key,
                RequestCount = g.Count(),
                SuccessRate = g.Count(r => r.StatusCode < 400) * 100.0 / g.Count(),
                AvgResponseTime = g.Average(r => r.Duration * 1000) // Convert to ms
            })
            .OrderByDescending(x => x.RequestCount)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<ResponseTimeStats> GetResponseTimeStatsAsync(int days = 30)
    {
        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-days);

        var dailyStats = await _context.RequestLogs
            .Where(l => l.RequestTime >= startDate)
            .GroupBy(l => l.RequestTime.Date)
            .Select(g => new
            {
                Date = g.Key,
                AvgTime = g.Average(r => r.Duration * 1000) // Convert to ms
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return new ResponseTimeStats
        {
            Dates = dailyStats.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList(),
            AvgTimes = dailyStats.Select(x => x.AvgTime).ToList()
        };
    }
}

public class PaymentUsageStats
{
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public int TotalRequests { get; set; }
    public int UsedRequests { get; set; }
    public int RemainingRequests { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientUsageStats
{
    public int TotalRequests { get; set; }
    public int UsedRequests { get; set; }
    public int RemainingRequests { get; set; }
}

public class CreateNewClient
{
    public string Name { get; set; }
    public string Email { get; set; }

    public decimal AmountPaid { get; set; }
    public decimal UnitPrice { get; set; }


}

public class AnalyticsOverview
{
    public int TotalRequests { get; set; }
    public double SuccessRate { get; set; }
    public double ErrorRate { get; set; }
    public double AvgResponseTime { get; set; }
    public int Status2xx { get; set; }
    public int Status4xx { get; set; }
    public int Status5xx { get; set; }
}

public class TimelineStats
{
    public List<string> Dates { get; set; }
    public List<int> Counts { get; set; }
}

public class EndpointStats
{
    public string Path { get; set; }
    public int RequestCount { get; set; }
    public double SuccessRate { get; set; }
    public double AvgResponseTime { get; set; }
}

public class ResponseTimeStats
{
    public List<string> Dates { get; set; }
    public List<double> AvgTimes { get; set; }
}