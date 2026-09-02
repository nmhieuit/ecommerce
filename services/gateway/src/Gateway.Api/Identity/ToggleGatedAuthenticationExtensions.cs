using Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Gateway.Api.Identity;

/// <summary>
/// Registers authentication at the gateway so that the scheme actually used — <c>StubIdentity</c>
/// or real <c>JwtBearer</c> — is decided per request from a live-reloaded toggle, not fixed at
/// startup (research.md Decision 2/7; tasks.md T025). This is the one exception to every other
/// service's plain <c>AddIdentityValidation()</c> call (shared/Identity): the gateway alone still
/// carries the Phase 1 stub it must be able to fall back to.
/// </summary>
public static class ToggleGatedAuthenticationExtensions
{
    /// <summary>The scheme name registered as the gateway's default — resolves to one of the two below per request.</summary>
    public const string SchemeName = "ToggleGatedIdentity";

    public static AuthenticationBuilder AddToggleGatedIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<FeatureToggleOptions>(configuration.GetSection(FeatureToggleOptions.ConfigSectionName));

        var identityOptions = configuration.GetSection(IdentityServerOptions.ConfigSectionName).Get<IdentityServerOptions>()
            ?? new IdentityServerOptions();

        var authenticationBuilder = services
            .AddAuthentication(SchemeName)
            // Evaluated per request (HttpContext-scoped, via RequestServices), not once at
            // startup — the mechanism that makes "flip a config value, no redeploy" (Principle X)
            // actually take effect without also requiring a pod restart.
            .AddPolicyScheme(SchemeName, "Toggle-gated identity", policySchemeOptions =>
                policySchemeOptions.ForwardDefaultSelector = context =>
                {
                    var toggles = context.RequestServices.GetRequiredService<IOptionsMonitor<FeatureToggleOptions>>();
                    return toggles.CurrentValue.IdentityServerAuthCutover
                        ? JwtBearerDefaults.AuthenticationScheme
                        : StubIdentityAuthenticationHandler.SchemeName;
                })
            .AddScheme<StubIdentityAuthenticationSchemeOptions, StubIdentityAuthenticationHandler>(
                StubIdentityAuthenticationHandler.SchemeName,
                options => configuration.GetSection("StubIdentity").Bind(options))
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = identityOptions.Authority;
                jwtOptions.Audience = identityOptions.Audience;

                // See shared/Identity/IdentityValidationExtensions.cs for why this follows the
                // Authority's own scheme rather than the framework default of always-true.
                jwtOptions.RequireHttpsMetadata =
                    identityOptions.Authority?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ?? true;

                // Same clear, distinguishable 401 every other service gets from
                // AddIdentityValidation() (spec FR-006, US3) — applied here explicitly since the
                // gateway can't call that helper directly (see class remarks).
                ClearUnauthorizedResponseEvents.Configure(jwtOptions);
            });

        // Same ApiScope registration every other service gets from AddIdentityValidation()
        // (015-deny-by-default-authz, research.md Decision 1/2/5/6) — the gateway just can't call
        // that helper directly, since it needs the three-scheme registration above instead of the
        // plain single AddJwtBearer call.
        services.Configure<AuthorizationToggleOptions>(
            configuration.GetSection(AuthorizationToggleOptions.ConfigSectionName));
        services.AddSingleton<IAuthorizationHandler, RequireApiScopeAuthorizationHandler>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ClearForbiddenResponseEvents>();

        // Same deny-by-default fallback every other service gets from AddIdentityValidation()
        // (research.md Decision 6) — the gateway just can't call that helper directly, since it
        // needs the three-scheme registration above instead of the plain single AddJwtBearer call.
        services.AddAuthorization(authorizationOptions =>
        {
            authorizationOptions.FallbackPolicy = AuthenticationFallbackPolicy.Build();
            authorizationOptions.AddPolicy(AuthorizationPolicies.ApiScope, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new RequireApiScopeRequirement()));
        });

        return authenticationBuilder;
    }
}
