using System.Net;
using System.Net.Http.Headers;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// spec US2 Acceptance Scenario 2/3, Test Scenario 2: this service authenticates a request's token
/// independently — it does not trust that the gateway already did, and a request reaching it
/// directly (bypassing the gateway entirely) is still validated. spec FR-011: no token is exactly
/// as unauthenticated as an invalid one.
/// </summary>
/// <remarks>
/// Uses <c>IntegrationTestSupport.TestJwtBearer</c> (shared across every service's integration
/// tests) rather than a locally-issued token: <c>FallbackPolicy</c> rejects an unauthenticated
/// request before the endpoint ever resolves a tenant or touches persistence, so this suite needs
/// no running identity server or database.
/// </remarks>
public class IndependentTokenValidationTests
{
    private static readonly string OrderRoute = $"/orders/{Guid.NewGuid():D}";

    [Fact]
    public async Task ARequestWithNoToken_IsRejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(OrderRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ARequestWithATamperedToken_IsRejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, OrderRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken() + "tampered");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>research.md Decision 6: health probes are the only explicit AllowAnonymous exception.</summary>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task AHealthProbe_RemainsAnonymous_EvenWithNoToken(string route)
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(route);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseTestJwtBearer());
}
