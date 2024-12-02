using Microsoft.EntityFrameworkCore;
using UsageMonitor.Core.Config;

namespace UsageMonitor.Core.Data;

public interface IDatabaseInitializer
{
    void Initialize();

}

public class DatabaseInitializer : IDatabaseInitializer
{

    private readonly UsageMonitorDbContext _context;
    private readonly UsageMonitorOptions _options;

    public DatabaseInitializer(UsageMonitorDbContext context, UsageMonitorOptions options)
    {
        _context = context;
        _options = options;
    }

    public void Initialize()
    {
        _context.Database.EnsureCreated();

    }
}