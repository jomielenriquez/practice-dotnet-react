using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;
using RiskRegister.Core.Repositories;

namespace RiskRegister.Core.Services;

/// <inheritdoc cref="IRiskService"/>
public class RiskService(IRiskRepository repository) : IRiskService
{
    private readonly IRiskRepository _repository = repository;

    /// <remarks>
    /// The ordering is the repository's job, not this method's: it has to happen in SQL so the
    /// register's indexes can serve it. Re-sorting here would silently discard that.
    /// </remarks>
    public Task<IReadOnlyList<Risk>> GetRegisterAsync(
        RiskStatus? status,
        CancellationToken cancellationToken) =>
        _repository.GetAllAsync(status, cancellationToken);

    public Task<Risk> CreateAsync(
        string title,
        string? description,
        string owner,
        byte likelihood,
        byte impact,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("POST /api/risks is a separate ticket.");
}
