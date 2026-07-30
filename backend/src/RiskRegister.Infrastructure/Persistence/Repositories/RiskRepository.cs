using Microsoft.EntityFrameworkCore;
using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;
using RiskRegister.Core.Repositories;

namespace RiskRegister.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IRiskRepository"/>
public class RiskRepository(RiskRegisterDbContext db) : IRiskRepository
{
    private readonly RiskRegisterDbContext _db = db;

    public async Task<IReadOnlyList<Risk>> GetAllAsync(
        RiskStatus? status,
        CancellationToken cancellationToken)
    {
        // AsNoTracking: this is a read, and the change tracker would only cost memory.
        var query = _db.Risks.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(risk => risk.Status == status);
        }

        // Matches the key order of IX_Risks_Score and IX_Risks_Status_Score exactly, so the sort
        // comes free from the index. CreatedUtc and Id are the tie-break: 3 × 4 and 4 × 3 both
        // score 12, which SPEC.md leaves undefined.
        //
        // Sort on Score, never on Severity — Severity is Ignore()d in RiskConfiguration, so EF
        // cannot translate it and would throw at runtime.
        return await query
            .OrderByDescending(risk => risk.Score)
            .ThenByDescending(risk => risk.CreatedUtc)
            .ThenByDescending(risk => risk.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Risk> AddAsync(Risk risk, CancellationToken cancellationToken)
    {
        _db.Risks.Add(risk);
        await _db.SaveChangesAsync(cancellationToken);

        // No re-query: Id, Score and CreatedUtc are all store-generated, so EF adds them to the
        // INSERT's OUTPUT clause and writes them back onto this instance. Score in particular
        // cannot be computed here — the persisted computed column is the only definition of it.
        return risk;
    }
}
