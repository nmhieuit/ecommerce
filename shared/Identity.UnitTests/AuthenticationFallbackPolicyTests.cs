using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Identity.UnitTests;

/// <summary>
/// 015-deny-by-default-authz, research.md Decision 1: the fallback every service's
/// <c>AddIdentityValidation()</c> installs must keep requiring an authenticated user (014, unchanged)
/// AND now also carry the toggle-gated scope requirement — the FallbackPolicy is deliberately at
/// least as strict as the named <c>ApiScope</c> policy every route declares explicitly.
/// </summary>
public class AuthenticationFallbackPolicyTests
{
    [Fact]
    public void Build_StillRequiresAnAuthenticatedUser()
    {
        var policy = AuthenticationFallbackPolicy.Build();

        Assert.Contains(policy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void Build_AlsoRequiresTheApiScopeRequirement()
    {
        var policy = AuthenticationFallbackPolicy.Build();

        Assert.Contains(policy.Requirements, requirement => requirement is RequireApiScopeRequirement);
    }
}
