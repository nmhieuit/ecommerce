using IntegrationTestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace Orders.Api.ContractTests;

/// <summary>
/// This service's own build, verifying its real order responses against what the BFF says it relies
/// on (<c>pacts/bff-orders.json</c>). A renamed or dropped field the BFF reads fails here, in the
/// orders build, rather than in the BFF's (011-consumer-contract-tests FR-001, FR-003, FR-005;
/// spec SC-002).
/// </summary>
/// <remarks>
/// The pact says nothing about <c>tenantId</c>, which this service does return. That is the
/// tolerant-reader rule working as intended (FR-007): the BFF's <c>OrderResource</c> never reads
/// it, so this service stays free to change it without breaking a consumer that does not depend
/// on it. Verification passing here is evidence of that, not an oversight.
/// </remarks>
public class OrdersProviderPactTests(SqlServerFixture sqlServer, ITestOutputHelper output)
    : IClassFixture<SqlServerFixture>
{
    /// <summary>Matches the tenant the consumer pact relays. Any non-blank value would do.</summary>
    private const string TenantId = "contoso";

    private static readonly Guid SeededProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: dựng Orders API thật rồi cho `PactVerifier` gửi lại đúng các request đã ghi trong
    /// `pacts/bff-orders.json` (đọc đơn theo id, đặt đơn) — phản hồi thật phải khớp.
    /// Lý do: đổi tên/bớt 1 trường `OrderResponse` mà BFF đang đọc (`id`/`placedAtUtc`/`total`) phải
    /// làm ĐÚNG build của orders đỏ, không phải build của BFF; verify PASS dù orders vẫn trả thêm
    /// `tenantId` (pact không khai) là bằng chứng tolerant-reader (FR-007) hoạt động đúng, không phải
    /// thiếu sót.
    /// Từng ĐỎ trên mọi interaction (401) từ 2026-09-03 (spec 015 gắn `.RequireAuthorization` thẳng
    /// vào endpoint, `FallbackPolicy = null` cũ của host hết tác dụng) tới 2026-09-22, khi
    /// `PactProviderHost` chuyển sang `UseTestJwtBearer()` và test này gắn token qua
    /// `WithCustomHeader` — xem QA_Debt mục 011 (đã đánh dấu đã vá).
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T016/T017, US1 (FR-001, FR-003, FR-005,
    /// FR-007).
    /// </summary>
    [Fact]
    public void OrderResponses_SatisfyTheBffsRecordedExpectations()
    {
        using var provider = new PactProviderHost(sqlServer.ConnectionString, TenantId, ApplyStateAsync);

        // Forces the host to build and start before the verifier is pointed at it. The factory
        // creates its host lazily, so without this BaseUri is still the placeholder.
        using var _ = provider.CreateDefaultClient();

        using var verifier = new PactVerifier(
            "orders",
            new PactVerifierConfig { Outputters = [new PactTestOutput(output)] });

        // Kiểm chứng nằm ở .Verify() bên dưới: không có Assert.* nào — PactVerifier tự ném
        // PactVerificationFailedException nếu response thật lệch khỏi file pact. Xanh khi cả 2
        // interaction khớp, đỏ (kèm log nêu rõ trường/status sai) khi lệch.
        verifier
            .WithHttpEndpoint(provider.BaseUri)
            .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-orders.json")))
            .WithProviderStateUrl(provider.ProviderStateUri)
            // 015-deny-by-default-authz: every replayed request must carry a token this host's
            // UseTestJwtBearer() accepts, or AuthorizationPolicies.ApiScope refuses it with 401
            // before the response body is ever compared. Minted fresh here rather than read from
            // the pact file's own (regex-matched, not literal) Authorization header — see
            // BffPact.AuthorizationHeader's remarks — so it is never stale relative to
            // TestJwtBearer's 5-minute default expiry.
            .WithCustomHeader("Authorization", "Bearer " + TestJwtBearer.CreateToken())
            .Verify();
    }

    /// <summary>
    /// Puts this service's orders into the state an interaction was recorded under. The order is
    /// built through <see cref="Order.PlaceFrom"/> rather than by setting properties, so the row
    /// the read route answers with is one this service's own domain rules produced.
    /// </summary>
    private static async Task ApplyStateAsync(ProviderState state, IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.MigrateAsync();

        // An interaction recorded without a Given depends on no prior state — placing an order
        // needs nothing in the database beforehand. The verifier still announces it, with an empty
        // description, so it is answered here rather than falling through to the unknown-state
        // guard below.
        if (string.IsNullOrEmpty(state.State))
        {
            return;
        }

        switch (state.State)
        {
            case "an order exists":
                dbContext.Orders.RemoveRange(dbContext.Orders);

                var order = Order.PlaceFrom(
                    [new OrderLine(SeededProductId, Quantity: 2, UnitPrice: 12.50m)],
                    DateTime.UtcNow,
                    TenantId);

                // The consumer recorded the interaction against a specific id, so the seeded row
                // has to carry that id for the replayed GET to reach it.
                order.Id = new Guid(state.Require("orderId"));

                dbContext.Orders.Add(order);
                await dbContext.SaveChangesAsync();
                break;

            default:
                // Loudly, not silently: a pact whose state nothing here recognises would otherwise
                // be verified against whatever the database happened to hold.
                throw new InvalidOperationException(
                    $"No provider state handler for '{state.State}'. Add one, or correct the consumer pact.");
        }
    }
}
