using System.Net;
using Bff.Api.DownstreamClients;
using PactNet.Matchers;

namespace Bff.Api.ContractTests;

/// <summary>
/// What the BFF relies on from the orders service, stated as a Pact document
/// (<c>pacts/bff-orders.json</c>) for the orders service's own build to verify
/// (011-consumer-contract-tests FR-001, FR-002).
/// </summary>
/// <remarks>
/// <c>tenantId</c> is deliberately absent from both expected responses even though the orders
/// service returns it. <see cref="OrderResource"/> does not read it, and a pact that named it would
/// stop the orders service dropping a field nobody consumes — the tolerant-reader rule FR-007
/// exists to prevent.
/// </remarks>
public class OrdersConsumerPactTests
{
    private const string Provider = "orders";

    private static readonly Guid ExistingOrderId = new("7c2e0d44-0003-4000-8000-000000000001");
    private static readonly Guid ProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: khai báo kỳ vọng của BFF ở 2 interaction (`GET /orders/{id}`, `POST /orders`) — ghi
    /// vào `pacts/bff-orders.json`, cố ý KHÔNG khai `tenantId` dù orders có trả trường đó.
    /// Lý do: `OrderResource` phía BFF không đọc `tenantId`; không khai nó trong pact là áp dụng
    /// đúng quy tắc tolerant-reader (FR-007) — nếu khai, service orders sẽ bị buộc giữ mãi 1 trường
    /// không ai dùng.
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T011, US1 (FR-001, FR-002, FR-007).
    /// </summary>
    [Fact]
    public async Task OrderInteractions_DependOnIdPlacedAtUtcAndTotal()
    {
        var pact = BffPact.For(Provider);

        pact
            .UponReceiving("a request for an order by id")
                .Given(
                    "an order exists",
                    new Dictionary<string, string> { ["orderId"] = ExistingOrderId.ToString() })
                .WithRequest(HttpMethod.Get, $"/orders/{ExistingOrderId}")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", BffPact.SubjectId)
                .WithHeader("Authorization", BffPact.AuthorizationHeader)
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(OrderBody());

        pact
            .UponReceiving("a request to place an order")
                .WithRequest(HttpMethod.Post, "/orders")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", BffPact.SubjectId)
                .WithHeader("Authorization", BffPact.AuthorizationHeader)
                // Lines only. No total is sent, and the pact says so: the total is the orders
                // service's answer, not the BFF's claim (004 research.md Decision 8).
                .WithJsonBody(new
                {
                    items = new[]
                    {
                        new { productId = ProductId, quantity = 2, unitPrice = 12.50m },
                    },
                })
            .WillRespond()
                // 201, because the client calls EnsureSuccessStatusCode and then reads the body —
                // a producer that switched to 202 with no body would break it.
                .WithStatus(HttpStatusCode.Created)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(OrderBody());

        await pact.VerifyAsync(async context =>
        {
            using var httpClient = BffPact.CreateRelayingClient(context.MockServerUri);
            var client = new OrdersApiClient(httpClient);

            var fetched = await client.GetOrderAsync(ExistingOrderId, CancellationToken.None);
            AssertReadable(fetched);

            var placed = await client.PlaceOrderAsync(
                new PlaceOrderCommand([new PlaceOrderLine(ProductId, Quantity: 2, UnitPrice: 12.50m)]),
                CancellationToken.None);
            AssertReadable(placed);
        });
    }

    private static object OrderBody() => new
    {
        id = Match.Regex(ExistingOrderId.ToString(), PactRegex.Uuid),
        placedAtUtc = Match.Regex("2026-08-22T10:15:30.1234567Z", PactRegex.Iso8601DateTime),
        total = Match.Number(25.00m),
    };

    /// <summary>Assert dùng chung cho cả GET và POST — cùng kiểm 1 hình dạng.</summary>
    private static void AssertReadable(OrderResource? order)
    {
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null.
        Assert.NotNull(order);
        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau — cho Id và
        // PlacedAtUtc (không được là Guid rỗng / thời điểm mặc định).
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.NotEqual(default, order.PlacedAtUtc);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Cả 3 Assert chứng minh
        // JSON dựng ở pact thật sự dựng lại được thành OrderResource, không chỉ đúng cú pháp JSON.
        Assert.Equal(25.00m, order.Total);
    }
}
