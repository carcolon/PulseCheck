using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Services;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Infrastructure.Persistence;
using PulseCheck.Infrastructure.Services;

namespace PulseCheck.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPulseCheckInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string databaseProvider)
    {
        services.AddDbContext<PulseCheckDbContext>(options =>
        {
            var provider = databaseProvider.Trim().ToLowerInvariant();
            if (provider == "sqlserver")
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("PulseCheckDb")
                    ?? "Server=localhost\\SQLEXPRESS;Database=PulseCheckDb;Trusted_Connection=True;TrustServerCertificate=True;");
                return;
            }

            options.UseSqlite(configuration.GetConnectionString("PulseCheckDb") ?? "Data Source=pulsecheck.db");
        });

        services.AddScoped<IPulseCheckUnitOfWork, PulseCheckUnitOfWork>();
        services.AddScoped<IEmployeeIdentityResolver, NullEmployeeIdentityResolver>();
        services.AddScoped<IEmployeeOperationsProfileResolver, NullEmployeeOperationsProfileResolver>();
        services.AddScoped<ILeaderAlertEmailService, NullLeaderAlertEmailService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<PulseCheckDbInitializer>();
        services.AddScoped<AdminAuthService>();
        services.AddScoped<AdminUserService>();

        return services;
    }
}
