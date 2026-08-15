using Microsoft.EntityFrameworkCore;

namespace Parties.Api.Data;

/// <summary>
/// The Parties service's own database, and the only database it can reach — no other service's
/// schema is referenced here or anywhere downstream of it (spec FR-004, FR-005).
/// </summary>
/// <remarks>
/// Deliberately has no entity sets: this feature scaffolds the service shell, and Party plus its
/// related identity records arrive with the first domain story. The context exists now so the
/// readiness probe can prove real connectivity rather than assert process liveness alone.
/// The connection is supplied through <see cref="DbContextOptions{TContext}"/> at registration
/// rather than resolved inside the context, so SCRUM-12's tenant-keyed connection resolver can
/// replace that one call site without changing this type.
/// </remarks>
public class PartiesDbContext(DbContextOptions<PartiesDbContext> options) : DbContext(options);
