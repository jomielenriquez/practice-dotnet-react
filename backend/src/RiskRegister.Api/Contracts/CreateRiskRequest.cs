using System.ComponentModel.DataAnnotations;
using RiskRegister.Core.Scoring;

namespace RiskRegister.Api.Contracts;

/// <summary>
/// The body of <c>POST /api/risks</c>.
/// </summary>
/// <remarks>
/// Plain DataAnnotations, no FluentValidation and no endpoint filter: <c>[ApiController]</c> turns a
/// failed <c>ModelState</c> into RFC 7807 <c>ValidationProblemDetails</c> —
/// <c>{ "errors": { "title": ["..."] } }</c> — keyed by the property name in camelCase, which is the
/// field-mappable shape SPEC.md asks for.
/// <para>
/// There is no <c>status</c> here on purpose: new risks are always <see cref="Core.Enums.RiskStatus.Open"/>.
/// Nor is there a <c>score</c> — SQL Server computes it — or a <c>createdUtc</c>, which the database
/// defaults. Accepting any of the three would let a caller submit a value the API then ignores.
/// </para>
/// </remarks>
public sealed record CreateRiskRequest
{
    private readonly string _title = string.Empty;
    private readonly string _owner = string.Empty;
    private readonly string? _description;

    /// <remarks>
    /// Trimmed on the way in, so validation, the response and the stored row all agree on one value.
    /// Trimming afterwards would let <c>"  ab  "</c> pass the 3-character minimum and then land in a
    /// column whose CHECK constraint rejects it — a 503 for what is really a 400. A whitespace-only
    /// title trims to empty and is caught by <see cref="RequiredAttribute"/>.
    /// </remarks>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    public string Title
    {
        get => _title;
        init => _title = value?.Trim() ?? string.Empty;
    }

    /// <remarks>
    /// Blank and absent mean the same thing, so both normalise to null rather than storing an empty
    /// string that the frontend would have to treat as "no description" anyway.
    /// </remarks>
    [StringLength(2000, ErrorMessage = "Description must be 2000 characters or fewer.")]
    public string? Description
    {
        get => _description;
        init => _description = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <inheritdoc cref="Title"/>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Owner is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Owner must be between 1 and 100 characters.")]
    public string Owner
    {
        get => _owner;
        init => _owner = value?.Trim() ?? string.Empty;
    }

    /// <remarks>
    /// <c>int?</c>, not <c>byte</c>, for two reasons. A missing <c>likelihood</c> would bind a
    /// non-nullable value type to 0 and report it as out of range rather than missing; nullable makes
    /// "absent" distinguishable, so <see cref="RequiredAttribute"/> can say so. And a body carrying
    /// <c>300</c> overflows a <c>byte</c> during deserialisation, producing a JSON error instead of
    /// the range message. The narrowing to <c>byte</c> happens in the controller, after validation.
    /// </remarks>
    [Required(ErrorMessage = "Likelihood is required.")]
    [Range(RiskScoring.MinAxis, RiskScoring.MaxAxis,
        ErrorMessage = "Likelihood must be an integer between 1 and 5.")]
    public int? Likelihood { get; init; }

    /// <inheritdoc cref="Likelihood"/>
    [Required(ErrorMessage = "Impact is required.")]
    [Range(RiskScoring.MinAxis, RiskScoring.MaxAxis,
        ErrorMessage = "Impact must be an integer between 1 and 5.")]
    public int? Impact { get; init; }
}
