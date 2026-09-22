using Baskets.Api.Data;

namespace Baskets.Api.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa spec FR-005 and FR-021: "adding a product already present in the basket
/// MUST increase that product's quantity rather than create a second entry for the same product."
/// </summary>
/// <remarks>
/// Unit tests rather than integration ones because this is a domain rule, not a persistence
/// behaviour — the merge has to hold before anything is saved, and asserting it here means a
/// regression fails in milliseconds instead of behind a container start.
/// </remarks>
public class BasketLineMergeTests
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    /// <summary>
    /// Kiểm tra: thêm 1 sản phẩm chưa có trong giỏ thì giỏ có đúng 1 dòng mới với số lượng vừa
    /// thêm.
    /// Lý do: nhánh cơ bản của US2 kịch bản 1 (thêm sản phẩm vào giỏ, số lượng = 1); là điểm đối
    /// chứng cho các test gộp dòng bên dưới.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T032, US2 (FR-021).
    /// </summary>
    [Fact]
    public void AddItem_CreatesALine_WhenTheProductIsNotInTheBasketYet()
    {
        var basket = NewBasket();

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var line = Assert.Single(basket.LineItems);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(Notebook, line.ProductId);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(12.50m, line.UnitPrice);
    }

    /// <summary>
    /// Kiểm tra: thêm lại đúng sản phẩm đã có trong giỏ thì dòng cũ tăng số lượng, không sinh dòng
    /// thứ hai.
    /// Lý do: quy tắc gộp dòng của FR-005/FR-021 (US2 kịch bản 2): 1 sản phẩm chỉ chiếm tối đa 1
    /// dòng. Kiểm tra ở tầng domain để lỗi hiện ra trong vài mili-giây, không phải sau khi khởi
    /// động container.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T032, US2 (FR-005, FR-021).
    /// </summary>
    [Fact]
    public void AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket()
    {
        var basket = NewBasket();
        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn. Vẫn đúng 1 dòng.
        var line = Assert.Single(basket.LineItems);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Số lượng cộng dồn 2.
        Assert.Equal(2, line.Quantity);
    }

    /// <summary>
    /// Kiểm tra: thêm 2 sản phẩm khác nhau thì giỏ có 2 dòng riêng biệt.
    /// Lý do: chống việc quy tắc gộp dòng gộp nhầm cả những sản phẩm khác nhau vào cùng 1 dòng.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T032, US2 (FR-005).
    /// </summary>
    [Fact]
    public void AddItem_KeepsProductsApart_WhenDifferentProductsAreAdded()
    {
        var basket = NewBasket();

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        basket.AddItem(Apron, quantity: 2, unitPrice: 34.25m);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(2, basket.LineItems.Count);
        Assert.Equal(1, Assert.Single(basket.LineItems, line => line.ProductId == Notebook).Quantity);
        Assert.Equal(2, Assert.Single(basket.LineItems, line => line.ProductId == Apron).Quantity);
    }

    /// <summary>
    /// Kiểm tra: thêm cùng 1 sản phẩm nhiều lần liên tiếp cho ra đúng 1 dòng với số lượng bằng số
    /// lần thêm (5 lần → số lượng 5).
    /// Lý do: SC-003 yêu cầu ở 100% số lần thử, số lượng luôn bằng đúng số lần sản phẩm được thêm
    /// và mỗi sản phẩm hiện đúng 1 lần.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T032, US2 (SC-003).
    /// </summary>
    [Fact]
    public void AddItem_AccumulatesQuantities_AcrossManyAdditions()
    {
        var basket = NewBasket();

        for (var i = 0; i < 5; i++)
        {
            basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        }

        // Spec SC-003: the quantity equals the number of times the product was added, in 100% of
        // trials. One line, five of it.
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Single: 1 dòng; Equal:
        // số lượng 5.
        Assert.Equal(5, Assert.Single(basket.LineItems).Quantity);
    }

    /// <summary>
    /// Kiểm tra: thêm lại 1 sản phẩm với đơn giá mới hơn thì dòng vẫn giữ đơn giá đã chụp lúc thêm
    /// lần đầu.
    /// Lý do: giỏ chụp lại đơn giá tại thời điểm thêm (research.md Decision 7); tính lại giá cho
    /// giỏ người mua đã tự chọn xong là đúng điều việc chụp giá muốn tránh.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T032, US2 (research.md Decision 7).
    /// </summary>
    [Fact]
    public void AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged()
    {
        var basket = NewBasket();
        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        basket.AddItem(Notebook, quantity: 1, unitPrice: 99.99m);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var line = Assert.Single(basket.LineItems);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(12.50m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
    }

    /// <summary>
    /// Kiểm tra: thêm với số lượng nhỏ hơn 1 (0, số âm) bị từ chối.
    /// Lý do: data-model.md quy định số lượng của 1 dòng tối thiểu là 1, "dòng số lượng 0 không
    /// được tồn tại". Chặn ngay ở domain để quy tắc đúng dù caller nào quên validate.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T032, US2 (FR-020).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_Rejects_AQuantityBelowOne(int quantity)
    {
        var basket = NewBasket();

        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => basket.AddItem(Notebook, quantity, unitPrice: 12.50m));
    }

    /// <summary>
    /// Kiểm tra: thêm với đơn giá âm bị từ chối.
    /// Lý do: đơn giá âm sẽ làm tổng giỏ giảm đi — một cách hạ giá không cần quyền; domain phải tự
    /// bảo vệ dù lớp phía trên đã validate hay chưa.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T032, US2 (FR-020, FR-021).
    /// </summary>
    [Fact]
    public void AddItem_Rejects_ANegativeUnitPrice()
    {
        var basket = NewBasket();

        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác. Đỏ khi chấp nhận giá âm.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => basket.AddItem(Notebook, quantity: 1, unitPrice: -0.01m));
    }

    private static Basket NewBasket() => Basket.ForCustomer("phase1-stub-user");
}
