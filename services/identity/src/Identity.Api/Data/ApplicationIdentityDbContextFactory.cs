using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Api.Data;

/// <summary>
/// Builds an <see cref="ApplicationIdentityDbContext"/> for the <c>dotnet ef</c> tooling only —
/// mirrors <c>Parties.Api.Data.PartiesDbContextFactory</c>. Design-time discovery otherwise falls
/// back to building the application host, which would start Duende IdentityServer itself; this
/// keeps migration tooling independent of that.
/// </summary>
public sealed class ApplicationIdentityDbContextFactory : IDesignTimeDbContextFactory<ApplicationIdentityDbContext>
{
    public ApplicationIdentityDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<ApplicationIdentityDbContext>()
            .UseSqlServer(
                configuration.GetConnectionString("IdentityDb"),
                sql => sql.MigrationsHistoryTable(MigrationsHistoryTables.Identity))
            .Options;

        return new ApplicationIdentityDbContext(options);
    }
}
