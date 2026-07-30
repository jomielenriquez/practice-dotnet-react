using Microsoft.AspNetCore.Mvc;
using RiskRegister.Api.Contracts;
using RiskRegister.Core.Enums;
using RiskRegister.Core.Services;

namespace RiskRegister.Api.Controllers;

[ApiController]
[Route("api/risks")]
[Produces("application/json")]
public class RisksController(IRiskService riskService) : ControllerBase
{
    private readonly IRiskService _riskService = riskService;

    /// <summary>
    /// Returns the register, worst risks first, optionally filtered by status.
    /// </summary>
    /// <param name="status">
    /// Optional status filter, matched case-insensitively: <c>Open</c>, <c>Mitigating</c>,
    /// <c>Accepted</c> or <c>Closed</c>. Omitted or blank returns the whole register.
    /// </param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RiskResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RiskResponse>>> GetRisks(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        // Bound as a string, not as RiskStatus?, on purpose. The default enum binder rejects a bad
        // value with "The value 'Nonsense' is not valid.", which never tells the caller what *is*
        // valid. Parsing here lets the 400 name the four accepted values.
        if (!RiskStatusParser.TryParse(status, out var parsedStatus))
        {
            ModelState.AddModelError(
                nameof(status),
                $"'{status}' is not a valid status. Valid values are: {RiskStatusParser.ValidValues}.");

            // An unknown status is a typo, not "no matching risks". Returning [] would be
            // indistinguishable from an empty register and would hide the mistake silently.
            return ValidationProblem(ModelState);
        }

        var risks = await _riskService.GetRegisterAsync(parsedStatus, cancellationToken);

        // An empty register is 200 with [], never 204: the frontend needs a body to render its
        // empty state against.
        return Ok(risks.Select(RiskResponse.From).ToList());
    }

    /// <summary>
    /// Adds a risk to the register. New risks start <c>Open</c>; <c>score</c> and <c>severity</c>
    /// are computed server-side and returned on the created risk.
    /// </summary>
    /// <remarks>
    /// No explicit ModelState check: <c>[ApiController]</c> short-circuits an invalid body into a
    /// 400 <see cref="ValidationProblemDetails"/> before this method runs, which is why the
    /// nullable axes can be dereferenced below.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<RiskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RiskResponse>> CreateRisk(
        [FromBody] CreateRiskRequest request,
        CancellationToken cancellationToken)
    {
        var risk = await _riskService.CreateAsync(
            request.Title,
            request.Description,
            request.Owner,
            (byte)request.Likelihood!.Value,
            (byte)request.Impact!.Value,
            cancellationToken);

        // Location points at the register, not at /api/risks/{id}: there is no GET-by-id endpoint
        // (SPEC.md does not ask for one), and a Location header that 404s is worse than one that
        // resolves. The created risk is in the body either way, which is what the frontend uses.
        return CreatedAtAction(nameof(GetRisks), RiskResponse.From(risk));
    }
}
