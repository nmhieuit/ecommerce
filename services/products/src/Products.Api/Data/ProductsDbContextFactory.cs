using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Products.Api.Data;

/// <summary>
/// Builds a <see cref="ProductsDbContext"/> for the <c>dotnet ef</c> tooling only.
/// </summary>
/// <remarks>
/// Without this, EF's design-time discovery falls back to building the application's host and
/// resolving the context from DI — which since 003-stub-identity-tenant-context means hitting the
/// tenant gate and failing with <c>MissingTenantContextException</c>, because a command-line tool
/// has no request and therefore no resolved tenant. Constructing the context directly here keeps
/// the gate strict at runtime (where it matters) without making migrations impossible to author or
/// apply. This type is never used by the running service.
/// </remarks>
public sealed class ProductsDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlServer(configuration.GetConnectionString("ProductsDb"))
            .Options;

        return new ProductsDbContext(options);
    }
}
