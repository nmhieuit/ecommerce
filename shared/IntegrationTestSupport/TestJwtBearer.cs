using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTestSupport;

/// <summary>
/// A symmetric-key-signed test token and the <see cref="IWebHostBuilder"/> configuration that lets
/// a <c>WebApplicationFactory</c>-hosted service validate it, bypassing the real OIDC discovery/
/// JWKS fetch a live <c>Authority</c> would need over the network (research.md Decision 5).
/// </summary>
/// <remarks>
/// Every domain service, the BFF, and the gateway now run behind <c>AddIdentityValidation()</c>'s
/// deny-by-default <c>FallbackPolicy</c> (014-identity-server-auth, research.md Decision 6) — any
/// integration test that calls a business endpoint needs a token accepted by that check. This is
/// the one place that bypass is implemented, mirroring
/// <c>Gateway.Api.IntegrationTests.JwtBearerAuthenticationTests</c>' approach but shared so it is
/// not duplicated across every service's test project.
/// </remarks>
public static class TestJwtBearer
{
    private const string SigningKey = "integration-test-jwt-signing-key-at-least-32-bytes!!";

    /// <summary>
    /// Issues a token accepted by <see cref="UseTestJwtBearer"/>-configured hosts.
    /// </summary>
    /// <param name="includeApiScope">
    /// 015-deny-by-default-authz: whether the token carries a <c>scope</c> claim of
    /// <c>Identity.AuthorizationPolicies.RequiredApiScopeValue</c> ("ecommerce-api") — the claim the
    /// <c>ApiScope</c> policy requires once its toggle is on. Defaults to <see langword="true"/> so
    /// every existing call site (via <see cref="UseTestBearerToken"/>) keeps producing a token that
    /// passes the policy unchanged; pass <see langword="false"/> only to build the one negative case
    /// the policy exists to reject (spec Test Scenario 2).
    /// </param>
    public static string CreateToken(string subject = "test-user", DateTime? expires = null, bool includeApiScope = true)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        List<Claim> claims = [new Claim(JwtRegisteredClaimNames.Sub, subject)];
        if (includeApiScope)
        {
            claims.Add(new Claim("scope", "ecommerce-api"));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Configures the host's <c>JwtBearer</c> scheme to validate <see cref="CreateToken"/>'s tokens
    /// via the shared symmetric key, without contacting a real identity server.
    /// </summary>
    public static IWebHostBuilder UseTestJwtBearer(this IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // A non-null Configuration stops JwtBearerHandler from fetching the OIDC discovery
                // document/JWKS over HTTP.
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                };
            }));
    }

    /// <summary>Attaches a fresh valid token to every request this client sends.</summary>
    public static HttpClient UseTestBearerToken(this HttpClient client, string subject = "test-user", bool includeApiScope = true)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(subject, includeApiScope: includeApiScope));
        return client;
    }
}
