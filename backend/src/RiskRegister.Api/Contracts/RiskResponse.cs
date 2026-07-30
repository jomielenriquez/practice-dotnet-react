using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;

namespace RiskRegister.Api.Contracts;

/// <summary>
/// A risk as the API returns it. Mirrored by <c>Risk</c> in frontend/src/api/types.ts.
/// </summary>
/// <remarks>
/// A DTO rather than returning <see cref="Risk"/> directly: the entity carries a DB-computed
/// <c>Score</c> with a private setter and a derived <c>Severity</c>, and the wire contract should
/// not be hostage to EF mapping decisions.
/// <para>
/// <see cref="Severity"/> and <see cref="Status"/> serialise as strings ("Critical", "Open") via the
/// JsonStringEnumConverter registered in Program.cs — the frontend keys its severity styling off
/// those names.
/// </para>
/// </remarks>
public sealed record RiskResponse(
    int Id,
    string Title,
    string? Description,
    string Owner,
    byte Likelihood,
    byte Impact,
    int Score,
    Severity Severity,
    RiskStatus Status,
    DateTimeOffset CreatedUtc)
{
    public static RiskResponse From(Risk risk) => new(
        risk.Id,
        risk.Title,
        risk.Description,
        risk.Owner,
        risk.Likelihood,
        risk.Impact,
        risk.Score,
        risk.Severity,
        risk.Status,
        risk.CreatedUtc);
}
