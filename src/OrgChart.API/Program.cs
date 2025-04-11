using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OrgChart.API.Middleware;
using OrgChart.Core.DI;
using OrgChart.Infrastructure;
using OrgChart.Infrastructure.DI;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog(Log.Logger);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
builder.Services.AddCoreServices();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrgChartDbContext>();
        db.Database.Migrate();
    }
}

app.UseErrorHandlingMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();
app.Lifetime.ApplicationStopping.Register(Log.CloseAndFlush);
app.Run();

public partial class Program { }
