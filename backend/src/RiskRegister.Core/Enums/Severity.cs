namespace RiskRegister.Core.Enums;

/// <summary>
/// The band a risk score falls into. Derived from the score, never stored — see
/// <see cref="Scoring.RiskScoring"/>.
/// </summary>
public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}
