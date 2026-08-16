using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Parties.Api.Data;
using Tenancy;

namespace Parties.Api.IntegrationTests;

/// <summary>
/// The party read surface the BFF's party route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory provider.
/// </summary>
public class PartyEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// The tenant these tests seed and read as. Any non-blank value works — this suite is about the
    /// party surface, not about which tenant resolved; that is
    /// <see cref="TenantEnforcementTests"/>' subject.
    /// </summary>
    private const string SeedTenantId = "contoso";

    [Fact]
    public async Task GetParty_ReturnsTheParty_WhenItExists()
    {
        var party = new Party { Id = Guid.NewGuid(), DisplayName = "Ada Lovelace" };

        await using var factory = await CreateFactoryWithPartiesAsync([party]);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/parties/{party.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<PartyResponse>();
        Assert.NotNull(actual);
        Assert.Equal(party.Id, actual.Id);
        Assert.Equal(party.DisplayName, actual.DisplayName);
    }

    [Fact]
    public async Task GetParty_ReturnsNotFound_WhenNoPartyHasThatId()
    {
        await using var factory = await CreateFactoryWithPartiesAsync([]);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/parties/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryWithPartiesAsync(
        IReadOnlyCollection<Party> parties)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PartiesDb"] = sqlServer.ConnectionString,
                })));

        using var scope = factory.Services.CreateScope();

        // No HTTP request runs for this scope, so TenantContextMiddleware never populates it —
        // seeding must prime the tenant itself or the gated registration throws (research.md
        // Decision 7).
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<PartiesDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Parties.RemoveRange(dbContext.Parties);
        dbContext.Parties.AddRange(parties);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    /// <summary>
    /// A client whose requests carry the tenant the gateway would have resolved. Without it every
    /// request here is Unresolved and never reaches persistence at all.
    /// </summary>
    private static HttpClient CreateTenantClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, SeedTenantId);

        return client;
    }

    private sealed record PartyResponse(Guid Id, string DisplayName);
}
