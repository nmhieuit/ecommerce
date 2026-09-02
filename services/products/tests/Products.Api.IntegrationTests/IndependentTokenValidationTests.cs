using System.Net;
using System.Net.Http.Headers;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Products.Api.IntegrationTests;

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
    [Fact]
    public async Task ARequestWithNoToken_IsRejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ARequestWithATamperedToken_IsRejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken() + "tampered");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// spec US3 Acceptance Scenario 1/2, Test Scenario 3: an expired token is rejected with a clear,
    /// distinguishable response — not the framework's default empty-body 401, and not conflated with
    /// a tampered or malformed token (data-model.md — Token, trạng thái Expired).
    /// </summary>
    [Fact]
    public async Task ARequestWithAnExpiredToken_IsRejected_WithAClearExpiredMessage()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/products");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtBearer.CreateToken(expires: DateTime.UtcNow.AddMinutes(-5)));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("token_expired", body, StringComparison.Ordinal);
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
