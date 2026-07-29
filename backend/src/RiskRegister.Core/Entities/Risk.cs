using RiskRegister.Core.Enums;
using RiskRegister.Core.Scoring;

namespace RiskRegister.Core.Entities;

/// <summary>
/// A potential future event that could harm the business, scored on likelihood and impact.
/// Maps to dbo.Risks.
/// </summary>
public class Risk
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The person accountable for the risk.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>How probable the event is, 1–5.</summary>
    public byte Likelihood { get; set; }

    /// <summary>How damaging the event would be, 1–5.</summary>
    public byte Impact { get; set; }

    /// <summary>
    /// Likelihood × impact, 1–25. Computed and persisted by SQL Server, so it can be indexed
    /// and ordered on and can never disagree with its inputs. Never written by the application:
    /// the setter exists only for EF to populate after an INSERT.
    /// </summary>
    public int Score { get; private set; }

    public RiskStatus Status { get; set; } = RiskStatus.Open;

    /// <summary>Server-assigned creation instant, always UTC.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// The band <see cref="Score"/> falls into. Derived, not mapped to a column — storing it
    /// would only create a second place for the boundaries to be wrong.
    /// </summary>
    public Severity Severity => RiskScoring.SeverityFor(Score);
}
