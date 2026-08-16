extern alias PartiesApi;

using System.Net;
using System.Net.Http.Json;
using PartiesApi::Parties.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// US1: the BFF serves party (customer) data so the SPA never addresses the parties service
/// directly (spec FR-002). Asserted against a real Parties.Api reading a real database
/// (research.md Decision 5).
/// </summary>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class PartiesRouteTests(DownstreamServicesFixture fixture)
{
    [Fact]
    public async Task GetParty_ReturnsShapedPartyFromThePartiesService()
    {
        var party = new Party { Id = Guid.NewGuid(), DisplayName = "Ada Lovelace" };

        await using var parties = await CreatePartiesServiceAsync("bff-parties", party);
        await using var bff = BffTestHost.CreateBff("PartiesApi", parties);
        var client = BffTestHost.CreateTenantClient(bff);

        var response = await client.GetAsync($"/bff/parties/{party.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<PartyResponse>();
        Assert.NotNull(actual);
        Assert.Equal(party.Id, actual.Id);
        Assert.Equal(party.DisplayName, actual.DisplayName);
    }

    [Fact]
    public async Task GetParty_ReturnsNotFound_WhenThePartiesServiceHasNoSuchParty()
    {
        await using var parties = await CreatePartiesServiceAsync("bff-parties-missing");
        await using var bff = BffTestHost.CreateBff("PartiesApi", parties);
        var client = BffTestHost.CreateTenantClient(bff);

        var response = await client.GetAsync($"/bff/parties/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<PartiesApi::Program>>
        CreatePartiesServiceAsync(string database, params Party[] parties) =>
        BffTestHost.CreateDownstreamAsync<PartiesApi::Program, PartiesDbContext>(
            "PartiesDb",
            fixture.ConnectionStringFor(database),
            async dbContext =>
            {
                dbContext.Parties.RemoveRange(dbContext.Parties);
                dbContext.Parties.AddRange(parties);
                await dbContext.SaveChangesAsync();
            });

    private sealed record PartyResponse(Guid Id, string DisplayName);
}
