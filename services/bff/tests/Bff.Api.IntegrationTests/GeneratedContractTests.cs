using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// Constitution Principle II and ADR-0004: the generated OpenAPI document is the authoritative
/// contract, and the frontend's API client is generated from it rather than hand-written.
/// </summary>
/// <remarks>
/// That makes an incomplete document a real defect, not a documentation nit. Minimal APIs infer
/// only the success response, so without explicit metadata the document would advertise 200 as the
/// sole outcome of every route — and SCRUM-14's generated client would have no typed notion of
/// "basket not found" or "products service unavailable", despite the BFF returning both.
/// These assertions pin the document against the hand-authored reference in
/// <c>specs/002-gateway-bff-routing/contracts/bff-openapi.yaml</c>.
/// </remarks>
public class GeneratedContractTests
{
    [Fact]
    public async Task TheDocument_DescribesEveryClientFacingRoute()
    {
        var paths = (await GetDocumentAsync()).GetProperty("paths");

        foreach (var route in new[]
                 { "/bff/products", "/bff/baskets/{basketId}", "/bff/orders/{orderId}", "/bff/parties/{partyId}" })
        {
            Assert.True(paths.TryGetProperty(route, out _), $"The document omits '{route}'.");
        }
    }

    /// <summary>
    /// Every route can fail with a downstream error (US3), so every route must say so.
    /// </summary>
    [Theory]
    [InlineData("/bff/products")]
    [InlineData("/bff/baskets/{basketId}")]
    [InlineData("/bff/orders/{orderId}")]
    [InlineData("/bff/parties/{partyId}")]
    public async Task EveryRoute_DeclaresItsDownstreamFailureResponses(string route)
    {
        var responses = await GetResponseCodesAsync(route);

        Assert.Contains("200", responses);
        Assert.Contains("502", responses);
        Assert.Contains("504", responses);
    }

    /// <summary>
    /// The by-id routes return 404 for an unknown id — a distinct, expected outcome the client must
    /// be able to branch on rather than treat as a failure.
    /// </summary>
    [Theory]
    [InlineData("/bff/baskets/{basketId}")]
    [InlineData("/bff/orders/{orderId}")]
    [InlineData("/bff/parties/{partyId}")]
    public async Task TheByIdRoutes_DeclareNotFound(string route)
    {
        Assert.Contains("404", await GetResponseCodesAsync(route));
    }

    /// <summary>
    /// The product listing is the one shape the contract fully specifies (spec FR-004), so its
    /// schema is asserted field by field against contracts/bff-openapi.yaml.
    /// </summary>
    [Fact]
    public async Task TheProductListingSchema_MatchesTheHandAuthoredContract()
    {
        var schemas = (await GetDocumentAsync()).GetProperty("components").GetProperty("schemas");

        Assert.True(schemas.TryGetProperty("ProductListResponse", out var listing));
        Assert.True(listing.GetProperty("properties").TryGetProperty("items", out _));

        Assert.True(schemas.TryGetProperty("ProductSummary", out var summary));
        var properties = summary.GetProperty("properties");
        foreach (var field in new[] { "id", "name", "price" })
        {
            Assert.True(properties.TryGetProperty(field, out _), $"ProductSummary omits '{field}'.");
        }
    }

    private static async Task<IReadOnlyList<string>> GetResponseCodesAsync(string route)
    {
        var operation = (await GetDocumentAsync())
            .GetProperty("paths")
            .GetProperty(route)
            .GetProperty("get");

        return [.. operation.GetProperty("responses").EnumerateObject().Select(response => response.Name)];
    }

    /// <summary>
    /// The document is published in Development only (T013), which is also where the codegen
    /// pipeline reads it from.
    /// </summary>
    private static async Task<JsonElement> GetDocumentAsync()
    {
        await using var bff = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development));

        return await bff.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
    }
}
