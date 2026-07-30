using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiskRegister.Api.Contracts;
using RiskRegister.Api.Controllers;
using RiskRegister.Core.Enums;
using RiskRegister.Core.Services;
using RiskRegister.Tests.Fakes;

namespace RiskRegister.Tests;

public class RisksControllerTests
{
    private static RisksController ControllerOver(FakeRiskRepository repository) =>
        new(new RiskService(repository))
        {
            // ValidationProblem() reads ProblemDetailsFactory off the controller context, which
            // MVC supplies at runtime and a bare `new` does not.
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };

    private static IReadOnlyList<RiskResponse> OkPayload(ActionResult<IReadOnlyList<RiskResponse>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        return Assert.IsAssignableFrom<IReadOnlyList<RiskResponse>>(ok.Value);
    }

    [Fact]
    public async Task Returns_every_risk_when_no_status_is_given()
    {
        var repository = new FakeRiskRepository(
            FakeRiskRepository.Create(1, "Open one", 5, 5),
            FakeRiskRepository.Create(2, "Closed one", 1, 1, RiskStatus.Closed));
        var controller = ControllerOver(repository);

        var payload = OkPayload(await controller.GetRisks(null, CancellationToken.None));

        Assert.Equal(2, payload.Count);
        Assert.Null(repository.LastStatus);
    }

    [Theory]
    [InlineData("Open", RiskStatus.Open)]
    [InlineData("open", RiskStatus.Open)]
    [InlineData("MITIGATING", RiskStatus.Mitigating)]
    public async Task Passes_the_parsed_status_to_the_service(string raw, RiskStatus expected)
    {
        var repository = new FakeRiskRepository();
        var controller = ControllerOver(repository);

        await controller.GetRisks(raw, CancellationToken.None);

        Assert.Equal(expected, repository.LastStatus);
    }

    [Fact]
    public async Task Filters_the_register_by_status()
    {
        var repository = new FakeRiskRepository(
            FakeRiskRepository.Create(1, "Still open", 5, 5),
            FakeRiskRepository.Create(2, "Done with", 1, 1, RiskStatus.Closed));
        var controller = ControllerOver(repository);

        var payload = OkPayload(await controller.GetRisks("Closed", CancellationToken.None));

        Assert.Equal("Done with", Assert.Single(payload).Title);
    }

