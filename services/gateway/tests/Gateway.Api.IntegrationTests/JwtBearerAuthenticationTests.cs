extern alias BffApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Gateway.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// spec US1 Acceptance Scenario 2 (a valid token is treated as a genuine, resolved identity — not
/// the Phase 1 fake user) and constitution Principle X (toggle rollback without redeploy). Tests the
/// gateway's *consumption* of a token — tasks.md T017, against a real running <c>Identity.Api</c>,
/// covers *issuance*; this suite proves the gateway side independently by supplying its own
/// symmetric-key-signed token and bypassing the real OIDC discovery/JWKS fetch (research.md
/// Decision 5) that a real <c>Authority</c> would otherwise require over the network.
/// </summary>
public class JwtBearerAuthenticationTests
{
    private const string TestSigningKey = "integration-test-jwt-signing-key-at-least-32-bytes!!";
    private const string TestTenantId = "contoso-jwt-test";
    private const string TestSubjectId = "jwt-bearer-test-user";

    /// <summary>
    /// spec US1 Acceptance Scenario 2, FR-008: a valid token is authenticated, and
    /// <see cref="TenantHeaderPropagationMiddleware"/>/<see cref="SubjectHeaderPropagationMiddleware"/>
    /// produce the same headers from its claims as they always have from
    /// <c>StubIdentityAuthenticationHandler</c>'s — proving research.md Decision 3's claim that
    /// nothing downstream of the authentication scheme needed to change.
    /// </summary>
    [Fact]
    public async Task ARequestWithAValidToken_PropagatesTenantAndSubject_WhenToggleIsOn()
    {
        var recorder = new HeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = CreateGatewayWithTestJwtBearer(bff, toggleOn: true);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var observed = Assert.Single(recorder.Observed);
        Assert.Equal(TestTenantId, observed.TenantId);
        Assert.Equal(TestSubjectId, observed.SubjectId);
    }

    /// <summary>
    /// spec US2 Acceptance Scenario 1 (the gateway validates before forwarding) and spec FR-011: no
    /// token is exactly as unauthenticated as an invalid one.
    /// </summary>
    [Fact]
    public async Task ARequestWithNoToken_IsRejected_WhenToggleIsOn()
    {
        await using var gateway = CreateGatewayWithTestJwtBearer(GatewayTestHost.CreateBff(), toggleOn: true);
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/bff/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// spec US2 Test Scenario 2 / Acceptance Scenario 3: a tampered token is rejected — the gateway
    /// does not trust a signature it cannot verify.
    /// </summary>
    [Fact]
    public async Task ARequestWithATamperedToken_IsRejected_WhenToggleIsOn()
    {
        await using var gateway = CreateGatewayWithTestJwtBearer(GatewayTestHost.CreateBff(), toggleOn: true);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken() + "tampered");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// spec US3 Acceptance Scenario 1/2, Test Scenario 3: an expired token is rejected with a clear,
    /// distinguishable response — not the framework's default empty-body 401, and not conflated with
    /// a tampered or malformed token (data-model.md — Token, trạng thái Expired).
    /// </summary>
    [Fact]
    public async Task ARequestWithAnExpiredToken_IsRejected_WithAClearExpiredMessage_WhenToggleIsOn()
    {
        await using var gateway = CreateGatewayWithTestJwtBearer(GatewayTestHost.CreateBff(), toggleOn: true);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(expired: true));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("token_expired", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// constitution Principle X; research.md Decision 7: gagging the toggle back off restores the
    /// gateway's own Phase 1 behavior — it stops requiring a token itself and forwards the request
    /// regardless, without a code change or redeploy. This is the rollback path the toggle exists
    /// for. Since Phase 4 (T027) gave the BFF its own independent auth, that rollback is scoped to
    /// the gateway alone (constitution Principle VI: "gateway is not a trust boundary services rely
    /// on") — the BFF still enforces its own policy regardless of this toggle, so what the toggle
    /// actually controls is whether the *gateway* rejects the request, not the end-to-end outcome.
    /// Proven the same way <see cref="ARequestWithAValidToken_PropagatesTenantAndSubject_WhenToggleIsOn"/>
    /// proves forwarding: the request reaches the BFF's pipeline at all, recorded before the BFF's
    /// own auth has a chance to act on it.
    /// </summary>
    [Fact]
    public async Task ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff()
    {
        var recorder = new HeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = CreateGatewayWithTestJwtBearer(bff, toggleOn: false);
        var client = gateway.CreateClient();

        await client.GetAsync("/bff/products");

        Assert.Single(recorder.Observed);
    }

    private static string CreateToken(bool expired = false)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, TestSubjectId),
                new Claim(TenantClaimType, TestTenantId),
            ],
            expires: expired ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Must equal <c>Identity.Api.HostedIdentity.TenantClaimsProfileService.TenantClaimType</c> — a wire contract, not shared code (research.md Decision 3).</summary>
    private const string TenantClaimType = "tenant_id";

    private static WebApplicationFactory<Program> CreateGatewayWithTestJwtBearer(
        WebApplicationFactory<BffApi::Program> bff, bool toggleOn) =>
        GatewayTestHost.CreateGateway(bff).WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FeatureToggles:IdentityServerAuthCutover"] = toggleOn ? "true" : "false",
                }));

            builder.ConfigureServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // A non-null Configuration stops JwtBearerHandler from fetching the OIDC
                    // discovery document/JWKS over HTTP (research.md Decision 5) — this suite tests
                    // the gateway's consumption of a token, not the identity server's issuance
                    // (that's tasks.md T017, against a real running Identity.Api).
                    options.Configuration = new OpenIdConnectConfiguration();
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
                    };
                }));
        });

    private static WebApplicationFactory<BffApi::Program> CreateRecordingBff(HeaderRecorder recorder) =>
        GatewayTestHost.CreateBff().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new HeaderRecordingStartupFilter(recorder))));

    private sealed record ObservedHeaders(string? TenantId, string? SubjectId);

    private sealed class HeaderRecorder
    {
        private readonly List<ObservedHeaders> _observed = [];

        public IReadOnlyList<ObservedHeaders> Observed
        {
            get
            {
                lock (_observed)
                {
                    return _observed.ToArray();
                }
            }
        }

        public void Record(ObservedHeaders headers)
        {
            lock (_observed)
            {
                _observed.Add(headers);
            }
        }
    }

    private sealed class HeaderRecordingStartupFilter(HeaderRecorder recorder) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    recorder.Record(new ObservedHeaders(
                        context.Request.Headers.TryGetValue(TenantHeaderPropagationMiddleware.HeaderName, out var tenant)
                            ? tenant.ToString()
                            : null,
                        context.Request.Headers.TryGetValue(SubjectHeaderPropagationMiddleware.HeaderName, out var subject)
                            ? subject.ToString()
                            : null));

                    await nextMiddleware();
                });

                next(app);
            };
    }
}
