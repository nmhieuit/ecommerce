namespace Tenancy.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa data-model.md — CallerContext. The caller's subject decides *whose*
/// rows a request may touch, the same way the tenant decides *which store* it may reach, so it gets
/// the same two states and the same refusal to invent a third.
/// </summary>
/// <remarks>
/// Deliberately a mirror of <see cref="TenantContextTests"/>. Two concepts that behave identically
/// should be tested identically — a reader who knows one already knows the other, and a divergence
/// between them shows up as a diff rather than as a subtlety nobody notices.
/// </remarks>
public class CallerContextTests
{
    /// <summary>
    /// Kiểm tra: khi subject đã được gán, `RequireSubjectId()` trả về đúng giá trị đó.
    /// Lý do: nhánh "Resolved" đối chứng cho các test ném exception bên dưới; thiếu nó thì guard có
    /// thể luôn ném mà vẫn qua các test khác.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T011, US2 (FR-006).
    /// </summary>
    [Fact]
    public void RequireSubjectId_ReturnsTheResolvedSubject_WhenOneHasBeenSet()
    {
        var context = new CallerContext { SubjectId = "phase1-stub-user" };

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Trả lại đúng subject đã
        // gán; đỏ khi khác hoặc ném lỗi.
        Assert.Equal("phase1-stub-user", context.RequireSubjectId());
    }

    /// <summary>
    /// Kiểm tra: `CallerContext` mới tạo có `SubjectId` là null.
    /// Lý do: trạng thái khởi đầu phải là Unresolved; nếu có sẵn 1 người mua mặc định thì mọi
    /// request sẽ dùng chung 1 giỏ.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T011, US2 (FR-006).
    /// </summary>
    [Fact]
    public void CallerContext_IsUnresolved_BeforeAnythingSetsIt()
    {
        var context = new CallerContext();

        // Assert.Null(giá trị): xanh khi là null, đỏ khi có giá trị.
        Assert.Null(context.SubjectId);
    }

    /// <summary>
    /// Kiểm tra: khi chưa có subject, `RequireSubjectId()` ném `MissingCallerContextException`.
    /// Lý do: giỏ và đơn hàng là dữ liệu của từng người mua — không xác định được người gọi thì
    /// phải thất bại to tiếng, không được phát giỏ của ai đó (Principle V mở rộng cho caller).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T011, US2 (FR-006).
    /// </summary>
    [Fact]
    public void RequireSubjectId_Throws_WhenNoSubjectHasBeenResolved()
    {
        var context = new CallerContext();

        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác.
        Assert.Throws<MissingCallerContextException>(() => context.RequireSubjectId());
    }

    /// <summary>
    /// Kiểm tra: subject rỗng, khoảng trắng hoặc tab vẫn khiến `RequireSubjectId()` ném exception.
    /// Lý do: subject rỗng là Unresolved, không phải "người mua có tên rỗng"; nếu không, mọi
    /// request mang header `X-Subject-Id` rỗng sẽ dùng chung 1 giỏ.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T011, US2 (FR-006).
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RequireSubjectId_Throws_WhenTheResolvedSubjectIsBlank(string blank)
    {
        var context = new CallerContext { SubjectId = blank };

        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác. Cả 3 giá trị trắng phải bị coi như chưa có danh tính.
        Assert.Throws<MissingCallerContextException>(() => context.RequireSubjectId());
    }
}
