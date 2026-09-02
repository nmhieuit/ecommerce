using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

/// <summary>
/// research.md Decision 4/6: <c>AddIdentityValidation()</c> is the one call every service (gateway,
/// BFF, 4 domain services) makes to get JwtBearer authentication plus a deny-by-default
/// <c>FallbackPolicy</c> — this proves the DI registration it produces, without needing a full HTTP
/// round-trip (mirrors shared/Tenancy.UnitTests' approach: no <c>WebApplicationFactory</c> needed).
/// </summary>
public class IdentityValidationExtensionsTests
{
    [Fact]
    public async Task AddIdentityValidation_RegistersTheJwtBearerScheme()
    {
        var services = new ServiceCollection();
        services.AddIdentityValidation(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        var scheme = await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(scheme);
        Assert.Equal(typeof(JwtBearerHandler), scheme.HandlerType);
    }

    [Fact]
    public async Task AddIdentityValidation_SetsTheFallbackPolicy_ToRequireAnAuthenticatedUser()
    {
        var services = new ServiceCollection();
        services.AddIdentityValidation(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();

        // The requirement itself, not just "a policy exists": a fallback policy with zero
        // requirements would let every unauthenticated request through, which is the exact gap
        // FallbackPolicy = RequireAuthenticatedUser() exists to close (research.md Decision 6).
        Assert.NotNull(fallbackPolicy);
        Assert.Contains(fallbackPolicy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void AddIdentityValidation_BindsAuthorityAndAudience_FromTheSuppliedConfiguration()
    {
        var services = new ServiceCollection();
        services.AddIdentityValidation(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var jwtOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("http://identity-api:8080", jwtOptions.Authority);
        Assert.Equal("ecommerce-api", jwtOptions.Audience);
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Authority"] = "http://identity-api:8080",
                ["Identity:Audience"] = "ecommerce-api",
            })
            .Build();
}
