using Baskets.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace Baskets.Api.ContractTests;

/// <summary>
/// This service's own build, verifying its real basket responses against what the BFF says it
/// relies on (<c>pacts/bff-baskets.json</c>). A renamed field, or a status the BFF reads meaning
/// dropped, fails here — in the baskets build — rather than in the BFF's
/// (011-consumer-contract-tests FR-001, FR-003, FR-005; spec SC-002).
/// </summary>
public class BasketsProviderPactTests(SqlServerFixture sqlServer, ITestOutputHelper output)
    : IClassFixture<SqlServerFixture>
{
    /// <summary>Matches the tenant the consumer pact relays. Any non-blank value would do.</summary>
    private const string TenantId = "contoso";

    private static readonly Guid SeededProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task BasketResponses_SatisfyTheBffsRecordedExpectations()
    {
        using var provider = new PactProviderHost(sqlServer.ConnectionString, TenantId, ApplyStateAsync);

        // Forces the host to build and start before the verifier is pointed at it. The factory
        // creates its host lazily, so without this BaseUri is still the placeholder.
        using var _ = provider.CreateDefaultClient();

        await MigrateAsync(provider);

        using var verifier = new PactVerifier(
            "baskets",
            new PactVerifierConfig { Outputters = [new PactTestOutput(output)] });

        verifier
            .WithHttpEndpoint(provider.BaseUri)
            .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-baskets.json")))
            .WithProviderStateUrl(provider.ProviderStateUri)
            // A cold container can still make the first request slow even after the
            // migration above; the default budget is tight enough to trip on it.
            .WithRequestTimeout(TimeSpan.FromMinutes(2))
            .Verify();
    }


    /// <summary>
    /// Applies migrations once, before verification starts.
    /// </summary>
    /// <remarks>
    /// Not merely an optimisation. Pact gives each provider-state callback a short timeout, and the
    /// first <c>MigrateAsync</c> against a freshly started SQL Server container comfortably exceeds
    /// it — the state endpoint never answers in time and the verifier reports
    /// <c>error sending request for url (.../_pact/provider-states)</c>, which reads like the host
    /// is down rather than like a slow first migration. This suite passed locally for months
    /// because the container was already warm; it failed the moment it ran on cold CI.
    /// Migrating up front leaves the state handlers doing only fast row work.
    /// </remarks>
    private static async Task MigrateAsync(PactProviderHost provider)
    {
        using var scope = provider.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Tenancy.TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<BasketsDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Puts this service's baskets into the state an interaction was recorded under.
    /// </summary>
    /// <remarks>
    /// Every state clears the table first and then seeds only the caller it names. The two clear
    /// interactions differ solely in whether the caller's basket has anything in it, so a state
    /// that added to what the previous interaction left behind would make the 204 and the 409
    /// depend on replay order.
    /// </remarks>
    private static async Task ApplyStateAsync(ProviderState state, IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<BasketsDbContext>();
        await dbContext.Database.MigrateAsync();

        // An interaction recorded without a Given depends on no prior state — placing an order
        // needs nothing in the database beforehand. The verifier still announces it, with an empty
        // description, so it is answered here rather than falling through to the unknown-state
        // guard below.
        if (string.IsNullOrEmpty(state.State))
        {
            return;
        }

        dbContext.Baskets.RemoveRange(dbContext.Baskets);
        await dbContext.SaveChangesAsync();

        var basket = Basket.ForCustomer(state.Require("customerRef"));

        switch (state.State)
        {
            case "a basket holding one item exists for the caller":
                basket.AddItem(SeededProductId, quantity: 2, unitPrice: 12.50m);
                break;

            case "an empty basket exists for the caller":
                break;

            default:
                // Loudly, not silently: a pact whose state nothing here recognises would otherwise
                // be verified against whatever the database happened to hold.
                throw new InvalidOperationException(
                    $"No provider state handler for '{state.State}'. Add one, or correct the consumer pact.");
        }

        dbContext.Baskets.Add(basket);
        await dbContext.SaveChangesAsync();
    }
}
