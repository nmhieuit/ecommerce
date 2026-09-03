using System.Net;
using System.Net.Http.Headers;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// 015-deny-by-default-authz, spec US1 Acceptance Scenario 2, Test Scenario 2: a request that
/// authenticated successfully but whose token lacks the <c>ApiScope</c> policy's required claim is
/// rejected 403, not processed as if it were 200.
/// </summary>
public class AuthorizationPolicyTests
{
    private static readonly string OrderRoute = $"/orders/{Guid.NewGuid():D}";

    [Fact]
    public async Task ARequestWithATokenMissingTheApiScopeClaim_IsForbidden()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, OrderRoute);
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

        using var request = new HttpRequestMessage(HttpMethod.Get, OrderRoute);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken(includeApiScope: true));

        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseTestJwtBearer());
}
