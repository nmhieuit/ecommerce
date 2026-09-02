using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Api.Data;

/// <summary>
/// Builds Duende's <see cref="ConfigurationDbContext"/> (Client/Resource store — data-model.md
/// Client Application) for the <c>dotnet ef</c> tooling only. See
/// <see cref="ApplicationIdentityDbContextFactory"/> for why this is needed at all.
/// </summary>
public sealed class ConfigurationStoreDbContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    public ConfigurationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Unlike Parties/Products/etc.'s plain DbContexts, ConfigurationDbContext resolves
        // IOptions<ConfigurationStoreOptions> from its own internal service provider at
        // construction time — a bare DbContextOptionsBuilder has none, so one is supplied
        // explicitly (verified empirically: omitting this throws
        // "Unable to resolve service for type 'ConfigurationStoreOptions'").
        var internalServices = new ServiceCollection()
            .AddOptions()
            .AddSingleton(new ConfigurationStoreOptions())
            .AddSingleton(Options.Create(new ConfigurationStoreOptions()))
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseApplicationServiceProvider(internalServices)
            .UseSqlServer(
                configuration.GetConnectionString("IdentityDb"),
                sql => sql
                    .MigrationsHistoryTable(MigrationsHistoryTables.Configuration)
                    // The DbContext type lives in Duende.IdentityServer.EntityFramework.Storage,
                    // which EF defaults the migrations assembly to; migrations must instead live in
                    // this project so they ship with this service's own image, not the NuGet
                    // package's (verified: without this, `dotnet ef` refuses with "target project
                    // doesn't match your migrations assembly"). Program.cs's AddConfigurationStore
                    // sets the same assembly at runtime.
                    .MigrationsAssembly(MigrationsAssembly.Name))
            .Options;

        return new ConfigurationDbContext(options);
    }
}
