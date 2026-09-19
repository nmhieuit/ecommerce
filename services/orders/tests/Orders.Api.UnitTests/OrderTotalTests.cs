using Orders.Api.Data;

namespace Orders.Api.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa research.md Decision 8: "the orders service computes the total from the
/// lines it is sent" — never accepts one. That is what keeps every monetary computation inside a
/// domain service and the aggregation layer above free of arithmetic.
/// </summary>
public class OrderTotalTests
{
    /// <summary>
    /// Incidental to every assertion in this class - the tenant is required to build an order
    /// (006 FR-005) but none of these tests are about it. <see cref="OrderTenantTests"/> is.
    /// </summary>
    private const string Tenant = "contoso";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    /// <summary>
    /// Kiểm tra: đơn 1 dòng có tổng bằng số lượng × đơn giá của dòng đó.
    /// Lý do phải test: research.md Decision 8: Orders tự tính tổng từ các dòng được gửi tới, không
    /// nhận tổng từ caller — mọi phép tính tiền nằm trong service nghiệp vụ, BFF không làm số học.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T049, US3 (research.md Decision 8).
    /// </summary>
    [Fact]
    public void PlaceFrom_MultipliesQuantityByUnitPrice()
    {
        var order = Order.PlaceFrom([new OrderLine(Notebook, 2, 12.50m)], placedAtUtc: Now, tenantId: Tenant);

        Assert.Equal(25.00m, order.Total);
    }

    /// <summary>
    /// Kiểm tra: đơn nhiều dòng có tổng bằng tổng thành tiền của các dòng (2 Notebook + 1 Apron =
    /// $59.25).
    /// Lý do phải test: con số mà quickstart.md Scenario 5 trích; bảo đảm màn hình xác nhận hiển
    /// thị đúng tổng đơn (FR-009).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T049, US3 (FR-009, FR-022).
    /// </summary>
    [Fact]
    public void PlaceFrom_SumsEveryLine()
    {
        var order = Order.PlaceFrom(
            [new OrderLine(Notebook, 2, 12.50m), new OrderLine(Apron, 1, 34.25m)],
            placedAtUtc: Now,
            tenantId: Tenant);

        // The figure quickstart.md Scenario 5 quotes: two notebooks and an apron.
        Assert.Equal(59.25m, order.Total);
    }

    /// <summary>
    /// Kiểm tra: đơn được tạo có mã định danh (Id) và thời điểm đặt hàng.
    /// Lý do phải test: mã định danh chính là "mã tham chiếu" người mua đọc trên màn hình xác nhận
    /// và dùng để tra lại đơn (FR-009, SC-005).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T049, US3 (FR-009, SC-005).
    /// </summary>
    [Fact]
    public void PlaceFrom_RecordsWhenTheOrderWasPlaced_AndGivesItAnIdentifier()
    {
        var placedAt = new DateTime(2026, 8, 16, 9, 30, 0, DateTimeKind.Utc);

        var order = Order.PlaceFrom([new OrderLine(Notebook, 1, 12.50m)], placedAt, Tenant);

        Assert.Equal(placedAt, order.PlacedAtUtc);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    /// <summary>
    /// Kiểm tra: tạo đơn với danh sách dòng rỗng bị từ chối.
    /// Lý do phải test: quy tắc giỏ rỗng (FR-008) được bảo vệ ở 3 tầng; đây là tầng trong cùng — 1
    /// đơn không có dòng nào sẽ là đơn cho không có gì, tổng bằng 0.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T049, US3 (FR-008).
    /// </summary>
    [Fact]
    public void PlaceFrom_Rejects_AnEmptyLineSet()
    {
        Assert.Throws<ArgumentException>(() => Order.PlaceFrom([], Now, Tenant));
    }

    /// <summary>
    /// Kiểm tra: dòng có số lượng 0 hoặc âm bị từ chối khi tạo đơn.
    /// Lý do phải test: chặn dòng vô nghĩa hoặc dòng làm giảm tổng đơn ngay ở domain.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T049, US3 (FR-022).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PlaceFrom_Rejects_ALineWithANonPositiveQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Order.PlaceFrom([new OrderLine(Notebook, quantity, 12.50m)], Now, Tenant));
    }

    /// <summary>
    /// Kiểm tra: dòng có đơn giá âm bị từ chối khi tạo đơn.
    /// Lý do phải test: đơn giá âm sẽ hạ tổng đơn dưới giá trị thật của hàng hoá.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T049, US3 (FR-022).
    /// </summary>
    [Fact]
    public void PlaceFrom_Rejects_ALineWithANegativePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Order.PlaceFrom([new OrderLine(Notebook, 1, -0.01m)], Now, Tenant));
    }

    /// <summary>
    /// Kiểm tra: các số tiền mà số thực dấu phẩy động sẽ làm tròn sai vẫn cho tổng đơn chính xác.
    /// Lý do phải test: tổng đơn lệch 1 cent sẽ hiện ngay trên màn hình xác nhận của người mua —
    /// dùng decimal, không dùng float.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T049, US3 (FR-009, FR-024).
    /// </summary>
    [Fact]
    public void PlaceFrom_IsExact_ForAmountsThatFloatingPointWouldRound()
    {
        var order = Order.PlaceFrom(
            [new OrderLine(Notebook, 1, 0.10m), new OrderLine(Apron, 1, 0.20m)],
            Now,
            Tenant);

        Assert.Equal(0.30m, order.Total);
    }

    private static DateTime Now => new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
}
