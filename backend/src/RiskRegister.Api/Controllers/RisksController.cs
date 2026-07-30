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
}
