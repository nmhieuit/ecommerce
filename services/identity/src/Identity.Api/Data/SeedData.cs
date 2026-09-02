using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;

namespace Identity.Api.Data;

/// <summary>
/// Seeds <see cref="Config"/>'s clients, resources, and scopes into the configuration store
/// (data-model.md — Client Application). Deliberately holds no user credentials — seeding a demo
/// account with a known password here would put it on every environment this image runs in,
/// which constitution Principle VI's "secrets MUST NOT appear in source, configuration files,
/// container images" rules out for anything reachable in production.
/// </summary>
/// <remarks>
/// Invoked explicitly via the <c>--seed</c> command-line flag (<c>Program.cs</c>), the same pattern
/// <c>services/parties</c>'s migrator container uses for schema — a one-shot step that must
/// succeed before the service is exposed, not something every replica races to do at startup
/// (which, for a table with a uniqueness expectation on <c>ClientId</c>, is exactly the kind of
/// race the migration-as-a-separate-step convention this platform already follows exists to avoid).
/// </remarks>
public static class SeedData
{
    public static async Task EnsureSeedDataAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var configurationDbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        foreach (var client in Config.Clients)
        {
            if (!configurationDbContext.Clients.Any(existing => existing.ClientId == client.ClientId))
            {
                configurationDbContext.Clients.Add(client.ToEntity());
            }
        }

        foreach (var identityResource in Config.IdentityResources)
        {
            if (!configurationDbContext.IdentityResources.Any(existing => existing.Name == identityResource.Name))
            {
                configurationDbContext.IdentityResources.Add(identityResource.ToEntity());
            }
        }

        foreach (var apiScope in Config.ApiScopes)
        {
            if (!configurationDbContext.ApiScopes.Any(existing => existing.Name == apiScope.Name))
            {
                configurationDbContext.ApiScopes.Add(apiScope.ToEntity());
            }
        }

        foreach (var apiResource in Config.ApiResources)
        {
            if (!configurationDbContext.ApiResources.Any(existing => existing.Name == apiResource.Name))
            {
                configurationDbContext.ApiResources.Add(apiResource.ToEntity());
            }
        }

        await configurationDbContext.SaveChangesAsync();
    }
}
