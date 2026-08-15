using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// One real SQL Server instance shared by the whole BFF integration suite — constitution
/// Principle III: real dependencies via Testcontainers, never an in-memory provider or a
/// hand-rolled fake.
/// </summary>
/// <remarks>
/// The BFF owns no database. This container exists for the real downstream services the BFF is
/// tested against (research.md Decision 5), each of which does own one. Every service is handed a
/// connection string naming its own database on this server rather than sharing a single catalog,
/// so the test topology mirrors the production isolation rule (constitution Principle I) instead
/// of quietly proving the BFF works in a world where all four services share a schema.
/// </remarks>
public sealed class DownstreamServicesFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>
    /// A connection string for <paramref name="database"/> on this fixture's server. The database
    /// need not exist yet — EF Core's <c>MigrateAsync</c> creates it on first use.
    /// </summary>
    public string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = database,
        }.ConnectionString;

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>
/// Binds the fixture to every test class in the suite, so the container starts once for the whole
/// run rather than once per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DownstreamServicesCollectionDefinition : ICollectionFixture<DownstreamServicesFixture>
{
    public const string Name = "downstream-services";
}
