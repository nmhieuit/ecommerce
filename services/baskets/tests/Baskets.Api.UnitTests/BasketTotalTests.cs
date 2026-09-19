using Baskets.Api.Data;

namespace Baskets.Api.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa data-model.md: the basket total is the sum of quantity × captured unit
/// price, "computed by the baskets service from its own rows and never stored".
/// </summary>
/// <remarks>
/// This is also what keeps the BFF free of arithmetic (004 plan.md, post-design re-check): the
/// total exists here so that no aggregation layer has to add anything up.
/// </remarks>
public class BasketTotalTests
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid PourOver = new("9f8d6b1e-0001-4000-8000-000000000002");

    /// <summary>
    /// Kiểm tra: giỏ rỗng có tổng tiền bằng 0.
    /// Lý do phải test: trạng thái khởi đầu và trạng thái sau khi thanh toán (FR-010) phải có tổng
    /// xác định, không phải null hay lỗi.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T033, US2 (FR-004).
    /// </summary>
    [Fact]
    public void Total_IsZero_ForAnEmptyBasket()
    {
        Assert.Equal(0m, Basket.ForCustomer("phase1-stub-user").Total);
    }

    /// <summary>
    /// Kiểm tra: tổng của giỏ 1 dòng bằng số lượng × đơn giá đã chụp.
    /// Lý do phải test: công thức nền tảng của tổng giỏ theo data-model.md ("tính, không lưu"); mọi
    /// tổng khác đều dựa trên nó.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T033, US2 (FR-004).
    /// </summary>
    [Fact]
    public void Total_MultipliesQuantityByUnitPrice()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 2, unitPrice: 12.50m);

        Assert.Equal(25.00m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: giỏ nhiều dòng có tổng bằng tổng thành tiền của từng dòng.
    /// Lý do phải test: chứng minh tổng cộng dồn đủ mọi dòng, không bỏ sót dòng nào.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T033, US2 (FR-004).
    /// </summary>
    [Fact]
    public void Total_SumsEveryLine()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 2, unitPrice: 12.50m);
        basket.AddItem(PourOver, quantity: 1, unitPrice: 48.00m);

        Assert.Equal(73.00m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: 2 cuốn Notebook $12.50 + 1 Linen Apron $34.25 cho tổng $59.25.
    /// Lý do phải test: quickstart.md Scenario 2 và 5 đều trích con số này; ghim ở test đơn vị để
    /// mọi thay đổi cách tính làm hỏng test ngay, thay vì hỏng giữa lúc chạy thủ công.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T033, US2 (quickstart Scenario 2, 5).
    /// </summary>
    [Fact]
    public void Total_MatchesTheWalkthroughFigure()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        basket.AddItem(new Guid("9f8d6b1e-0001-4000-8000-000000000003"), quantity: 1, unitPrice: 34.25m);

        Assert.Equal(59.25m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: các số tiền mà số thực dấu phẩy động sẽ làm tròn sai (0.1 + 0.2) vẫn cho tổng
    /// chính xác.
    /// Lý do phải test: dùng số thập phân (decimal) chứ không phải float — cùng phép cộng bằng
    /// double ra 0.30000000000000004, tổng giỏ lệch 1 cent là lỗi người mua sẽ nhìn thấy.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T033, US2 (FR-004, FR-024).
    /// </summary>
    [Fact]
    public void Total_IsExact_ForAmountsThatFloatingPointWouldRound()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 1, unitPrice: 0.10m);
        basket.AddItem(PourOver, quantity: 1, unitPrice: 0.20m);

        Assert.Equal(0.30m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: xoá giỏ (Clear) làm mất mọi dòng và đưa tổng về 0.
    /// Lý do phải test: nền tảng cho checkout: sau khi đặt đơn thành công giỏ phải rỗng (FR-010) và
    /// tổng phải phản ánh đúng trạng thái đó.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T033, US3 (FR-010).
    /// </summary>
    [Fact]
    public void Clear_EmptiesTheBasket_AndZeroesTheTotal()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");
        basket.AddItem(Notebook, quantity: 3, unitPrice: 12.50m);

        basket.Clear();

        Assert.Empty(basket.LineItems);
        Assert.Equal(0m, basket.Total);
    }
}
