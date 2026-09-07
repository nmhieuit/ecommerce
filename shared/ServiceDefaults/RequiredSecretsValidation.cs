using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ServiceDefaults;

/// <summary>
/// A secret a service cannot start without — constitution Principle VI ("injected at runtime from
/// the cluster secret store") and specs/018-cluster-secret-store FR-002/FR-007. <see cref="Name"/>
/// is also the contract's naming surface: it must match the key used both in
/// <c>appsettings*.json</c>/environment variables and in that service's
/// <c>deploy/k8s/&lt;service&gt;/external-secret.yaml</c> (see
/// specs/018-cluster-secret-store/contracts/external-secret-manifest-contract.md).
/// </summary>
public sealed record RequiredSecret(string Name, Func<IConfiguration, string?> Resolve)
{
    /// <summary>
    /// The common case: a required connection string, resolved the same way
    /// <c>IConfiguration.GetConnectionString</c> already resolves it for the DbContext that will
    /// consume it (<c>ConnectionStrings:&lt;name&gt;</c> — env var override
    /// <c>ConnectionStrings__&lt;name&gt;</c>).
    /// </summary>
    /// <remarks>
    /// A non-blank value alone is not enough: the committed, non-Development
    /// <c>appsettings.json</c> deliberately still carries a host/database-only connection string
    /// with no credential (contracts/service-configuration-contract.md rule 2), so it is never
    /// blank even when the cluster never injected one. Resolves to <see langword="null"/> — treated
    /// as missing by <see cref="RequiredSecretsValidator"/> — unless the value actually carries a
    /// credential component (<c>Password=</c>/<c>Pwd=</c>) or an explicit trusted/integrated
    /// connection.
    /// </remarks>
    public static RequiredSecret ConnectionString(string name) =>
        new($"ConnectionStrings:{name}", configuration =>
        {
            var value = configuration.GetConnectionString(name);
            return HasCredential(value) ? value : null;
        });

    private static bool HasCredential(string? connectionString) =>
        connectionString is not null &&
        (Contains(connectionString, "Password=") ||
         Contains(connectionString, "Pwd=") ||
         Contains(connectionString, "Integrated Security=true") ||
         Contains(connectionString, "Trusted_Connection=true"));

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The set of <see cref="RequiredSecret"/>s one service declares via
/// <see cref="ServiceDefaultsExtensions.AddRequiredSecretsValidation"/>.
/// </summary>
public sealed class RequiredSecretsOptions
{
    public IReadOnlyList<RequiredSecret> Secrets { get; set; } = [];
}

/// <summary>
/// Fail-fast validation (research.md #3): a missing or blank required secret is a startup error,
/// not a deferred failure the first time a dependency is called. Never includes a secret's actual
/// value in the failure message — only the names of the secrets that are missing.
/// </summary>
public sealed class RequiredSecretsValidator(IConfiguration configuration) : IValidateOptions<RequiredSecretsOptions>
{
    public ValidateOptionsResult Validate(string? name, RequiredSecretsOptions options)
    {
        var missing = options.Secrets
            .Where(secret => string.IsNullOrWhiteSpace(secret.Resolve(configuration)))
            .Select(secret => secret.Name)
            .ToArray();

        if (missing.Length == 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"Missing required secret(s): {string.Join(", ", missing)}. Each must be supplied at " +
            "runtime from the cluster secret store, never hardcoded — see " +
            "specs/018-cluster-secret-store/contracts/service-configuration-contract.md.");
    }
}