    [Theory]
    [InlineData("Nonsense")]
    [InlineData("2")]
    [InlineData("99")]
    public async Task Rejects_an_unknown_status_with_a_field_mapped_400(string raw)
    {
        var repository = new FakeRiskRepository(FakeRiskRepository.Create(1, "Ignored", 3, 3));
        var controller = ControllerOver(repository);

        var result = await controller.GetRisks(raw, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        // Keyed by the parameter name so the frontend can map it back to the filter control.
        var messages = Assert.Contains("status", problem.Errors.AsReadOnly());
        Assert.Contains(raw, messages.Single());
        Assert.Contains("Open, Mitigating, Accepted, Closed", messages.Single());

        // A rejected request must not reach the database at all.
        Assert.Equal(0, repository.GetAllCallCount);
    }

    [Fact]
    public async Task An_empty_register_is_200_with_an_empty_array_not_204()
    {
        var controller = ControllerOver(new FakeRiskRepository());

        var payload = OkPayload(await controller.GetRisks(null, CancellationToken.None));

        Assert.Empty(payload);
    }

    [Fact]
    public async Task Maps_score_and_severity_onto_the_response()
    {
        var repository = new FakeRiskRepository(FakeRiskRepository.Create(7, "Total data loss", 5, 5));
        var controller = ControllerOver(repository);

        var risk = Assert.Single(OkPayload(await controller.GetRisks(null, CancellationToken.None)));

        Assert.Equal(7, risk.Id);
        Assert.Equal(25, risk.Score);
        Assert.Equal(Severity.Critical, risk.Severity);
        Assert.Equal(RiskStatus.Open, risk.Status);
    }

    [Fact]
    public async Task Flows_the_cancellation_token_to_the_data_layer()
    {
        var repository = new FakeRiskRepository();
        var controller = ControllerOver(repository);
        using var cts = new CancellationTokenSource();

        await controller.GetRisks(null, cts.Token);

        Assert.Equal(cts.Token, repository.LastCancellationToken);
    }

    // --- POST /api/risks -------------------------------------------------------------------
    //
    // These call the action directly, so [ApiController]'s automatic 400 never runs: an invalid
    // body is rejected by MVC before the method is entered. The rules themselves are pinned in
    // CreateRiskRequestTests, which validates the DTO the same way the framework does.

    private static CreateRiskRequest ValidRequest(int likelihood = 3, int impact = 3) => new()
    {
        Title = "Customer database has no tested restore path",
        Description = "Nightly backups report success; a restore has never been attempted.",
        Owner = "Priya Raman",
        Likelihood = likelihood,
        Impact = impact
    };

    private static RiskResponse CreatedPayload(ActionResult<RiskResponse> result)
    {
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        return Assert.IsType<RiskResponse>(created.Value);
    }

    [Fact]
    public async Task Create_returns_201_with_the_created_risk()
    {
        var repository = new FakeRiskRepository();
        var controller = ControllerOver(repository);

        var payload = CreatedPayload(await controller.CreateRisk(ValidRequest(), CancellationToken.None));

        Assert.Equal("Customer database has no tested restore path", payload.Title);
        Assert.Equal("Priya Raman", payload.Owner);
        Assert.NotEqual(0, payload.Id);
        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task Create_points_Location_at_the_register()
    {
        var controller = ControllerOver(new FakeRiskRepository());

        var result = await controller.CreateRisk(ValidRequest(), CancellationToken.None);

        // Not /api/risks/{id}: there is no GET-by-id endpoint, so a Location header naming one
        // would 404.
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(RisksController.GetRisks), created.ActionName);
    }

    [Theory]
    [InlineData(3, 3, 9, Severity.Medium)]
    [InlineData(5, 5, 25, Severity.Critical)]
    [InlineData(1, 1, 1, Severity.Low)]
    [InlineData(2, 5, 10, Severity.High)]
    public async Task Create_returns_the_server_computed_score_and_severity(
        int likelihood,
        int impact,
        int expectedScore,
        Severity expectedSeverity)
    {
        var controller = ControllerOver(new FakeRiskRepository());

        var payload = CreatedPayload(
            await controller.CreateRisk(ValidRequest(likelihood, impact), CancellationToken.None));

        Assert.Equal(expectedScore, payload.Score);
        Assert.Equal(expectedSeverity, payload.Severity);
    }

    [Fact]
    public async Task Create_starts_a_new_risk_Open()
    {
        var controller = ControllerOver(new FakeRiskRepository());

        var payload = CreatedPayload(await controller.CreateRisk(ValidRequest(), CancellationToken.None));

        Assert.Equal(RiskStatus.Open, payload.Status);
    }

    [Fact]
    public async Task A_created_risk_appears_in_the_register()
    {
        var repository = new FakeRiskRepository();
        var controller = ControllerOver(repository);

        await controller.CreateRisk(ValidRequest(5, 5), CancellationToken.None);
        var register = OkPayload(await controller.GetRisks(null, CancellationToken.None));

        Assert.Equal(25, Assert.Single(register).Score);
    }

    [Fact]
    public async Task Create_flows_the_cancellation_token_to_the_data_layer()
    {
        var repository = new FakeRiskRepository();
        var controller = ControllerOver(repository);
        using var cts = new CancellationTokenSource();

        await controller.CreateRisk(ValidRequest(), cts.Token);

        Assert.Equal(cts.Token, repository.LastCancellationToken);
    }
}
