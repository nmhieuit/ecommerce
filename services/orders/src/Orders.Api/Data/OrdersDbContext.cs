using Microsoft.EntityFrameworkCore;

namespace Orders.Api.Data;

/// <summary>
/// The Orders service's own database, and the only database it can reach — no other service's
/// schema is referenced here or anywhere downstream of it (spec FR-004, FR-005).
/// </summary>
/// <remarks>
/// Holds the minimal order read surface the BFF proxies (002-gateway-bff-routing). Line items,
/// status transitions, and the outbox table (constitution Principle IV) still belong to this
/// service's first domain story; what exists here is the smallest set of fields that makes the
/// BFF's order route a real proxy.
/// The connection is supplied through <see cref="DbContextOptions{TContext}"/> at registration
/// rather than resolved inside the context, so SCRUM-12's tenant-keyed connection resolver can
/// replace that one call site without changing this type.
/// </remarks>
public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(entity => entity.Id);

            // Explicit precision: EF's default for decimal on SQL Server truncates to two decimal
            // places with a warning. Money is stated, not inferred.
            order.Property(entity => entity.Total).HasPrecision(18, 2);

            // Nullable on purpose - the expand half of expand/contract (constitution Principle X,
            // 006 research.md Decision 3). A NOT NULL column with no default would break the
            // previously deployed version's inserts the moment both versions run side by side,
            // which is exactly what that principle forbids. Every row this version writes carries a
            // tenant regardless, because Order.PlaceFrom will not build one without it.
            //
            // Bounded rather than nvarchar(max): the tenant identifier doubles as a SQL identifier
            // for the schema-per-tenant separation still to come, and an unbounded key column
            // cannot be indexed when that lands.
            order.Property(entity => entity.TenantId).HasMaxLength(128);
        });
    }
}
