using Microsoft.EntityFrameworkCore;
using RiskRegister.Core.Entities;

namespace RiskRegister.Infrastructure.Persistence;

public class RiskRegisterDbContext(DbContextOptions<RiskRegisterDbContext> options)
    : DbContext(options)
{
    public DbSet<Risk> Risks => Set<Risk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RiskRegisterDbContext).Assembly);
    }
}
