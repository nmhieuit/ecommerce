namespace Identity.Api.Data;

/// <summary>
/// Distinct <c>__EFMigrationsHistory</c> table names for this service's three
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>s, all pointing at the same "identity"
/// database. EF Core would otherwise have all three record their applied migrations in the same
/// default-named history table; distinct names keep each context's migration history legible on
/// its own, matching Duende's own documented multi-context sample convention.
/// </summary>
public static class MigrationsHistoryTables
{
    public const string Identity = "__EFMigrationsHistory_Identity";
    public const string Configuration = "__EFMigrationsHistory_Configuration";
    public const string PersistedGrant = "__EFMigrationsHistory_PersistedGrant";
}

/// <summary>
/// The assembly Duende's two EF stores must generate/apply migrations against. Their DbContext
/// types live in <c>Duende.IdentityServer.EntityFramework.Storage</c> — EF defaults the migrations
/// assembly to wherever the DbContext type itself is declared, which would mean this service's
/// schema changes ship inside a third-party NuGet package instead of this project's own image
/// (verified: without pointing this at <c>Identity.Api</c> explicitly, <c>dotnet ef</c> refuses
/// with "target project doesn't match your migrations assembly"). Both
/// <see cref="Data.ConfigurationStoreDbContextFactory"/>/<see cref="Data.PersistedGrantStoreDbContextFactory"/>
/// (design time) and <c>Program.cs</c>'s <c>AddConfigurationStore</c>/<c>AddOperationalStore</c>
/// calls (runtime) must agree on this value.
/// </summary>
public static class MigrationsAssembly
{
    public const string Name = "Identity.Api";
}
