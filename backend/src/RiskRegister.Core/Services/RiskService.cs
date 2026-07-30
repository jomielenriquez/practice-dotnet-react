using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;
using RiskRegister.Core.Repositories;
using RiskRegister.Core.Scoring;

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

    /// <remarks>
    /// The axis guards are a backstop, not the validation: the request DTO has already returned a
    /// field-mapped 400 by the time anything reaches here. They exist because this method is
    /// callable from anywhere in the application, and an out-of-range value that got past them
    /// would fail on the database's CHECK constraint as a 503 — an unhelpful way to learn about a
    /// programming error.
    /// <para>
    /// <c>Status</c>, <c>Score</c> and <c>CreatedUtc</c> are deliberately not set. Status defaults
    /// to <see cref="RiskStatus.Open"/> on the entity, and the other two are the database's:
    /// <c>Score</c> is a persisted computed column and <c>CreatedUtc</c> defaults to
    /// <c>SYSUTCDATETIME()</c>. All three come back populated on the returned instance.
    /// </para>
    /// </remarks>
    public Task<Risk> CreateAsync(
        string title,
        string? description,
        string owner,
        byte likelihood,
        byte impact,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        if (!RiskScoring.IsValidAxis(likelihood))
        {
            throw new ArgumentOutOfRangeException(
                nameof(likelihood),
                likelihood,
                $"Likelihood must be between {RiskScoring.MinAxis} and {RiskScoring.MaxAxis}.");
        }

        if (!RiskScoring.IsValidAxis(impact))
        {
            throw new ArgumentOutOfRangeException(
                nameof(impact),
                impact,
                $"Impact must be between {RiskScoring.MinAxis} and {RiskScoring.MaxAxis}.");
        }

        var risk = new Risk
        {
            Title = title,
            Description = description,
            Owner = owner,
            Likelihood = likelihood,
            Impact = impact
        };

        return _repository.AddAsync(risk, cancellationToken);
    }
}
