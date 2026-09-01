using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Identity.Api.Data;
using Identity.Api.HostedIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Duende.IdentityServer.EntityFramework.DbContexts;

namespace Identity.Api.IntegrationTests;

/// <summary>
/// spec Test Scenario 1, US1 Acceptance Scenario 1: logging in through the identity server issues a
/// token carrying verified identity and tenant claims. Exercises real login → token issuance over
/// HTTP against a real SQL Server (constitution Principle III) — the identity-server half of US1;
/// <c>Gateway.Api.IntegrationTests.JwtBearerAuthenticationTests</c> covers the gateway's consumption
/// of a token independently.
/// </summary>
/// <remarks>
/// Logs in via the Resource Owner Password grant on the test-only <c>integration-test-ropc</c>
/// client (<c>Config.cs</c> — deliberately not the SPA's Authorization Code + PKCE client, which
/// needs an interactive login UI this phase does not build). This still proves the thing US1
/// actually needs proven: the identity server authenticates real credentials against its own store
/// and issues a token whose <c>sub</c>/<c>tenant_id</c> claims come from that user's row, through the
/// same <see cref="TenantClaimsProfileService"/> any grant type uses.
/// </remarks>
public class LoginIssuesTokenTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TestTenantId = "contoso-login-test";
    private const string TestPassword = "Integration-Test-P@ssw0rd!";

    [Fact]
    public async Task Login_IssuesATokenContaining_VerifiedIdentityAndTenantClaims()
    {
        var username = $"login-test-{Guid.NewGuid():N}@example.test";
        await using var factory = await CreateFactoryAsync();
        await SeedUserAsync(factory, username, TestTenantId);
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = TestPassword,
                ["client_id"] = "integration-test-ropc",
                ["client_secret"] = "integration-test-secret",
                ["scope"] = "openid profile ecommerce-api",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(payload.AccessToken);
        var subject = token.Claims.SingleOrDefault(claim => claim.Type == "sub")?.Value;
        var tenantId = token.Claims.SingleOrDefault(claim => claim.Type == TenantClaimsProfileService.TenantClaimType)?.Value;

        Assert.False(string.IsNullOrWhiteSpace(subject));
        Assert.Equal(TestTenantId, tenantId);
    }

    [Fact]
    public async Task Login_Fails_WhenThePasswordIsWrong()
    {
        var username = $"login-test-{Guid.NewGuid():N}@example.test";
        await using var factory = await CreateFactoryAsync();
        await SeedUserAsync(factory, username, TestTenantId);
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = "not-the-right-password",
                ["client_id"] = "integration-test-ropc",
                ["client_secret"] = "integration-test-secret",
                ["scope"] = "openid profile ecommerce-api",
            }));

        // Duende answers a failed grant with 400 (invalid_grant), not 401 — this is a rejected
        // token *request*, not a rejected already-issued token (that distinction is
        // Gateway.Api.IntegrationTests.JwtBearerAuthenticationTests' concern).
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDb"] = sqlServer.ConnectionString,
                })));

        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();

        // Config.cs's clients/resources/scopes — normally seeded by `dotnet run -- --seed`
        // (Data/SeedData.cs), run here directly against the same migrated database.
        await SeedData.EnsureSeedDataAsync(factory.Services);

        return factory;
    }

    /// <summary>
    /// A test user, created directly through <see cref="UserManager{TUser}"/> — deliberately not
    /// through <see cref="SeedData"/>, which holds no credentials by design (Data/SeedData.cs remarks).
    /// </summary>
    private static async Task SeedUserAsync(WebApplicationFactory<Program> factory, string username, string tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { UserName = username, Email = username, TenantId = tenantId };
        var result = await userManager.CreateAsync(user, TestPassword);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);
}
