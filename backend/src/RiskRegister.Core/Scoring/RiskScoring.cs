using RiskRegister.Core.Enums;

namespace RiskRegister.Core.Scoring;

/// <summary>
/// The scoring rules from SPEC.md. This is the single place the band boundaries are defined —
/// severity is never stored, so there is nothing to keep in sync.
/// </summary>
public static class RiskScoring
{
    /// <summary>Lowest value accepted for likelihood and impact.</summary>
    public const byte MinAxis = 1;

    /// <summary>Highest value accepted for likelihood and impact.</summary>
    public const byte MaxAxis = 5;

    /// <summary>
    /// Maps a risk score to its severity band.
    /// </summary>
    /// <remarks>
    /// SPEC.md: 1–4 Low, 5–9 Medium, 10–15 High, 16–25 Critical. Written as open-ended
    /// comparisons rather than ranges so every score is covered, including any that fall
    /// outside 1–25 should the axis range ever change.
    /// </remarks>
    public static Severity SeverityFor(int score) => score switch
    {
        >= 16 => Severity.Critical,
        >= 10 => Severity.High,
        >= 5 => Severity.Medium,
        _ => Severity.Low
    };

    /// <summary>Whether a likelihood or impact value is within the allowed range.</summary>
    public static bool IsValidAxis(int value) => value is >= MinAxis and <= MaxAxis;
}
