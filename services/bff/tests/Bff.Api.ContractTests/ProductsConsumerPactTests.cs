using System.Net;
using Bff.Api.DownstreamClients;
using PactNet.Matchers;

namespace Bff.Api.ContractTests;

/// <summary>
/// What the BFF relies on from the products service, stated as a Pact document
/// (<c>pacts/bff-products.json</c>) for the products service's own build to verify
/// (011-consumer-contract-tests FR-001, FR-002).
/// </summary>
/// <remarks>
/// The expectations are driven through <see cref="ProductsApiClient"/> itself rather than through a
/// hand-written request. That is the point of a consumer-driven pact: what gets recorded is what
/// the real client sends and what it can actually deserialise, so a field the BFF never reads
/// cannot leak into the contract and pin the producer down for no reason (FR-007).
/// </remarks>
public class ProductsConsumerPactTests
{
    private const string Provider = "products";

    /// <summary>
    /// Kiểm tra: khai báo kỳ vọng của BFF ở `GET /products` (qua `ProductsApiClient` thật) — ghi
    /// thành file `pacts/bff-products.json` cho service products tự verify trong build của nó.
    /// Lý do: đây là "hợp đồng" mà spec 011 dựa vào — nếu khai sai thứ BFF thật sự cần, cả bộ máy
    /// consumer-driven contract sẽ kiểm chứng nhầm thứ.
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T009, US1 (FR-001, FR-002).
    /// </summary>
    [Fact]
    public async Task GetProducts_DependsOnIdNameAndPrice()
    {
        var pact = BffPact.For(Provider);

        pact
            .UponReceiving("a request for a page of the catalog")
                .Given("the catalog contains at least one product")
                .WithRequest(HttpMethod.Get, "/products")
                .WithQuery("page", "1")
                .WithQuery("pageSize", "20")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("Authorization", BffPact.AuthorizationHeader)
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                // items uses MinType(_, 1) rather than a fixed array: the BFF reads however many
                // products the page contains, so pinning the count would make an unrelated catalog
                // change a contract break. page/pageSize/totalCount are the additive pagination
                // envelope (specs/023-audit-n1-unbounded-pagination FR-007 — Items unchanged).
                .WithJsonBody(new
                {
                    items = Match.MinType(
                        new
                        {
                            id = Match.Type("9f8d6b1e-0001-4000-8000-000000000001"),
                            name = Match.Type("Field Notes Notebook"),
                            price = Match.Number(12.50m),
                        },
                        1),
                    page = Match.Type(1),
                    pageSize = Match.Type(20),
                    totalCount = Match.Type(1),
                });

        await pact.VerifyAsync(async context =>
        {
            using var httpClient = BffPact.CreateRelayingClient(context.MockServerUri);
            var client = new ProductsApiClient(httpClient);

            var page = await client.GetProductsAsync(1, 20, CancellationToken.None);

            // Asserting on the deserialised resource, not on raw JSON: it is what proves the shape
            // recorded above is one ProductResource can actually be built from.
            // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
            // nhiều hơn.
            var product = Assert.Single(page.Items);
            // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau.
            Assert.NotEqual(Guid.Empty, product.Id);
            // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng. Tên sản phẩm không rỗng.
            Assert.False(string.IsNullOrWhiteSpace(product.Name));
            // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Cả 4 Assert cùng
            // chứng minh 1 điều: JSON khai ở pact thật sự dựng lại được thành ProductResource mà
            // ProductsApiClient đọc — không chỉ "đúng cú pháp JSON".
            Assert.Equal(12.50m, product.Price);
        });
    }
}
