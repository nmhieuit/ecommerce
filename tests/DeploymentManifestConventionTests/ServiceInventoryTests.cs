namespace DeploymentManifestConventionTests;

/// <summary>
/// Spec SC-001: 100% service đang chạy khai báo đủ cả 2 probe. Khẳng định đó chỉ có ý nghĩa nếu
/// inventory mà role dùng để triển khai thật sự liệt kê ĐỦ mọi service — 1 lượt quét âm thầm chỉ
/// xét dưới 7 service sẽ báo "tất cả pass" trong khi bỏ sót hẳn 1 service — cùng cái bẫy mà
/// <c>ContainerConventionTests.TheScan_Examined_EveryService</c> đã phòng cho Dockerfile.
/// </summary>
public class ServiceInventoryTests
{
    /// <summary>Mọi service có Deployment, do đó bắt buộc phải có mặt trong inventory.</summary>
    private static readonly string[] ExpectedServices =
        ["parties", "products", "baskets", "orders", "identity", "gateway", "bff"];

    /// <summary>
    /// Kiểm tra: `inventories/services.yml` liệt kê ĐÚNG 7 service kỳ vọng — không thiếu, không
    /// thừa.
    /// Lý do: SC-001 nói "100% service" — con số 100% chỉ đáng tin nếu mẫu số (inventory) đúng; 1
    /// service bị bỏ sót khỏi inventory sẽ không bao giờ được render/kiểm tra probe, nhưng vẫn
    /// "trông như" đạt SC-001 vì không ai đếm thiếu nó.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — SC-001.
    /// </summary>
    [Fact]
    public void Inventory_ListsExactlyTheSevenExpectedServices()
    {
        var inventory = ServiceInventory.Load(ProbeTemplateRenderer.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi 2 tập hợp (đã sắp xếp) giống hệt nhau, đỏ khi
        // khác — inventory phải khớp đúng 7 tên service, không thiếu không thừa.
        Assert.Equal(ExpectedServices.OrderBy(s => s), inventory.Keys.OrderBy(s => s));
    }

    /// <summary>
    /// Kiểm tra: mỗi service trong inventory đều khai `container_port` dương và có khai
    /// `depends_on_database` (đúng nhóm `db_backed` hoặc `stateless`).
    /// Lý do: `depends_on_database` không có giá trị mặc định ngầm định
    /// (`contracts/service-deployment-vars.md` rule 3) — `ServiceInventory.Load` tự ném lỗi nếu
    /// thiếu khoá này, nên chạm tới được dòng assert nghĩa là khoá đã tồn tại; assertion ở đây ghi
    /// lại rõ 2 service (`gateway`/`bff`) là ngoại lệ không phụ thuộc database, còn lại 5 service
    /// phải phụ thuộc.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-004, contracts/
    /// service-deployment-vars.md.
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaresContainerPortAndDependsOnDatabase(string serviceName)
    {
        var inventory = ServiceInventory.Load(ProbeTemplateRenderer.LocateRepositoryRoot());

        // Assert.True(điều kiện, thông báo): xanh khi service có mặt trong inventory; đỏ kèm tên
        // service khi thiếu.
        Assert.True(inventory.ContainsKey(serviceName), $"'{serviceName}' is missing from services.yml.");
        var entry = inventory[serviceName];

        // Assert.True(điều kiện, thông báo): xanh khi cổng > 0; đỏ kèm tên service khi không hợp lệ.
        Assert.True(entry.ContainerPort > 0, $"'{serviceName}' must declare a positive container_port.");
        if (serviceName is "gateway" or "bff")
        {
            // Assert.False(điều kiện): xanh khi gateway/bff KHÔNG phụ thuộc database (đúng vai trò
            // cổng/tổng hợp không trạng thái); đỏ nếu vô tình bị gắn nhầm.
            Assert.False(entry.DependsOnDatabase);
        }
        else
        {
            // Assert.True(điều kiện): xanh khi 5 service còn lại CÓ phụ thuộc database; đỏ nếu bị
            // gắn sai nhóm.
            Assert.True(entry.DependsOnDatabase);
        }
    }
}
