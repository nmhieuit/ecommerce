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
    /// Registers the request-scoped <see cref="TenantContext"/> and <see cref="CallerContext"/>
    /// that the middlewares populate and everything downstream reads.
    /// </summary>
    /// <remarks>
    /// The caller context is registered here rather than behind its own <c>AddCallerIdentity()</c>
    /// call on purpose: a service that resolved a tenant but not a caller, or the reverse, is a
    /// half-wired service, and two opt-ins are two chances to wire only one. One call, both
    /// contexts, no service left partly configured (004-minimal-shopping-spa research.md Decision 6).
    /// </remarks>
    public static IServiceCollection AddTenancy(this IServiceCollection services)
    {
        // Scoped, not singleton: one resolved tenant and one resolved caller per request, never
        // shared across requests.
        services.AddScoped<TenantContext>();
        services.AddScoped<CallerContext>();

        // No DI registration for the middlewares — UseMiddleware<T>() constructs conventional
        // middleware itself, injecting RequestDelegate specially, and registering it in the
        // container as well breaks DI validation (the same note ServiceDefaults carries).
        return services;
    }

    /// <summary>
    /// Wires <see cref="TenantContextMiddleware"/> and <see cref="CallerContextMiddleware"/> into
    /// the pipeline. Call immediately after <c>UseServiceDefaults()</c>, before any endpoint
    /// mapping, so both are available for the whole lifetime of every request — including to the
    /// logging scope the correlation ID has already opened by that point.
    /// </summary>
    /// <remarks>
    /// Tenant first, then caller, so the nested logging scopes read outward-in as
    /// correlation → tenant → caller: which request, which store, whose rows.
    /// </remarks>
    public static WebApplication UseTenancy(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<TenantContextMiddleware>();
        app.UseMiddleware<CallerContextMiddleware>();
        return app;
    }
}
