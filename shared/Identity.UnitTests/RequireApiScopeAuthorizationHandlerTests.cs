using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Identity.UnitTests;

/// <summary>
/// 015-deny-by-default-authz, research.md Decision 1/5: the handler behind the <c>ApiScope</c>
/// policy — toggle-gated, so it must behave exactly like today's authenticated-only check while off
/// (constitution Principle X rollback) and only start requiring the <c>scope</c> claim once on.
/// </summary>
public class RequireApiScopeAuthorizationHandlerTests
{
    [Theory]
    [InlineData(true, true, true)] // toggle on, scope present -> succeeds
    [InlineData(true, false, false)] // toggle on, scope missing -> the one case that must NOT succeed
    [InlineData(false, true, true)] // toggle off, scope present -> succeeds
    [InlineData(false, false, true)] // toggle off, scope missing -> succeeds regardless (today's behaviour, unchanged)
    public async Task HandleAsync_SucceedsOnlyWhenToggleIsOff_OrScopeClaimIsPresent(
        bool toggleOn, bool includeScopeClaim, bool expectedSucceeded)
    {
        var handler = new RequireApiScopeAuthorizationHandler(CreateToggles(toggleOn));
        var context = CreateContext(includeScopeClaim ? ["openid", "ecommerce-api"] : ["openid"]);

        await handler.HandleAsync(context);

        Assert.Equal(expectedSucceeded, context.HasSucceeded);
    }

    /// <summary>
    /// Duende/JwtBearer can surface a token's scopes either as one space-delimited claim or as
    /// several individual claims of the same type, depending on how claims mapping is configured —
    /// the handler must not assume one shape (research.md Decision 1).
    /// </summary>
    [Fact]
    public async Task HandleAsync_TreatsMultipleIndividualScopeClaims_SameAsOneSpaceDelimitedClaim()
    {
        var handler = new RequireApiScopeAuthorizationHandler(CreateToggles(toggleOn: true));
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim("scope", "openid"));
        identity.AddClaim(new Claim("scope", "ecommerce-api"));
        var context = new AuthorizationHandlerContext(
            [new RequireApiScopeRequirement()], new ClaimsPrincipal(identity), resource: null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim("scope", string.Join(' ', scopes)));

        return new AuthorizationHandlerContext(
            [new RequireApiScopeRequirement()], new ClaimsPrincipal(identity), resource: null);
    }

    private static StaticOptionsMonitor<AuthorizationToggleOptions> CreateToggles(bool toggleOn) =>
        new(new AuthorizationToggleOptions { AuthorizationRequireApiScope = toggleOn });

    /// <summary>
    /// A minimal <see cref="IOptionsMonitor{TOptions}"/> stand-in — the repo carries no mocking
    /// framework (Moq/NSubstitute), same reasoning as 014's <c>TenantClaimsProfileServiceTests</c>.
    /// </summary>
    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
