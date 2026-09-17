namespace Identity.Api.Data;

/// <summary>
/// The non-secret fields of the one local/test <see cref="ApplicationUser"/>, applied through EF
/// Core's <c>HasData</c> in <see cref="ApplicationIdentityDbContext.OnModelCreating"/> — the same
/// pattern <c>services/products</c> uses for its catalog (<c>Data/CatalogSeed.cs</c>,
/// <c>ProductsDbContext.OnModelCreating</c>), applied automatically by <c>identity-migrate</c>, no
/// separate CLI step.
/// </summary>
/// <remarks>
/// Deliberately excludes <see cref="Microsoft.AspNetCore.Identity.IdentityUser{TKey}.PasswordHash"/>:
/// unlike a product's price, a password is a secret, and <c>HasData</c> bakes whatever value it is
/// given as a literal into a committed migration file — exactly what constitution Principle VI
/// rules out. The password is set separately, at container-run time, by
/// <see cref="TestUserPasswordProvisioning"/> against this already-seeded row.
/// </remarks>
public static class TestUserSeed
{
    /// <summary>Fixed so the migration is deterministic and so the row is recognisable in <c>AspNetUsers</c>.</summary>
    public const string Id = "9f2b1e2a-9b7a-4e3e-8f21-9a2c9f6b6a11";

    /// <summary>Matches postman/local.postman_environment.v2.json's <c>testUsername</c> — the collection assumes this exact account exists.</summary>
    public const string Email = "postman-test@local.test";

    /// <summary>Matches postman/local.postman_environment.v2.json's <c>tenantId</c>.</summary>
    public const string TenantId = "contoso";

    public static ApplicationUser User => new()
    {
        Id = Id,
        UserName = Email,
        NormalizedUserName = Email.ToUpperInvariant(),
        Email = Email,
        NormalizedEmail = Email.ToUpperInvariant(),
        EmailConfirmed = true,
        TenantId = TenantId,
        // Fixed rather than Guid.NewGuid(): HasData bakes whatever value is read at
        // migration-authoring time into the generated migration as a literal, so a random value
        // here would still end up fixed on disk — being explicit makes that visible instead of
        // accidental. Neither stamp is a credential (constitution Principle VI's concern is the
        // password), so a literal here carries none of the risk PasswordHash would.
        ConcurrencyStamp = "b3e9a5b0-9d5d-4f2e-8a30-3a1f6e0c6f9a",
        SecurityStamp = "5e2a2b1d-6f5c-4e0a-9a1a-7b1e6b8f2c3d",
    };
}
