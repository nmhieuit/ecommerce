using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Api.Data;

/// <summary>
/// Builds Duende's <see cref="PersistedGrantDbContext"/> (tokens/grants/device-code operational
/// store) for the <c>dotnet ef</c> tooling only. See
/// <see cref="ApplicationIdentityDbContextFactory"/> for why this is needed at all.
/// </summary>
public sealed class PersistedGrantStoreDbContextFactory : IDesignTimeDbContextFactory<PersistedGrantDbContext>
{
    public PersistedGrantDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // See ConfigurationStoreDbContextFactory's equivalent comment — PersistedGrantDbContext
        // resolves IOptions<OperationalStoreOptions> the same way.
        var internalServices = new ServiceCollection()
            .AddOptions()
            .AddSingleton(new OperationalStoreOptions())
            .AddSingleton(Options.Create(new OperationalStoreOptions()))
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<PersistedGrantDbContext>()
            .UseApplicationServiceProvider(internalServices)
            .UseSqlServer(
                configuration.GetConnectionString("IdentityDb"),
                sql => sql
                    .MigrationsHistoryTable(MigrationsHistoryTables.PersistedGrant)
                    .MigrationsAssembly(MigrationsAssembly.Name))
            .Options;

        return new PersistedGrantDbContext(options);
    }
}
