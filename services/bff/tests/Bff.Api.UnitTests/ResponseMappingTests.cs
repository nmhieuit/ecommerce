using Bff.Api.DownstreamClients;
using Bff.Api.Features.Baskets;
using Bff.Api.Features.Orders;
using Bff.Api.Features.Parties;
using Bff.Api.Features.Products;

namespace Bff.Api.UnitTests;

/// <summary>
/// The response-shaping functions, exercised without HTTP.
/// </summary>
/// <remarks>
/// Shaping is the only logic the BFF owns — everything else is proxying (spec FR-005) — so it is
/// the one thing worth asserting directly rather than only through an integration test. These also
/// pin the property-by-property mapping: an integration test comparing whole objects would pass if
/// two same-typed fields were transposed, and <c>Order</c> carries exactly such a pair once
/// <c>Total</c> and a future amount field coexist.
/// </remarks>
public class ResponseMappingTests
{
    /// <summary>
    /// Kiểm tra: hàm `ProductsEndpoints.ToSummary` map đúng cả 3 trường (`Id`/`Name`/`Price`) từ
    /// `ProductResource` (downstream) sang `ProductSummary` (response BFF), không qua HTTP.
    /// Lý do phải test: shaping là nghiệp vụ DUY NHẤT BFF sở hữu (mọi thứ khác là proxy thuần theo
    /// FR-005) — test đơn vị trực tiếp trên hàm mapping bắt được lỗi field-by-field (vd. 2 trường
    /// cùng kiểu bị đảo chỗ) mà 1 test tích hợp so sánh nguyên object có thể bỏ lọt.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T061, US1.
    /// </summary>
    [Fact]
    public void ProductSummary_CarriesEveryFieldFromTheDownstreamProduct()
    {
        var product = new ProductResource(Guid.NewGuid(), "Ceramic mug", 12.50m);

        var summary = ProductsEndpoints.ToSummary(product);

        Assert.Equal(product.Id, summary.Id);
        Assert.Equal(product.Name, summary.Name);
        Assert.Equal(product.Price, summary.Price);
    }

    /// <summary>
    /// Kiểm tra: `BasketsEndpoints.ToItem` nối tên sản phẩm (tra từ dictionary catalog) vào dòng
    /// giỏ hàng, đồng thời giữ nguyên mọi trường khác (`ProductId`/`Quantity`/`UnitPrice`/`LineTotal`).
    /// Lý do phải test: dòng giỏ hàng người mua thấy = dòng gốc từ baskets service + tên sản phẩm nối
    /// từ catalog. `LineTotal` đặc biệt phải được TRUYỀN NGUYÊN, không được tính lại ở BFF — vì phép
    /// toán tiền tệ thuộc về baskets service (spec 004 plan.md), BFF chỉ shaping.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T061, US1.
    /// </summary>
    [Fact]
    public void BasketItem_JoinsTheProductName_AndPassesEveryOtherFieldThrough()
    {
        var productId = Guid.Parse("9f8d6b1e-0001-4000-8000-000000000001");
        var line = new BasketLineItemResource(productId, Quantity: 2, UnitPrice: 12.50m, LineTotal: 25.00m);

        var item = BasketsEndpoints.ToItem(line, new Dictionary<Guid, string>
        {
            [productId] = "Field Notes Notebook",
        });

        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Field Notes Notebook", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(12.50m, item.UnitPrice);
        Assert.Equal(25.00m, item.LineTotal);
    }

    /// <summary>
    /// Kiểm tra: khi dictionary catalog không có tên cho sản phẩm của dòng giỏ hàng (sản phẩm đã bị
    /// xoá khỏi catalog), dòng đó vẫn xuất hiện trong response, `LineTotal` không đổi, và `Name` vẫn
    /// có giá trị (không rỗng/trắng).
    /// Lý do phải test: 1 dòng có sản phẩm đã rời khỏi catalog vẫn phải giữ chỗ, không được biến mất
    /// — người mua đã chọn nó và đang bị tính tiền cho nó, xoá dòng sẽ làm sai lệch tổng tiền họ sắp
    /// trả.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T061, US1.
    /// </summary>
    [Fact]
    public void BasketItem_SurvivesAProductMissingFromTheCatalog()
    {
        var line = new BasketLineItemResource(Guid.NewGuid(), Quantity: 1, UnitPrice: 9.99m, LineTotal: 9.99m);

        var item = BasketsEndpoints.ToItem(line, new Dictionary<Guid, string>());

        Assert.Equal(9.99m, item.LineTotal);
        Assert.False(string.IsNullOrWhiteSpace(item.Name));
    }

