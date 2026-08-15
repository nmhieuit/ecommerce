using Microsoft.EntityFrameworkCore;

namespace Parties.Api.Data;

/// <summary>
/// The Parties service's own database, and the only database it can reach — no other service's
/// schema is referenced here or anywhere downstream of it (spec FR-004, FR-005).
/// </summary>
/// <remarks>
/// Holds the minimal party read surface the BFF proxies (002-gateway-bff-routing). Contact details
/// and identity linkage still belong to this service's first domain story; what exists here is the
/// smallest set of fields that makes the BFF's party route a real proxy.
/// The connection is supplied through <see cref="DbContextOptions{TContext}"/> at registration
/// rather than resolved inside the context, so SCRUM-12's tenant-keyed connection resolver can
/// replace that one call site without changing this type.
/// </remarks>
public class PartiesDbContext(DbContextOptions<PartiesDbContext> options) : DbContext(options)
{
    public DbSet<Party> Parties => Set<Party>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Party>(party =>
        {
            party.HasKey(entity => entity.Id);
            party.Property(entity => entity.DisplayName).IsRequired().HasMaxLength(200);
        });
    }
}
