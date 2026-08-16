using Microsoft.EntityFrameworkCore;

namespace Products.Api.Data;

/// <summary>
/// The Products service's own database, and the only database it can reach — no other service's
/// schema is referenced here or anywhere downstream of it (spec FR-004, FR-005).
/// </summary>
/// <remarks>
/// Holds the minimal catalog read surface the BFF proxies (002-gateway-bff-routing). Product's
/// catalog/pricing depth still belongs to this service's first domain story; what exists here is
/// the smallest set of fields that makes the BFF's product-listing route a real proxy rather than
/// a route with nothing behind it.
/// The connection is supplied through <see cref="DbContextOptions{TContext}"/> at registration
/// rather than resolved inside the context, so SCRUM-12's tenant-keyed connection resolver can
/// replace that one call site without changing this type.
/// </remarks>
public class ProductsDbContext(DbContextOptions<ProductsDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(product =>
        {
            product.HasKey(entity => entity.Id);
            product.Property(entity => entity.Name).IsRequired().HasMaxLength(200);

            // Explicit precision: EF's default for decimal on SQL Server truncates to two decimal
            // places with a warning. Money is stated, not inferred.
            product.Property(entity => entity.Price).HasPrecision(18, 2);

            // The catalog every environment starts with (004-minimal-shopping-spa FR-018). Seeded
            // through the migration history rather than at startup, so it is applied once and rolls
            // back with the migration that introduced it.
            product.HasData(CatalogSeed.Products);
        });
    }
}
