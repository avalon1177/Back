using Xunit;

namespace Marketplace.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<MarketplaceApiFactory>, IAsyncLifetime
{
    protected IntegrationTestBase(MarketplaceApiFactory factory)
    {
        Factory = factory;
        Scenario = new TestScenarioBuilder(factory);
    }

    protected MarketplaceApiFactory Factory { get; }
    protected TestScenarioBuilder Scenario { get; }

    public Task InitializeAsync() => Factory.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
