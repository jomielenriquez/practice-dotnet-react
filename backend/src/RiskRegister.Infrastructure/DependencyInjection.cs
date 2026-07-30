using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskRegister.Core.Repositories;
using RiskRegister.Core.Services;
using RiskRegister.Infrastructure.Persistence;
using RiskRegister.Infrastructure.Persistence.Repositories;

namespace RiskRegister.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the data layer and the services built on it, so the API project never names a
    /// concrete persistence type.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RiskRegister")
            ?? throw new InvalidOperationException(
                "Connection string 'RiskRegister' is not configured. Set ConnectionStrings:RiskRegister "
                + "in appsettings.Development.json or user-secrets.");

        services.AddDbContext<RiskRegisterDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Scoped, matching the DbContext's lifetime: a repository must not outlive the context it
        // holds, and a singleton would capture one across every request.
        services.AddScoped<IRiskRepository, RiskRepository>();
        services.AddScoped<IRiskService, RiskService>();

        return services;
    }
}
