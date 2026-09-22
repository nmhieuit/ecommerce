using IntegrationTestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PactNet.Verifier;
using Xunit.Abstractions;
using Products.Api.Data;

namespace Products.Api.ContractTests;

/// <summary>
/// This service's own build, verifying its real catalog responses against what the BFF says it
/// relies on (<c>pacts/bff-products.json</c>). A change to <c>ProductResponse</c> that drops or
/// renames a field the BFF reads fails here, in the products build, rather than in the BFF's
/// (011-consumer-contract-tests FR-001, FR-003, FR-005; spec SC-002).
/// </summary>
/// <remarks>
/// The pact is read from the repository's committed <c>pacts/</c> directory rather than from a
/// broker: the file is the exchange mechanism for now, and standing a broker up is ADR-0006 Action
/// Item 1 (research.md Decision 2).
/// </remarks>
public class ProductsProviderPactTests(SqlServerFixture sqlServer, ITestOutputHelper output)
    : IClassFixture<SqlServerFixture>
{
    /// <summary>Matches the tenant the consumer pact relays. Any non-blank value would do.</summary>
    private const string TenantId = "contoso";

    /// <summary>
    /// Kiểm tra: dựng Products API thật (SQL Server thật qua `SqlServerFixture`) rồi cho
    /// `PactVerifier` gửi lại chính xác các request đã ghi trong `pacts/bff-products.json` — phản
    /// hồi thật phải khớp đúng những gì BFF đã khai là cần.
    /// Lý do: đây là nửa "bên phát tự kiểm mình" của spec 011 — đổi tên/bớt 1 trường
    /// `ProductResponse` mà BFF đang đọc phải làm ĐÚNG build của products đỏ, không phải build của
    /// BFF.
    /// Từng ĐỎ trên mọi interaction (401) từ 2026-09-03 (spec 015 gắn `.RequireAuthorization` thẳng
    /// vào endpoint, `FallbackPolicy = null` cũ của host hết tác dụng) tới 2026-09-22, khi
    /// `PactProviderHost` chuyển sang `UseTestJwtBearer()` và test này gắn token qua
    /// `WithCustomHeader` — xem QA_Debt mục 011 (đã đánh dấu đã vá).
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T012/T013, US1 (FR-001, FR-003, FR-005).
    /// </summary>
    [Fact]
    public void CatalogResponses_SatisfyTheBffsRecordedExpectations()
    {
        using var provider = new PactProviderHost(sqlServer.ConnectionString, TenantId, ApplyStateAsync);

        // Forces the host to build and start before the verifier is pointed at it. The factory
        // creates its host lazily, so without this BaseUri is still the placeholder.
        using var _ = provider.CreateDefaultClient();

        using var verifier = new PactVerifier(
            "products",
            new PactVerifierConfig { Outputters = [new PactTestOutput(output)] });

        // Kiểm chứng nằm ở .Verify() bên dưới: không có Assert.* nào — PactVerifier tự ném
        // PactVerificationFailedException nếu response thật lệch khỏi file pact, xUnit coi đó là
        // test đỏ. Xanh khi mọi interaction khớp, đỏ (kèm log nêu rõ trường/status sai) khi lệch.
        verifier
            .WithHttpEndpoint(provider.BaseUri)
            .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-products.json")))
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
    /// Puts the catalog into the state an interaction was recorded under. Each state states the
    /// whole catalog it needs rather than adding to whatever the last one left, so interactions
    /// stay independent of the order the verifier replays them in.
    /// </summary>
    private static async Task ApplyStateAsync(ProviderState state, IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<ProductsDbContext>();
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
            case "the catalog contains at least one product":
                dbContext.Products.RemoveRange(dbContext.Products);
                dbContext.Products.Add(new Product
                {
                    Id = new Guid("9f8d6b1e-0001-4000-8000-000000000001"),
                    Name = "Field Notes Notebook",
                    Price = 12.50m,
                });
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
