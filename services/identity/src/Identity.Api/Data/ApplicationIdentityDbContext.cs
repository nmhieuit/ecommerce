using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Data;

/// <summary>
/// The user credential store (data-model.md — Identity User), backed by ASP.NET Core Identity's
/// own schema. This is one of three <see cref="DbContext"/>s this service owns against the same
/// "identity" database — the other two (Duende's <c>ConfigurationDbContext</c> and
/// <c>PersistedGrantDbContext</c>) are wired in <c>Program.cs</c> via <c>AddConfigurationStore</c>/
/// <c>AddOperationalStore</c>, not hand-authored here.
/// </summary>
public sealed class ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
            user.Property(u => u.TenantId).IsRequired().HasMaxLength(100));
    }
}
