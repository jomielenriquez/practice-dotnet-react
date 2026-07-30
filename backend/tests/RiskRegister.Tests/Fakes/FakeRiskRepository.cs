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

    /// <summary>The token passed to the last call, whichever method it was.</summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>The risk handed to the last <see cref="AddAsync"/> call.</summary>
    public Risk? LastAdded { get; private set; }

    public int GetAllCallCount { get; private set; }

    public int AddCallCount { get; private set; }

    public Task<IReadOnlyList<Risk>> GetAllAsync(
        RiskStatus? status,
        CancellationToken cancellationToken)
    {
        LastStatus = status;
        LastCancellationToken = cancellationToken;
        GetAllCallCount++;

        IReadOnlyList<Risk> result = status is null
            ? _risks
            : [.. _risks.Where(risk => risk.Status == status)];

        return Task.FromResult(result);
    }

    /// <summary>
    /// Stands in for the INSERT, including the three columns the database owns.
    /// </summary>
    /// <remarks>
    /// The real repository gets <c>Id</c>, <c>Score</c> and <c>CreatedUtc</c> back from the INSERT's
    /// OUTPUT clause; a fake that left them at zero would let a controller drop them and still pass.
    /// So they are assigned here, <c>Score</c> by the same reflection <see cref="Create"/> uses.
    /// </remarks>
    public Task<Risk> AddAsync(Risk risk, CancellationToken cancellationToken)
    {
        LastAdded = risk;
        LastCancellationToken = cancellationToken;
        AddCallCount++;

        risk.Id = _risks.Count == 0 ? 1 : _risks.Max(existing => existing.Id) + 1;
        risk.CreatedUtc = CreatedUtcForNewRisks;
        SetScore(risk, risk.Likelihood * risk.Impact);

        _risks.Add(risk);

        return Task.FromResult(risk);
    }

    /// <summary>The instant this fake stamps onto inserted risks, standing in for SYSUTCDATETIME().</summary>
    public static readonly DateTimeOffset CreatedUtcForNewRisks =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

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

        SetScore(risk, likelihood * impact);

        return risk;
    }

    private static void SetScore(Risk risk, int score) =>
        typeof(Risk)
            .GetProperty(nameof(Risk.Score), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(risk, score);
}
