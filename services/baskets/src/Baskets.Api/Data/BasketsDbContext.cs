using Microsoft.EntityFrameworkCore;

namespace Baskets.Api.Data;

/// <summary>
/// The Baskets service's own database, and the only database it can reach — no other service's
/// schema is referenced here or anywhere downstream of it (spec FR-004, FR-005).
/// </summary>
/// <remarks>
/// Holds the basket surface 004-minimal-shopping-spa needs: a basket per shopper, its line items,
/// and the quantities and captured prices on them. Reservation rules, expiry, and promotions still
/// belong to this service's later domain stories.
/// The connection is supplied through <see cref="DbContextOptions{TContext}"/> at registration
/// rather than resolved inside the context, so a tenant-keyed connection resolver can replace that
/// one call site without changing this type.
/// </remarks>
public class BasketsDbContext(DbContextOptions<BasketsDbContext> options) : DbContext(options)
{
    public DbSet<Basket> Baskets => Set<Basket>();

    public DbSet<BasketLineItem> BasketLineItems => Set<BasketLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Basket>(basket =>
        {
            basket.HasKey(entity => entity.Id);

            basket.Property(entity => entity.CustomerRef).IsRequired().HasMaxLength(200);

            // Spec FR-006: at most one open basket per shopper. A unique index rather than a
            // check-before-insert, because two concurrent first-visit requests would both pass the
            // check and only the database can settle it.
            basket.HasIndex(entity => entity.CustomerRef).IsUnique();

            // Computed from the lines, never stored (data-model.md). Without this EF would try to
            // map it to a column and the total would become a second source of truth.
            basket.Ignore(entity => entity.Total);

            basket.HasMany(entity => entity.LineItems)
                .WithOne()
                .HasForeignKey(line => line.BasketId)
                // Line items have no life of their own: deleting a basket deletes them with it.
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BasketLineItem>(line =>
        {
            line.HasKey(entity => entity.Id);

            // Spec FR-005: a product occupies at most one line per basket. Enforced here so the
            // merge rule cannot be defeated by two concurrent additions of the same product.
            line.HasIndex(entity => new { entity.BasketId, entity.ProductId }).IsUnique();

            // Explicit precision: EF's default for decimal on SQL Server truncates to two decimal
            // places with a warning. Money is stated, not inferred.
            line.Property(entity => entity.UnitPrice).HasPrecision(18, 2);

            line.Ignore(entity => entity.LineTotal);
        });
    }
}
