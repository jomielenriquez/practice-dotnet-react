using RiskRegister.Core.Entities;
using RiskRegister.Core.Enums;
using RiskRegister.Core.Services;
using RiskRegister.Tests.Fakes;

namespace RiskRegister.Tests;

public class RiskServiceTests
{
    [Fact]
    public async Task Preserves_the_order_the_repository_returned()
    {
        // Handed back deliberately unsorted. The service must not "helpfully" re-sort: ordering
        // happens in SQL so the register's indexes can serve it, and sorting again in memory would
        // mask a broken ORDER BY.
        var repository = new FakeRiskRepository(
            FakeRiskRepository.Create(1, "Middle", 3, 4),
            FakeRiskRepository.Create(2, "Worst", 5, 5),
            FakeRiskRepository.Create(3, "Least", 1, 1));
        var service = new RiskService(repository);

        var result = await service.GetRegisterAsync(null, CancellationToken.None);

        Assert.Equal(["Middle", "Worst", "Least"], result.Select(risk => risk.Title));
    }

    [Fact]
    public async Task Passes_the_status_filter_through_untouched()
    {
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);

        await service.GetRegisterAsync(RiskStatus.Mitigating, CancellationToken.None);

        Assert.Equal(RiskStatus.Mitigating, repository.LastStatus);
        Assert.Equal(1, repository.GetAllCallCount);
    }

    [Fact]
    public async Task Passes_the_cancellation_token_through()
    {
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);
        using var cts = new CancellationTokenSource();

        await service.GetRegisterAsync(null, cts.Token);

        Assert.Equal(cts.Token, repository.LastCancellationToken);
    }

    [Fact]
    public async Task Returns_an_empty_list_for_an_empty_register()
    {
        var service = new RiskService(new FakeRiskRepository());

        var result = await service.GetRegisterAsync(null, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Create_hands_the_repository_the_submitted_values()
    {
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);

        await service.CreateAsync(
            "Backups have never been restore-tested",
            "Nightly job reports success but no restore has been attempted.",
            "Priya Raman",
            likelihood: 4,
            impact: 5,
            CancellationToken.None);

        var added = Assert.IsType<Risk>(repository.LastAdded);
        Assert.Equal("Backups have never been restore-tested", added.Title);
        Assert.Equal("Nightly job reports success but no restore has been attempted.", added.Description);
        Assert.Equal("Priya Raman", added.Owner);
        Assert.Equal(4, added.Likelihood);
        Assert.Equal(5, added.Impact);
        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task Create_defaults_a_new_risk_to_Open()
    {
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);

        var created = await service.CreateAsync("A new risk", null, "Owner", 1, 1, CancellationToken.None);

        Assert.Equal(RiskStatus.Open, created.Status);
    }

    [Fact]
    public async Task Create_keeps_a_null_description_null()
    {
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);

        var created = await service.CreateAsync("A new risk", null, "Owner", 2, 2, CancellationToken.None);

        Assert.Null(created.Description);
    }

    [Fact]
    public async Task Create_returns_the_risk_with_the_store_generated_values_populated()
    {
        // Id, Score and CreatedUtc all come back from the INSERT, never from C#. The fake stands in
        // for that, so the assertion is that the service passes them through rather than that it
        // computes them.
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);

        var created = await service.CreateAsync("A new risk", null, "Owner", 3, 4, CancellationToken.None);

        Assert.NotEqual(0, created.Id);
        Assert.Equal(12, created.Score);
        Assert.Equal(Severity.High, created.Severity);
        Assert.Equal(FakeRiskRepository.CreatedUtcForNewRisks, created.CreatedUtc);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(6, 3)]
    [InlineData(3, 0)]
    [InlineData(3, 6)]
    public async Task Create_rejects_an_axis_outside_1_to_5(byte likelihood, byte impact)
    {
        // The DTO returns a 400 long before this, but the guard keeps a programming error from
        // reaching the database's CHECK constraint and surfacing as a 503.
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync("A new risk", null, "Owner", likelihood, impact, CancellationToken.None));

        Assert.Equal(0, repository.AddCallCount);
    }

    [Theory]
    [InlineData("", "Owner")]
    [InlineData("   ", "Owner")]
    [InlineData("A new risk", "")]
    [InlineData("A new risk", "   ")]
    public async Task Create_rejects_a_blank_title_or_owner(string title, string owner)
    {
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(title, null, owner, 3, 3, CancellationToken.None));

        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task Create_passes_the_cancellation_token_through()
    {
        var repository = new FakeRiskRepository();
        var service = new RiskService(repository);
        using var cts = new CancellationTokenSource();

        await service.CreateAsync("A new risk", null, "Owner", 3, 3, cts.Token);

        Assert.Equal(cts.Token, repository.LastCancellationToken);
    }
}
