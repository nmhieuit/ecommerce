using System.Net;
using System.Net.Http.Headers;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// 015-deny-by-default-authz, spec US1 Acceptance Scenario 2, Test Scenario 2: a request that
/// authenticated successfully but whose token lacks the <c>ApiScope</c> policy's required claim is
/// rejected 403, not processed as if it were 200. The policy is toggle-gated (research.md Decision 5)
/// — <c>appsettings.Development.json</c> turns it on, which is what
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> loads by default.
/// </summary>
public class AuthorizationPolicyTests
{
    private static readonly string BasketRoute = $"/baskets/{Guid.NewGuid():D}";

    [Fact]
    public async Task ARequestWithATokenMissingTheApiScopeClaim_IsForbidden()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, BasketRoute);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken(includeApiScope: false));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Regression guard: a token carrying the required scope is not newly rejected.</summary>
    [Fact]
    public async Task ARequestWithATokenCarryingTheApiScopeClaim_IsNotRejectedForAuthorization()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, BasketRoute);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken(includeApiScope: true));

        var response = await client.SendAsync(request);

        // Whether this particular basket id exists (404) is not this test's concern — only that
        // the request was not turned away for lacking authorization.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseTestJwtBearer());
}
