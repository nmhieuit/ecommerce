namespace Tenancy.UnitTests;

/// <summary>
/// Spec FR-004/FR-005 and Test Scenario 2: a tenant is either resolved or it is an error to
/// proceed. data-model.md gives <c>TenantContext</c> exactly two states — Resolved and Unresolved —
/// and this suite is what keeps a third "resolved to a default" state from quietly appearing.
/// </summary>
public class TenantContextTests
{
    /// <summary>
    /// Kiểm tra: khi `TenantId` đã được gán ("acme"), `RequireTenantId()` trả về đúng giá trị đó.
    /// Lý do phải test: đây là nhánh "Resolved" đối chứng cho các test ném exception bên dưới — thiếu
    /// nó thì không có gì đảm bảo guard không bị lỗi ngược (luôn ném) mà vẫn "vô tình" pass các test
    /// chỉ kiểm tra trường hợp thiếu tenant.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T005, US2.
    /// </summary>
    [Fact]
    public void RequireTenantId_ReturnsTheResolvedTenant_WhenOneHasBeenSet()
    {
        var context = new TenantContext { TenantId = "acme" };

        Assert.Equal("acme", context.RequireTenantId());
    }

    /// <summary>
    /// Kiểm tra: `TenantContext` vừa tạo mới có `TenantId` là null.
    /// Lý do phải test: trạng thái khởi đầu phải là "Unresolved". Nếu có giá trị mặc định nào đó được
    /// điền sẵn, mọi service sẽ âm thầm chạy với 1 tenant không ai xác định — đúng điều FR-004 cấm.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T005, US2.
    /// </summary>
    [Fact]
    public void TenantContext_IsUnresolved_BeforeAnythingSetsIt()
    {
        var context = new TenantContext();

        Assert.Null(context.TenantId);
    }

    /// <summary>
    /// Kiểm tra: khi chưa có tenant nào được phân giải, `RequireTenantId()` ném
    /// `MissingTenantContextException`.
    /// Lý do phải test: đây là cơ chế cốt lõi của FR-004/FR-005 — truy cập persistence khi chưa biết
    /// tenant phải thất bại to tiếng, không được lặng lẽ chạy tiếp với tenant mặc định.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T005, US2.
    /// </summary>
    [Fact]
    public void RequireTenantId_Throws_WhenNoTenantHasBeenResolved()
    {
        var context = new TenantContext();

        Assert.Throws<MissingTenantContextException>(() => context.RequireTenantId());
    }

    /// <summary>
    /// Kiểm tra: khi `TenantId` là chuỗi rỗng, khoảng trắng hoặc tab, `RequireTenantId()` vẫn ném
    /// `MissingTenantContextException`.
    /// Lý do phải test: data-model.md khẳng định không có trạng thái "rỗng nhưng có mặt" — tenant rỗng
    /// là Unresolved, không phải 1 tenant có tên rỗng. Thiếu test này, 1 header `X-Tenant-Id` rỗng sẽ
    /// lọt qua guard và chạm tới persistence.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T005, US2.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RequireTenantId_Throws_WhenTheResolvedTenantIsBlank(string blank)
    {
        var context = new TenantContext { TenantId = blank };

        Assert.Throws<MissingTenantContextException>(() => context.RequireTenantId());
    }
}
