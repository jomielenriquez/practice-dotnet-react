using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;

namespace RiskRegister.Core.Repositories;

/// <summary>
/// Persistence for <see cref="Risk"/>. Declared here, in Core, so the service layer depends on
/// the abstraction and never sees a DbContext; the implementation lives in Infrastructure.
/// </summary>
public interface IRiskRepository
{
    /// <summary>
    /// Returns the register ordered by score descending, then newest first, then by id — the
    /// last two are the tie-break, since likelihood 3 × impact 4 and 4 × 3 both score 12.
    /// </summary>
    /// <param name="status">Optional status filter; null returns every risk.</param>
    Task<IReadOnlyList<Risk>> GetAllAsync(RiskStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a risk and returns it with the database-assigned <see cref="Risk.Id"/>,
    /// <see cref="Risk.Score"/> and <see cref="Risk.CreatedUtc"/> populated.
    /// </summary>
    Task<Risk> AddAsync(Risk risk, CancellationToken cancellationToken);
}
