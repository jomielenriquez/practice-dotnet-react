namespace RiskRegister.Core.Enums;

/// <summary>
/// The lifecycle state of a risk, per SPEC.md.
/// </summary>
/// <remarks>
/// Persisted as its <em>name</em>, not its numeric value — see RiskConfiguration. The names are
/// therefore part of both the database contract and the API contract, and cannot be renamed
/// without a migration.
/// </remarks>
public enum RiskStatus
{
    Open,
    Mitigating,
    Accepted,
    Closed
}
