using Baskets.Api.Data;
using IntegrationTestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace Baskets.Api.ContractTests;

/// <summary>
/// This service's own build, verifying its real basket responses against what the BFF says it
/// relies on (<c>pacts/bff-baskets.json</c>). A renamed field, or a status the BFF reads meaning
/// dropped, fails here — in the baskets build — rather than in the BFF's
/// (011-consumer-contract-tests FR-001, FR-003, FR-005; spec SC-002).
/// </summary>
public class BasketsProviderPactTests(SqlServerFixture sqlServer, ITestOutputHelper output)
    : IClassFixture<SqlServerFixture>
{
    /// <summary>Matches the tenant the consumer pact relays. Any non-blank value would do.</summary>
    private const string TenantId = "contoso";

    private static readonly Guid SeededProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: dựng Baskets API thật rồi cho `PactVerifier` gửi lại đúng các request đã ghi trong
    /// `pacts/bff-baskets.json` (4 interaction: đọc giỏ, thêm dòng, xoá giỏ có hàng, xoá giỏ rỗng) —
    /// phản hồi thật phải khớp.
    /// Lý do: đổi tên/bớt 1 trường `BasketResponse`, hoặc đổi mã trạng thái 204/409, phải làm ĐÚNG
    /// build của baskets đỏ, không phải build của BFF.
    /// Từng ĐỎ trên mọi interaction (401) từ 2026-09-03 (spec 015 gắn `.RequireAuthorization` thẳng
    /// vào endpoint, `FallbackPolicy = null` cũ của host hết tác dụng) tới 2026-09-22, khi
    /// `PactProviderHost` chuyển sang `UseTestJwtBearer()` và test này gắn token qua
    /// `WithCustomHeader` — xem QA_Debt mục 011 (đã đánh dấu đã vá).
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T014/T015, US1 (FR-001, FR-003, FR-005).
    /// </summary>
    [Fact]
    public void BasketResponses_SatisfyTheBffsRecordedExpectations()
    {
        using var provider = new PactProviderHost(sqlServer.ConnectionString, TenantId, ApplyStateAsync);

        // Forces the host to build and start before the verifier is pointed at it. The factory
        // creates its host lazily, so without this BaseUri is still the placeholder.
        using var _ = provider.CreateDefaultClient();

        using var verifier = new PactVerifier(
            "baskets",
            new PactVerifierConfig { Outputters = [new PactTestOutput(output)] });

        // Kiểm chứng nằm ở .Verify() bên dưới: không có Assert.* nào — PactVerifier tự ném
        // PactVerificationFailedException nếu response thật lệch khỏi file pact. Xanh khi cả 4
        // interaction khớp, đỏ (kèm log nêu rõ trường/status sai) khi lệch.
        verifier
            .WithHttpEndpoint(provider.BaseUri)
            .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-baskets.json")))
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
    /// Puts this service's baskets into the state an interaction was recorded under.
    /// </summary>
    /// <remarks>
    /// Every state clears the table first and then seeds only the caller it names. The two clear
    /// interactions differ solely in whether the caller's basket has anything in it, so a state
    /// that added to what the previous interaction left behind would make the 204 and the 409
    /// depend on replay order.
    /// </remarks>
    private static async Task ApplyStateAsync(ProviderState state, IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<BasketsDbContext>();
        await dbContext.Database.MigrateAsync();

        // An interaction recorded without a Given depends on no prior state — placing an order
        // needs nothing in the database beforehand. The verifier still announces it, with an empty
        // description, so it is answered here rather than falling through to the unknown-state
        // guard below.
        if (string.IsNullOrEmpty(state.State))
        {
            return;
        }

        dbContext.Baskets.RemoveRange(dbContext.Baskets);
        await dbContext.SaveChangesAsync();

        var basket = Basket.ForCustomer(state.Require("customerRef"));

        switch (state.State)
        {
            case "a basket holding one item exists for the caller":
                basket.AddItem(SeededProductId, quantity: 2, unitPrice: 12.50m);
                break;

            case "an empty basket exists for the caller":
                break;

            default:
                // Loudly, not silently: a pact whose state nothing here recognises would otherwise
                // be verified against whatever the database happened to hold.
                throw new InvalidOperationException(
                    $"No provider state handler for '{state.State}'. Add one, or correct the consumer pact.");
        }

        dbContext.Baskets.Add(basket);
        await dbContext.SaveChangesAsync();
    }
}
