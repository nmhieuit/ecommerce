using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace Orders.Api.ContractTests;

/// <summary>
/// This service's own build, verifying its real order responses against what the BFF says it relies
/// on (<c>pacts/bff-orders.json</c>). A renamed or dropped field the BFF reads fails here, in the
/// orders build, rather than in the BFF's (011-consumer-contract-tests FR-001, FR-003, FR-005;
/// spec SC-002).
/// </summary>
/// <remarks>
/// The pact says nothing about <c>tenantId</c>, which this service does return. That is the
/// tolerant-reader rule working as intended (FR-007): the BFF's <c>OrderResource</c> never reads
/// it, so this service stays free to change it without breaking a consumer that does not depend
/// on it. Verification passing here is evidence of that, not an oversight.
/// </remarks>
public class OrdersProviderPactTests(SqlServerFixture sqlServer, ITestOutputHelper output)
    : IClassFixture<SqlServerFixture>
{
    /// <summary>Matches the tenant the consumer pact relays. Any non-blank value would do.</summary>
    private const string TenantId = "contoso";

    private static readonly Guid SeededProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public void OrderResponses_SatisfyTheBffsRecordedExpectations()
    {
        using var provider = new PactProviderHost(sqlServer.ConnectionString, TenantId, ApplyStateAsync);

        // Forces the host to build and start before the verifier is pointed at it. The factory
        // creates its host lazily, so without this BaseUri is still the placeholder.
        using var _ = provider.CreateDefaultClient();

        using var verifier = new PactVerifier(
            "orders",
            new PactVerifierConfig { Outputters = [new PactTestOutput(output)] });

        verifier
            .WithHttpEndpoint(provider.BaseUri)
            .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-orders.json")))
            .WithProviderStateUrl(provider.ProviderStateUri)
            .Verify();
    }

    /// <summary>
    /// Puts this service's orders into the state an interaction was recorded under. The order is
    /// built through <see cref="Order.PlaceFrom"/> rather than by setting properties, so the row
    /// the read route answers with is one this service's own domain rules produced.
    /// </summary>
    private static async Task ApplyStateAsync(ProviderState state, IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.MigrateAsync();

        // An interaction recorded without a Given depends on no prior state — placing an order
        // needs nothing in the database beforehand. The verifier still announces it, with an empty
        // description, so it is answered here rather than falling through to the unknown-state
        // guard below.
        if (string.IsNullOrEmpty(state.State))
        {
            return;
        }

        switch (state.State)
        {
            case "an order exists":
                dbContext.Orders.RemoveRange(dbContext.Orders);

                var order = Order.PlaceFrom(
                    [new OrderLine(SeededProductId, Quantity: 2, UnitPrice: 12.50m)],
                    DateTime.UtcNow,
                    TenantId);

                // The consumer recorded the interaction against a specific id, so the seeded row
                // has to carry that id for the replayed GET to reach it.
                order.Id = new Guid(state.Require("orderId"));

                dbContext.Orders.Add(order);
                await dbContext.SaveChangesAsync();
                break;

            default:
                // Loudly, not silently: a pact whose state nothing here recognises would otherwise
                // be verified against whatever the database happened to hold.
                throw new InvalidOperationException(
                    $"No provider state handler for '{state.State}'. Add one, or correct the consumer pact.");
        }
    }
}
