using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskRegister.Infrastructure.Persistence;

namespace RiskRegister.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the data layer. Repository implementations are registered here too once they
    /// exist, so the API project never names a concrete persistence type.
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

        return services;
    }
}
