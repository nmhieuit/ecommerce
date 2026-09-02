using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity;

/// <summary>
/// The one way a service opts into independent token validation — call
/// <see cref="AddIdentityValidation"/> before <c>Build()</c> and <see cref="UseIdentityValidation"/>
/// on the built <see cref="WebApplication"/>, in every service's <c>Program.cs</c>. Mirrors
/// <c>TenancyExtensions</c>' shape for the same reason: a cross-cutting concern used identically
/// everywhere — the gateway, the BFF, and all four domain services — is shared wiring, not
/// something each service hand-rolls and lets drift (research.md Decision 4).
/// </summary>
public static class IdentityValidationExtensions
{
    /// <summary>
    /// Registers JwtBearer authentication against the identity server named by the "Identity"
    /// configuration section (<see cref="IdentityServerOptions"/>), and sets
    /// <see cref="AuthenticationFallbackPolicy"/> as the authorization fallback so every endpoint
    /// requires an authenticated user unless it explicitly opts out with <c>[AllowAnonymous]</c>
    /// (research.md Decision 6). A rejected token — expired or otherwise invalid — gets the clear,
    /// distinguishable response <see cref="ClearUnauthorizedResponseEvents"/> writes, not the
    /// framework's default empty-body 401 (spec FR-006, US3).
    /// </summary>
    public static IServiceCollection AddIdentityValidation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var identityOptions = configuration.GetSection(IdentityServerOptions.ConfigSectionName).Get<IdentityServerOptions>()
            ?? new IdentityServerOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = identityOptions.Authority;
                jwtOptions.Audience = identityOptions.Audience;

                // JwtBearer defaults RequireHttpsMetadata to true (constitution Principle VI:
                // secure by default). Every other inter-service call in this platform — gateway to
                // BFF, BFF to each domain service — is plain HTTP on the internal cluster network,
                // with TLS terminated at the edge; an https:// Authority (e.g. behind an ingress in
                // production) keeps the secure default, an http:// one (local/compose, in-cluster)
                // does not fail startup over a property the deployment topology already governs.
                jwtOptions.RequireHttpsMetadata =
                    identityOptions.Authority?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ?? true;

                ClearUnauthorizedResponseEvents.Configure(jwtOptions);
            });

        services.AddAuthorization(authorizationOptions =>
            authorizationOptions.FallbackPolicy = AuthenticationFallbackPolicy.Build());

        return services;
    }

    /// <summary>
    /// Wires authentication and authorization into the pipeline. Call immediately after
    /// <c>UseServiceDefaults()</c> (and, where present, <c>UseTenancy()</c>), before any endpoint
    /// mapping, so a request is challenged before it reaches a handler.
    /// </summary>
    public static WebApplication UseIdentityValidation(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
