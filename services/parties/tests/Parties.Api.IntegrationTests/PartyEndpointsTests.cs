using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Parties.Api.Data;

namespace Parties.Api.IntegrationTests;

/// <summary>
/// The party read surface the BFF's party route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory provider.
/// </summary>
public class PartyEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task GetParty_ReturnsTheParty_WhenItExists()
    {
        var party = new Party { Id = Guid.NewGuid(), DisplayName = "Ada Lovelace" };

        await using var factory = await CreateFactoryWithPartiesAsync([party]);
        var client = factory.CreateClient();

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
        var client = factory.CreateClient();

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
        var dbContext = scope.ServiceProvider.GetRequiredService<PartiesDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Parties.RemoveRange(dbContext.Parties);
        dbContext.Parties.AddRange(parties);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    private sealed record PartyResponse(Guid Id, string DisplayName);
}
