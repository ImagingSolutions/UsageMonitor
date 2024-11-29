using UsageMonitor.Core.Config;
using UsageMonitor.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddUsageMonitor(options =>
{
    options.ConnectionString = "Data Source= usagemonitor.db;";
    // options.ConnectionString = "Data Source=localhost;Initial Catalog=UsageMonitorDBTest;TrustServerCertificate=True;Integrated Security=true;";
    options.DatabaseProvider = DatabaseProvider.SQLite;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

//usage endpoints;
app.UseUsageMonitor();
app.MapUsageMonitorEndpoints();
app.MapUsageMonitorPages();

app.Run();
