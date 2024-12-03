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
    options.ConnectionString = "Data Source=localhost;Initial Catalog=UsageMonitorDBTest;TrustServerCertificate=True;Integrated Security=true;";
    options.DatabaseProvider = DatabaseProvider.SQLServer;
    options.BrandingImageUrl = "https://images.crunchbase.com/image/upload/c_pad,h_170,w_170,f_auto,b_white,q_auto:eco,dpr_1/v1502269844/tkozht1uupsomwt87cyr.png";
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
