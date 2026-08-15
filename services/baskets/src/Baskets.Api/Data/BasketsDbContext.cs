using Microsoft.EntityFrameworkCore;

namespace Baskets.Api.Data;

/// <summary>
/// The Baskets service's own database, and the only database it can reach — no other service's
/// schema is referenced here or anywhere downstream of it (spec FR-004, FR-005).
/// </summary>
/// <remarks>
/// Holds the minimal basket read surface the BFF proxies (002-gateway-bff-routing). Line items and
/// basket behaviour still belong to this service's first domain story; what exists here is the
/// smallest set of fields that makes the BFF's basket route a real proxy.
/// The connection is supplied through <see cref="DbContextOptions{TContext}"/> at registration
/// rather than resolved inside the context, so SCRUM-12's tenant-keyed connection resolver can
/// replace that one call site without changing this type.
/// </remarks>
public class BasketsDbContext(DbContextOptions<BasketsDbContext> options) : DbContext(options)
{
    public DbSet<Basket> Baskets => Set<Basket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Basket>(basket => basket.HasKey(entity => entity.Id));
    }
}
