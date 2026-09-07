using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace IntegrationTestSupport;

/// <summary>
/// specs/018-cluster-secret-store FR-007: every service now fails fast at startup unless its
/// declared <c>RequiredSecret</c>s resolve to a credentialed value
/// (<c>shared/ServiceDefaults/RequiredSecretsValidation.cs</c>). A suite that exercises
/// authentication/authorization/routing behaviour — rejected before any endpoint ever touches
/// persistence — does not need a reachable database, but its <c>WebApplicationFactory</c>-hosted
/// service still needs to start. This is a syntactically valid, credentialed, but deliberately
/// unroutable connection string for exactly that case, mirroring the pattern individual
/// <c>ReadinessTests.cs</c> suites already use for the same reason.
/// </summary>
public static class RequiredSecretsTestSupport
{
    public const string UnreachableCredentialedConnectionString =
        "Server=127.0.0.1,1;Database=unreachable;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=True";

    /// <summary>
    /// Supplies <paramref name="connectionStringKey"/> (e.g. <c>"OrdersDb"</c>) with
    /// <see cref="UnreachableCredentialedConnectionString"/>, so
    /// <c>AddRequiredSecretsValidation</c>'s startup check passes without the suite needing a real
    /// database.
    /// </summary>
    public static IWebHostBuilder UseUnreachableRequiredSecret(this IWebHostBuilder builder, string connectionStringKey) =>
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{connectionStringKey}"] = UnreachableCredentialedConnectionString,
            }));
}
