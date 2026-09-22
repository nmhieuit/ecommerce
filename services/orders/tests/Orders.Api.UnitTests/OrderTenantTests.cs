using Orders.Api.Data;

namespace Orders.Api.UnitTests;

/// <summary>
/// 006-e2e-order-demo FR-005: an order records the tenant it was placed for.
/// </summary>
/// <remarks>
/// <para>
/// The tenant is a constructor-level requirement rather than a property something sets afterwards,
/// for the same reason the total is computed rather than accepted: an order that exists without one
/// is a row nobody can attribute, and the window in which it could exist is the window in which it
/// could be saved.
/// </para>
/// <para>
/// Blank counts as absent here, exactly as it does in <c>TenantContext.RequireTenantId</c>. A tenant
/// whose identifier is an empty string is not a tenant, and accepting one would satisfy the schema
/// while defeating the point (constitution Principle V).
/// </para>
/// </remarks>
public class OrderTenantTests
{
    private const string Tenant = "contoso";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: `Order.PlaceFrom(lines, now, "contoso")` cho ra đơn có `TenantId` bằng đúng
    /// "contoso".
    /// Lý do: FR-005: đơn hàng mang theo tenant đã phân giải cho request đặt đơn, lưu cùng đơn chứ
    /// không suy ra lúc đọc. Tenant là yêu cầu ở mức constructor (không phải thuộc tính gán sau) —
    /// đơn tồn tại mà không có tenant là 1 dòng không ai quy thuộc được.
    /// Task nguồn: spec 006 (demo đặt hàng end-to-end) — T020, US2 (FR-005).
    /// </summary>
    [Fact]
    public void PlaceFrom_RecordsTheTenantItWasPlacedFor()
    {
        var order = Order.PlaceFrom([new OrderLine(Notebook, 1, 12.50m)], Now, Tenant);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi mất hoặc bị thay.
        Assert.Equal(Tenant, order.TenantId);
    }

    /// <summary>
    /// Kiểm tra: tenant null, rỗng, khoảng trắng hoặc tab đều làm `PlaceFrom` ném
    /// `ArgumentException`.
    /// Lý do: tenant rỗng không phải tenant — giống hệt `TenantContext.RequireTenantId`. Chấp nhận
    /// nó sẽ thoả schema nhưng phá đúng mục đích của tính năng (Constitution Principle V).
    /// Task nguồn: spec 006 (demo đặt hàng end-to-end) — T020, US2 (FR-005, FR-006).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void PlaceFrom_Rejects_AnAbsentOrBlankTenant(string? tenantId)
    {
        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác. Đỏ khi không ném gì (cho phép đơn không thuộc tenant nào)
        // hoặc ném loại khác.
        Assert.Throws<ArgumentException>(
            () => Order.PlaceFrom([new OrderLine(Notebook, 1, 12.50m)], Now, tenantId!));
    }

    /// <summary>
    /// Kiểm tra: danh sách dòng rỗng vẫn bị từ chối dù đã có tenant hợp lệ.
    /// Lý do: việc thêm kiểm tra tenant không được làm mất các quy tắc đã có (FR-008 của spec 004):
    /// 1 request sai theo 2 cách vẫn bị từ chối, bất kể kiểm tra nào chạy trước.
    /// Task nguồn: spec 006 (demo đặt hàng end-to-end) — T020, US2 (FR-005; giữ quy tắc đơn rỗng
    /// của spec 004 FR-008).
    /// </summary>
    [Fact]
    public void PlaceFrom_StillRejects_AnEmptyLineSet_EvenWithATenant()
    {
        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác. Đỏ khi có tenant thì được phép đơn rỗng.
        Assert.Throws<ArgumentException>(() => Order.PlaceFrom([], Now, Tenant));
    }

    private static DateTime Now => new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
}
