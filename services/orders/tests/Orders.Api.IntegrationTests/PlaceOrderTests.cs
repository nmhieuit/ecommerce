using System.Net;
using System.Net.Http.Json;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Tenancy;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// 004-minimal-shopping-spa spec FR-022 and contracts/downstream-openapi.yaml: an order is created
/// from the lines it is sent, with the total computed here.
/// </summary>
public class PlaceOrderTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    [Fact]
    public async Task PlaceOrder_CreatesTheOrder_AndComputesItsTotal()
    {
        await using var factory = await CreateFactoryAsync("orders-place");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[]
            {
                new { productId = Notebook, quantity = 2, unitPrice = 12.50m },
                new { productId = Apron, quantity = 1, unitPrice = 34.25m },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.NotEqual(Guid.Empty, order.Id);

        // quickstart.md Scenario 5's figure.
        Assert.Equal(59.25m, order.Total);
    }

    /// <summary>
    /// Spec SC-005: "the order reference shown on the confirmation screen matches the order actually
    /// created in the backend." That only holds if the order is retrievable by the identifier the
    /// caller was handed.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_ReturnsAnIdentifier_ThatReadsBackAsTheSameOrder()
    {
        await using var factory = await CreateFactoryAsync("orders-readback");
        var client = CreateClient(factory);

        var created = await (await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        })).Content.ReadFromJsonAsync<OrderResponse>();

        var readBack = await client.GetFromJsonAsync<OrderResponse>($"/orders/{created!.Id}");

        Assert.Equal(created.Id, readBack!.Id);
        Assert.Equal(created.Total, readBack.Total);
        Assert.Equal(created.PlacedAtUtc, readBack.PlacedAtUtc);
    }

    [Fact]
    public async Task PlaceOrder_ReturnsALocationHeader_ForTheCreatedOrder()
    {
        await using var factory = await CreateFactoryAsync("orders-location");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        });

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal($"/orders/{order!.Id}", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// The server-side half of spec FR-008. The storefront blocks an empty-basket checkout before
    /// it is sent, but client-side validation is UX only (constitution Principle VI) — an order for
    /// nothing must be impossible to create even by calling this directly.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_Rejects_ARequestWithNoLines()
    {
        await using var factory = await CreateFactoryAsync("orders-empty");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new { items = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_Rejects_ALineWithANonPositiveQuantity()
    {
        await using var factory = await CreateFactoryAsync("orders-bad-quantity");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 0, unitPrice = 12.50m } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// An order belongs to somebody. A request that did not come through the gateway resolved no
    /// caller, and must not be able to leave an order behind.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_Fails_WhenNoCallerWasResolved()
    {
        await using var factory = await CreateFactoryAsync("orders-no-caller");

        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// 006-e2e-order-demo FR-005: the order carries the tenant resolved for the request that placed
    /// it. Asserted against the stored row rather than the response, because the response could
    /// echo a value the record never kept.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_PersistsTheResolvedTenant_OnTheOrderRow()
    {
        await using var factory = await CreateFactoryAsync("orders-tenant-persisted");
        var client = CreateClient(factory);

        var created = await (await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        })).Content.ReadFromJsonAsync<OrderResponse>();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var stored = await dbContext.Orders.AsNoTracking().SingleAsync(order => order.Id == created!.Id);

        Assert.Equal(TenantId, stored.TenantId);
    }

    /// <summary>
    /// FR-005 again, from the other direction: the tenant stored is the one the gateway resolved,
    /// not something the caller could choose. A body that names a different tenant changes nothing
    /// - the field is not part of the request contract, and adding one is the smuggling route
    /// constitution Principle V exists to close.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_IgnoresATenantNamedInTheRequestBody()
    {
        await using var factory = await CreateFactoryAsync("orders-tenant-smuggle");
        var client = CreateClient(factory);

        var created = await (await client.PostAsJsonAsync("/orders", new
        {
            tenantId = "someone-elses-tenant",
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        })).Content.ReadFromJsonAsync<OrderResponse>();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var stored = await dbContext.Orders.AsNoTracking().SingleAsync(order => order.Id == created!.Id);

        Assert.Equal(TenantId, stored.TenantId);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(string database)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = connectionString,
                }));
            host.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, Shopper);

        return client;
    }

    private sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total, string TenantId);
}
