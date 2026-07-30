using System.Reflection;
using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;
using RiskRegister.Core.Repositories;

namespace RiskRegister.Tests.Fakes;

/// <summary>
/// An in-memory <see cref="IRiskRepository"/> that records what it was asked for.
/// </summary>
/// <remarks>
/// Hand-written rather than mocked: the interface has two members, and a fake that records its
/// arguments reads better in the assertions than a mock's setup/verify pair.
/// <para>
/// It returns rows in the order it was given them. It deliberately does <em>not</em> sort — ordering
/// is SQL's job, and a fake that re-sorted would let a broken <c>ORDER BY</c> pass.
/// </para>
/// </remarks>
public class FakeRiskRepository(params Risk[] risks) : IRiskRepository
{
    private readonly List<Risk> _risks = [.. risks];

    /// <summary>The status passed to the last <see cref="GetAllAsync"/> call.</summary>
    public RiskStatus? LastStatus { get; private set; }

    /// <summary>The token passed to the last <see cref="GetAllAsync"/> call.</summary>
    public CancellationToken LastCancellationToken { get; private set; }

    public int CallCount { get; private set; }

    public Task<IReadOnlyList<Risk>> GetAllAsync(
        RiskStatus? status,
        CancellationToken cancellationToken)
    {
        LastStatus = status;
        LastCancellationToken = cancellationToken;
        CallCount++;

        IReadOnlyList<Risk> result = status is null
            ? _risks
            : [.. _risks.Where(risk => risk.Status == status)];

        return Task.FromResult(result);
    }

    public Task<Risk> AddAsync(Risk risk, CancellationToken cancellationToken) =>
        throw new NotSupportedException("POST is not implemented yet.");

    /// <summary>
    /// Builds a <see cref="Risk"/> with a populated <c>Score</c>.
    /// </summary>
    /// <remarks>
    /// <c>Score</c> has a private setter because SQL Server computes it — which is correct for
    /// production and inconvenient here, since no test has a database to compute it. Reflection is
    /// confined to this one method rather than loosening the entity's accessibility for tests.
    /// </remarks>
    public static Risk Create(
        int id,
        string title,
        byte likelihood,
        byte impact,
        RiskStatus status = RiskStatus.Open,
        DateTimeOffset? createdUtc = null)
    {
        var risk = new Risk
        {
            Id = id,
            Title = title,
            Owner = "Test Owner",
            Likelihood = likelihood,
            Impact = impact,
            Status = status,
            CreatedUtc = createdUtc ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        typeof(Risk)
            .GetProperty(nameof(Risk.Score), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(risk, likelihood * impact);

        return risk;
    }
}
