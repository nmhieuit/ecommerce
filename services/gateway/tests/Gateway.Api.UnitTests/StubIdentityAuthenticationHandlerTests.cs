using System.Security.Claims;
using System.Text.Encodings.Web;
using Gateway.Api.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gateway.Api.UnitTests;

/// <summary>
/// The stub identity on its own, away from the full pipeline the gateway's integration tests drive
/// through it. research.md Decision 1 rests on this being a real authentication scheme rather than a
/// header-stamping shortcut, so the claim it issues is worth asserting directly: everything below
/// the gateway reads that claim and nothing else.
/// </summary>
public class StubIdentityAuthenticationHandlerTests
{
    private const string ConfiguredTenant = "contoso";
    private const string ConfiguredSubject = "phase1-stub-user";

    [Fact]
    public async Task AuthenticateAsync_Succeeds_ForAnyRequest()
    {
        var result = await AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task AuthenticateAsync_IssuesTheConfiguredTenantClaim()
    {
        var result = await AuthenticateAsync();

        var tenant = result.Principal?.FindFirst(StubIdentityAuthenticationHandler.TenantClaimType);
        Assert.NotNull(tenant);
        Assert.Equal(ConfiguredTenant, tenant.Value);
    }

    /// <summary>
    /// data-model.md — Stub Identity: the principal carries a subject as well as a tenant, so it is
    /// shaped like the real one Phase 3 replaces it with rather than carrying a tenant alone.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_IssuesTheConfiguredSubjectClaim()
    {
        var result = await AuthenticateAsync();

        Assert.Equal(ConfiguredSubject, result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    /// <summary>
    /// Phase 1 has no credentials to present (spec Assumptions), so nothing about the request can
    /// change the answer — including a caller trying to influence it through headers.
    /// </summary>
    [Theory]
    [InlineData("/bff/products")]
    [InlineData("/anything-at-all")]
    public async Task AuthenticateAsync_IgnoresTheRequest_HavingNoCredentialsToRead(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.Headers.Authorization = "Bearer not-a-real-token";

        var result = await AuthenticateAsync(httpContext: httpContext);

        Assert.True(result.Succeeded);
        Assert.Equal(
            ConfiguredTenant,
            result.Principal?.FindFirst(StubIdentityAuthenticationHandler.TenantClaimType)?.Value);
    }

    /// <summary>
    /// An unconfigured gateway must not authenticate anyone. A principal with no tenant would be an
    /// unresolved request wearing a disguise: every hop below would treat it as unresolved anyway,
    /// so failing here keeps "resolved once, at the edge" true rather than nominally satisfied.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AuthenticateAsync_Fails_WhenNoTenantIsConfigured(string unconfigured)
    {
        var result = await AuthenticateAsync(tenantId: unconfigured);

        Assert.False(result.Succeeded);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task AuthenticateAsync_IssuesAnIdentityNamingTheStubScheme()
    {
        var result = await AuthenticateAsync();

        Assert.Equal(
            StubIdentityAuthenticationHandler.SchemeName,
            result.Principal?.Identity?.AuthenticationType);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        string tenantId = ConfiguredTenant,
        HttpContext? httpContext = null)
    {
        var options = new StubIdentityAuthenticationSchemeOptions
        {
            TenantId = tenantId,
            SubjectId = ConfiguredSubject,
        };

        var handler = new StubIdentityAuthenticationHandler(
            new StaticOptionsMonitor(options),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        await handler.InitializeAsync(
            new AuthenticationScheme(
                StubIdentityAuthenticationHandler.SchemeName,
                displayName: null,
                handlerType: typeof(StubIdentityAuthenticationHandler)),
            httpContext ?? new DefaultHttpContext());

        return await handler.AuthenticateAsync();
    }

    /// <summary>
    /// The handler resolves its options through <see cref="IOptionsMonitor{TOptions}"/>; nothing
    /// here reloads them, so a fixed instance is the whole contract.
    /// </summary>
    private sealed class StaticOptionsMonitor(StubIdentityAuthenticationSchemeOptions options)
        : IOptionsMonitor<StubIdentityAuthenticationSchemeOptions>
    {
        public StubIdentityAuthenticationSchemeOptions CurrentValue => options;

        public StubIdentityAuthenticationSchemeOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<StubIdentityAuthenticationSchemeOptions, string?> listener) => null;
    }
}
