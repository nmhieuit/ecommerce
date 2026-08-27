using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PactNet.Verifier;
using Xunit.Abstractions;
using Products.Api.Data;

namespace Products.Api.ContractTests;

/// <summary>
/// This service's own build, verifying its real catalog responses against what the BFF says it
/// relies on (<c>pacts/bff-products.json</c>). A change to <c>ProductResponse</c> that drops or
/// renames a field the BFF reads fails here, in the products build, rather than in the BFF's
/// (011-consumer-contract-tests FR-001, FR-003, FR-005; spec SC-002).
/// </summary>
/// <remarks>
/// The pact is read from the repository's committed <c>pacts/</c> directory rather than from a
/// broker: the file is the exchange mechanism for now, and standing a broker up is ADR-0006 Action
/// Item 1 (research.md Decision 2).
/// </remarks>
public class ProductsProviderPactTests(SqlServerFixture sqlServer, ITestOutputHelper output)
    : IClassFixture<SqlServerFixture>
{
    /// <summary>Matches the tenant the consumer pact relays. Any non-blank value would do.</summary>
    private const string TenantId = "contoso";

    [Fact]
    public async Task CatalogResponses_SatisfyTheBffsRecordedExpectations()
    {
        using var provider = new PactProviderHost(sqlServer.ConnectionString, TenantId, ApplyStateAsync);

        // Forces the host to build and start before the verifier is pointed at it. The factory
        // creates its host lazily, so without this BaseUri is still the placeholder.
        using var _ = provider.CreateDefaultClient();

        await MigrateAsync(provider);

        using var verifier = new PactVerifier(
            "products",
            new PactVerifierConfig { Outputters = [new PactTestOutput(output)] });

        verifier
            .WithHttpEndpoint(provider.BaseUri)
            .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-products.json")))
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
        await scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Puts the catalog into the state an interaction was recorded under. Each state states the
    /// whole catalog it needs rather than adding to whatever the last one left, so interactions
    /// stay independent of the order the verifier replays them in.
    /// </summary>
    private static async Task ApplyStateAsync(ProviderState state, IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<ProductsDbContext>();
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
            case "the catalog contains at least one product":
                dbContext.Products.RemoveRange(dbContext.Products);
                dbContext.Products.Add(new Product
                {
                    Id = new Guid("9f8d6b1e-0001-4000-8000-000000000001"),
                    Name = "Field Notes Notebook",
                    Price = 12.50m,
                });
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
