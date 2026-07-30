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
        Assert.Equal(1, repository.CallCount);
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
}
