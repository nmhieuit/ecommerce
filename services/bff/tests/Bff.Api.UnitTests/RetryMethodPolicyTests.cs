using Bff.Api.DownstreamClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Api.UnitTests;

/// <summary>
/// 020-timeouts-retry-circuit-breaker User Story 3 (spec FR-006; Edge Case 1): a write call must
/// never be silently retried by the resilience pipeline. Before this feature,
/// <c>AddStandardResilienceHandler</c>'s default retry predicate does not look at the HTTP method at
/// all, so a <c>POST /basket/items</c> that timed out after the baskets service had already
/// processed it could be retried — and could add the item twice.
/// </summary>
/// <remarks>
/// Exercises <see cref="BasketsApiClient"/> directly through the real DI registration
/// (<see cref="DownstreamClientRegistrationExtensions.AddDownstreamClients"/>) with its primary
/// HTTP handler substituted for one that always fails and counts its own invocations — no ASP.NET
/// Core host or route mapping needed, since the policy under test lives entirely in the resilience
/// pipeline attached to the named <c>HttpClient</c>. <see cref="BasketsApiClient.GetCurrentBasketAsync"/>
/// (<c>GET</c>) and <see cref="BasketsApiClient.AddItemAsync"/> (<c>POST</c>) are both proxy calls
/// to the same client and the same pipeline configuration, so comparing their invocation counts
/// isolates the method-based restriction from everything else the pipeline does.
/// </remarks>
public class RetryMethodPolicyTests
{
    /// <summary>
    /// Matches <c>DownstreamClientRegistrationExtensions.MaxRetryAttempts</c>: 1 initial attempt + 2
    /// retries.
    /// </summary>
    private const int ExpectedAttemptsWhenRetried = 3;

    [Fact]
    public async Task GetCurrentBasket_IsRetried_OnATransientFailure()
    {
        var handler = new CountingFailingHandler();
        var basketsClient = BuildBasketsClient(handler);

        await Assert.ThrowsAnyAsync<Exception>(
            () => basketsClient.GetCurrentBasketAsync(CancellationToken.None));

        Assert.Equal(ExpectedAttemptsWhenRetried, handler.InvocationCount);
    }

    [Fact]
    public async Task AddItem_IsNeverRetried_OnATransientFailure()
    {
        var handler = new CountingFailingHandler();
        var basketsClient = BuildBasketsClient(handler);

        await Assert.ThrowsAnyAsync<Exception>(
            () => basketsClient.AddItemAsync(
                new AddBasketItemCommand(Guid.NewGuid(), Quantity: 1, UnitPrice: 9.99m),
                CancellationToken.None));

        // Exactly the one real attempt — a second would mean the resilience pipeline retried a
        // write, which is precisely the duplicate-side-effect risk spec FR-006 forbids.
        Assert.Equal(1, handler.InvocationCount);
    }

    private static BasketsApiClient BuildBasketsClient(HttpMessageHandler primaryHandler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:ProductsApi:BaseUrl"] = "http://products.test",
                ["Services:BasketsApi:BaseUrl"] = "http://baskets.test",
                ["Services:OrdersApi:BaseUrl"] = "http://orders.test",
                ["Services:PartiesApi:BaseUrl"] = "http://parties.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDownstreamClients(configuration);

        // Registered after AddDownstreamClients, exactly as
        // Bff.Api.IntegrationTests/BffTestHost.cs substitutes a downstream's transport — the named
        // client's resilience pipeline (retry, timeout, circuit breaker) stays exactly as
        // production configures it; only the socket underneath is replaced.
        services.AddHttpClient(BasketsApiClient.ServiceName)
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        return services.BuildServiceProvider().GetRequiredService<BasketsApiClient>();
    }

    /// <summary>
    /// Fails every call immediately with a transient-classified exception (matching
    /// <c>Bff.Api.IntegrationTests/BffTestHost.cs</c>'s <c>FailingTransportHandler</c>), counting how
    /// many times the resilience pipeline actually invoked it.
    /// </summary>
    private sealed class CountingFailingHandler : HttpMessageHandler
    {
        public int InvocationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            throw new HttpRequestException("Simulated transient failure.");
        }
    }
}
