using Microsoft.Extensions.Options;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Where one downstream service lives, as configured. One instance per service the BFF calls —
/// products, baskets, orders, parties (data-model.md, Downstream Service Client).
/// </summary>
public sealed class DownstreamServiceClientOptions
{
    /// <summary>The configuration section holding every downstream service's settings.</summary>
    public const string SectionName = "Services";

    /// <summary>
    /// The service's logical name (<c>ProductsApi</c>, <c>BasketsApi</c>, …). Set from the
    /// configuration key rather than the configuration body, so it cannot drift from the section
    /// it was bound from. This is the only name that may appear in an error surfaced to a client —
    /// never <see cref="BaseUrl"/>, which would leak internal topology (spec FR-001).
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>The service's base address, supplied per environment, never baked into the image.</summary>
    public Uri? BaseUrl { get; set; }
}

/// <summary>
/// Registers a downstream service's options with validation that fails at startup.
/// </summary>
public static class DownstreamServiceClientRegistration
{
    /// <summary>
    /// Binds <c>Services:{serviceName}</c> and refuses to start without a usable
    /// <see cref="DownstreamServiceClientOptions.BaseUrl"/>.
    /// </summary>
    /// <remarks>
    /// data-model.md's validation rule for this entity: "A downstream client with no configured
    /// BaseUrl is a startup configuration error, not a runtime null-reference — fail fast rather
    /// than fail per-request." The operational difference is the point: an instance that fails at
    /// startup never takes traffic, while one that fails per request passes its readiness probe
    /// and serves errors to real users until somebody reads the logs.
    /// </remarks>
    public static IServiceCollection AddDownstreamServiceOptions(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(configuration);

        var configurationKey = $"{DownstreamServiceClientOptions.SectionName}:{serviceName}";

        services.AddOptions<DownstreamServiceClientOptions>(serviceName)
            .Bind(configuration.GetSection(configurationKey))
            .Configure(options => options.ServiceName = serviceName)
            .Validate(
                options => options.BaseUrl is not null && options.BaseUrl.IsAbsoluteUri,
                // Names the exact key an operator has to fix, rather than only reporting that
                // something about the configuration is wrong.
                $"'{configurationKey}:BaseUrl' must be set to an absolute URI "
                + $"(for example http://{serviceName.ToLowerInvariant()}:8080).")
            .ValidateOnStart();

        return services;
    }

    /// <summary>Resolves a named <see cref="DownstreamServiceClientOptions"/>, triggering validation.</summary>
    public static DownstreamServiceClientOptions GetDownstreamOptions(
        this IServiceProvider services,
        string serviceName) =>
        services.GetRequiredService<IOptionsMonitor<DownstreamServiceClientOptions>>().Get(serviceName);
}
