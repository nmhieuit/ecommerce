using System.Net;
using System.Net.Http.Json;
using Baskets.Api.Data;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// Spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) FR-002/SC-002, User Story 2: số câu lệnh
/// SQL phát ra để tải 1 giỏ cùng các dòng của nó không được tăng theo số dòng.
/// </summary>
/// <remarks>
/// <para>
/// Test này khoá lại hành vi vốn đã đúng từ trước tính năng —
/// <c>.Include(b =&gt; b.LineItems)</c> của <c>BasketEndpoints</c> eager-load trong 1 truy vấn duy
/// nhất — thành 1 regression test tự động. Independent Test của US2 đòi "profile bằng EF Core
/// logging", không chỉ đọc mã, nên test này thay bước thủ công đó bằng
/// <see cref="QueryCountInterceptor"/> gắn vào 1 SQL Server thật qua Testcontainers (hiến chương
/// Principle III).
/// </para>
/// <para>
/// 2 giỏ, không phải 1 giỏ đem so với 1 con số cứng: so 1 giỏ 1 dòng với 1 giỏ 5 dòng mới chứng minh
/// được số câu lệnh độc lập với N, thay vì chỉ "nhỏ" với 1 giá trị N cụ thể.
/// </para>
/// </remarks>
public class BasketQueryCountTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";

    /// <summary>
    /// Kiểm tra: tải giỏ 1 dòng và giỏ 5 dòng qua `GET /baskets/{id}` thật (SQL Server thật) → số câu
    /// lệnh SQL phát ra BẰNG NHAU (không tăng theo số dòng).
    /// Lý do: FR-002/SC-002 — 5 dòng là 5 hàng trong CÙNG 1 result set của `.Include()`, không phải 5
    /// round trip; so sánh bằng nhau (chặt hơn "nhỏ hơn 1 hằng số nào đó") là khẳng định sắc nhất mà 1
    /// truy vấn eager-load đơn có thể đưa ra.
    /// Lưu ý: chỉ phủ đường `GET /baskets/{id}` của Baskets — lời gọi xuyên service ở BFF (đường thật sự
    /// nặng nhất theo `architecture/023`) được test ở `ProductLookupBatchingTests`.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002, SC-002, US2.
    /// </summary>
    [Fact]
    public async Task GetBasket_IssuesTheSameNumberOfStatements_RegardlessOfLineItemCount()
    {
        var interceptor = new QueryCountInterceptor();
        await using var factory = await CreateFactoryAsync("basket-query-count", interceptor);

        var oneLineBasketId = await SeedBasketAsync(factory, "shopper-one-line", lineItemCount: 1);
        var fiveLineBasketId = await SeedBasketAsync(factory, "shopper-five-lines", lineItemCount: 5);

        var client = CreateClient(factory);

        interceptor.Reset();
        var oneLineResponse = await client.GetAsync($"/baskets/{oneLineBasketId}");
        var statementsForOneLine = interceptor.ExecutedCommandCount;

        interceptor.Reset();
        var fiveLineResponse = await client.GetAsync($"/baskets/{fiveLineBasketId}");
        var statementsForFiveLines = interceptor.ExecutedCommandCount;

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — cả 2 request đều phải `200`.
        Assert.Equal(HttpStatusCode.OK, oneLineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fiveLineResponse.StatusCode);

        var oneLineBasket = await oneLineResponse.Content.ReadFromJsonAsync<BasketResponse>();
        var fiveLineBasket = await fiveLineResponse.Content.ReadFromJsonAsync<BasketResponse>();
        // Assert.Single(tập hợp): xanh khi đúng 1 dòng — bảo đảm 2 giỏ thật sự có 1 và 5 dòng để so sánh.
        Assert.Single(oneLineBasket!.Items);
        Assert.Equal(5, fiveLineBasket!.Items.Count);

        // Khẳng định chính: số câu lệnh KHÔNG tăng theo số dòng. Bằng nhau (không chỉ "bị chặn bởi 1
        // hằng số") là khẳng định sắc nhất mà eager-load 1 truy vấn của `.Include()` đưa ra ở đây — 5
        // dòng là mỗi dòng 1 hàng trong CÙNG 1 result set, không phải 5 round trip.
        // Assert.Equal(kỳ vọng, thực tế): đỏ khi giỏ 5 dòng tốn nhiều câu lệnh hơn giỏ 1 dòng (N+1).
        Assert.Equal(statementsForOneLine, statementsForFiveLines);
    }

    private static async Task<Guid> SeedBasketAsync(
        WebApplicationFactory<Program> factory, string customerRef, int lineItemCount)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<BasketsDbContext>();
        var basket = Basket.ForCustomer(customerRef);

        for (var i = 0; i < lineItemCount; i++)
        {
            basket.AddItem(Guid.NewGuid(), quantity: 1, unitPrice: 9.99m);
        }

        dbContext.Baskets.Add(basket);
        await dbContext.SaveChangesAsync();

        return basket.Id;
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(
        string database, QueryCountInterceptor interceptor)
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
                    ["ConnectionStrings:BasketsDb"] = connectionString,
                }));
            host.UseTestJwtBearer();

            // Re-registers BasketsDbContext with the same options the app's own Program.cs uses,
            // plus the counting interceptor — the last AddDbContext registration wins, so this
            // replaces rather than duplicates the production registration (no other behaviour
            // changes: same tenant gate, same connection string).
            host.ConfigureServices(services =>
            {
                services.AddDbContext<BasketsDbContext>((serviceProvider, options) =>
                {
                    serviceProvider.GetRequiredService<TenantContext>().RequireTenantId();
                    options.UseSqlServer(connectionString);
                    options.AddInterceptors(interceptor);
                });
            });
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<BasketsDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, "query-count-caller");

        return client;
    }

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    private sealed record BasketLineItemResponse(
        Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);
}