    /// <summary>
    /// Kiểm tra: `OrdersEndpoints.ToResponse` map đúng cả 3 trường (`Id`/`PlacedAtUtc`/`Total`) từ
    /// `OrderResource` (downstream) sang response BFF.
    /// Lý do phải test: cùng lý do với test mapping sản phẩm — shaping là nghiệp vụ duy nhất BFF sở
    /// hữu, nên cần khẳng định trực tiếp từng trường, không chỉ qua test tích hợp.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T061, US1.
    /// </summary>
    [Fact]
    public void OrderResponse_CarriesEveryFieldFromTheDownstreamOrder()
    {
        var order = new OrderResource(
            Guid.NewGuid(),
            new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            47.49m);

        var response = OrdersEndpoints.ToResponse(order);

        Assert.Equal(order.Id, response.Id);
        Assert.Equal(order.PlacedAtUtc, response.PlacedAtUtc);
        Assert.Equal(order.Total, response.Total);
    }

    /// <summary>
    /// Kiểm tra: sau khi qua `OrdersEndpoints.ToResponse`, `PlacedAtUtc.Kind` vẫn là
    /// `DateTimeKind.Utc`, không bị mất đi trong lúc shaping.
    /// Lý do phải test: thời điểm phải sống sót qua bước shaping đúng là 1 UTC instant. Nếu
    /// `DateTimeKind.Utc` bị đánh rơi, SPA sẽ hiển thị giờ đặt hàng sai múi giờ mà không có gì báo
    /// hiệu điều đó đã xảy ra.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T061, US1.
    /// </summary>
    [Fact]
    public void OrderResponse_PreservesTheUtcKindOfThePlacedTimestamp()
    {
        var placedAtUtc = new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc);

        var response = OrdersEndpoints.ToResponse(new OrderResource(Guid.NewGuid(), placedAtUtc, 1m));

        Assert.Equal(DateTimeKind.Utc, response.PlacedAtUtc.Kind);
    }

    /// <summary>
    /// Kiểm tra: `PartiesEndpoints.ToResponse` map đúng cả 2 trường (`Id`/`DisplayName`) từ
    /// `PartyResource` (downstream) sang response BFF.
    /// Lý do phải test: hàm shaping thứ 4 (cuối cùng trong 4 route) cần cùng mức khẳng định trực tiếp
    /// như 3 hàm shaping còn lại, không bỏ sót route nào.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T061, US1.
    /// </summary>
    [Fact]
    public void PartyResponse_CarriesEveryFieldFromTheDownstreamParty()
    {
        var party = new PartyResource(Guid.NewGuid(), "Ada Lovelace");

        var response = PartiesEndpoints.ToResponse(party);

        Assert.Equal(party.Id, response.Id);
        Assert.Equal(party.DisplayName, response.DisplayName);
    }

    /// <summary>
    /// Kiểm tra: với 3 giá trị tiền tệ khác nhau (kể cả số có nhiều số 9 và số thập phân nhỏ), giá
    /// sau khi qua `ProductsEndpoints.ToSummary` giữ nguyên CHÍNH XÁC — kể cả số 0 thừa ở cuối, so
    /// khớp bằng chuỗi chứ không chỉ bằng giá trị số.
    /// Lý do phải test: tiền không được làm tròn khi đi qua shaping. 1 bước shaping vô tình thu hẹp
    /// `decimal` thành `double` sẽ vẫn pass mọi phép so sánh bằng nhau ở các test phía trên (vì đó là
    /// số tròn), nhưng âm thầm làm sai lệch giá dạng như test này — dùng số có nhiều chữ số thập phân
    /// mới bắt được lỗi đó.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T061, US1.
    /// </summary>
    [Theory]
    [InlineData("0.01")]
    [InlineData("12.50")]
    [InlineData("999999999999.99")]
    public void ProductSummary_PreservesPricePrecisionExactly(string price)
    {
        var exact = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        var summary = ProductsEndpoints.ToSummary(new ProductResource(Guid.NewGuid(), "Anything", exact));

        Assert.Equal(exact, summary.Price);
        // Trailing zeros are part of a decimal's representation; a round trip through double or
        // float would not preserve them.
        Assert.Equal(price, summary.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
