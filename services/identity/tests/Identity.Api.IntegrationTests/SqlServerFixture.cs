using Testcontainers.MsSql;

namespace Identity.Api.IntegrationTests;

/// <summary>
/// One real SQL Server instance for the whole suite — constitution Principle III: real dependencies
/// via Testcontainers, never an in-memory provider or a hand-rolled fake. Mirrors
/// <c>Parties.Api.IntegrationTests.SqlServerFixture</c>. Backs all three of this service's
/// DbContexts (data-model.md — Identity User, Client Application) against the same database, exactly
/// as the running service does.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
