using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrgChart.Core.Interfaces;
using OrgChart.Infrastructure.Repositories;

namespace OrgChart.Infrastructure.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsEnvironment("Testing"))
        {
            services.AddDbContext<OrgChartDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    o => o.MigrationsAssembly("OrgChart.Infrastructure")));
        }

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        // services.AddScoped<IEmployeeRepository, EmployeeImplementedRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        return services;
    }
}
