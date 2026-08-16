using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Tenancy;

/// <summary>
/// The one way a service opts into tenant context — call <see cref="AddTenancy"/> before
/// <c>Build()</c> and <see cref="UseTenancy"/> on the built <see cref="WebApplication"/>, in every
/// service's <c>Program.cs</c>. Mirrors <c>ServiceDefaultsExtensions</c>' shape for the same
/// reason: a cross-cutting concern used identically everywhere is shared wiring, never something
/// each service hand-rolls and lets drift (research.md Decision 3).
/// </summary>
/// <remarks>
/// The gateway deliberately does not call these. It <em>produces</em> the header from the
/// authenticated identity rather than consuming one, so it has no use for the reader half.
/// </remarks>
public static class TenancyExtensions
{
    /// <summary>
    /// Registers the request-scoped <see cref="TenantContext"/> that
    /// <see cref="TenantContextMiddleware"/> populates and everything downstream reads.
    /// </summary>
    public static IServiceCollection AddTenancy(this IServiceCollection services)
    {
        // Scoped, not singleton: one resolved tenant per request, never shared across requests.
        services.AddScoped<TenantContext>();

        // No DI registration for TenantContextMiddleware — UseMiddleware<T>() constructs
        // conventional middleware itself, injecting RequestDelegate specially, and registering it
        // in the container as well breaks DI validation (the same note ServiceDefaults carries).
        return services;
    }

    /// <summary>
    /// Wires <see cref="TenantContextMiddleware"/> into the pipeline. Call immediately after
    /// <c>UseServiceDefaults()</c>, before any endpoint mapping, so the tenant is available for the
    /// whole lifetime of every request — including to the logging scope the correlation ID has
    /// already opened by that point.
    /// </summary>
    public static WebApplication UseTenancy(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<TenantContextMiddleware>();
        return app;
    }
}
