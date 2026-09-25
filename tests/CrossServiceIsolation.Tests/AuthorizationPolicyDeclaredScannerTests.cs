namespace CrossServiceIsolation.Tests;

/// <summary>
/// spec FR-001/FR-004 (US1/US2): mọi route HTTP đã map, xét theo từng route riêng lẻ, phải khai báo
/// tường minh quyết định phân quyền — Principle VI của hiến chương biến deny-by-default thành yêu cầu
/// bắt buộc, nên được khẳng định bằng cấu trúc mã ở đây thay vì trông chờ kỷ luật của từng PR, cùng lý
/// lẽ mà <see cref="AuthenticatedByDefaultScannerTests"/> đã áp dụng cho việc gắn xác thực.
/// </summary>
public class AuthorizationPolicyDeclaredScannerTests
{
    /// <summary>
    /// Kiểm tra: quét mã nguồn thật — mọi route đã map trong toàn bộ service phải khai
    /// `.RequireAuthorization(...)` hoặc `.AllowAnonymous()`, không được thiếu cả hai.
    /// Lý do: spec Test Scenario 1 (US2) — 1 route thiếu cả hai khai báo chính là lỗ hổng deny-by-default
    /// mà tính năng này lấp; hiến chương Principle VI coi đây là yêu cầu bắt buộc, không tuỳ chọn.
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — FR-001/FR-004, US2.
    /// </summary>
    [Fact]
    public void EveryMappedRoute_DeclaresAnAuthorizationDecision()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanEndpoints(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử nào
        // không đạt.
        Assert.All(result.Findings, finding => Assert.True(
            // Assert.True(điều kiện, thông báo): xanh khi route đã có khai báo phân quyền, đỏ (kèm
            // đúng tên service/route/file vi phạm) khi thiếu.
            finding.DeclaresAuthorizationDecision,
            $"{finding.Service}: {finding.RouteCallSite} in {finding.EndpointsFile} declares neither "
            + "RequireAuthorization(...) nor AllowAnonymous()."));
    }

    /// <summary>
    /// Kiểm tra: lượt quét ở test trên thật sự đã đi qua mọi service trong danh sách và tìm thấy ít
    /// nhất 1 route.
    /// Lý do: nếu scanner trỏ nhầm thư mục hoặc không khớp file nào sau khi đổi cấu trúc thư mục, nó sẽ
    /// không có gì để phản đối — trông y hệt 1 repo tuân thủ hoàn toàn dù thực ra chưa quét được gì
    /// (cùng lý lẽ với `AuthenticatedByDefaultScannerTests.Scan_ActuallyExaminesEveryService`).
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — tự bảo vệ cơ chế thực thi FR-004.
    /// </summary>
    [Fact]
    public void ScanEndpoints_ActuallyExaminesEveryAuthorizingService()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanEndpoints(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        // Assert.All + Assert.Contains(phần tử, tập hợp): với mỗi service kỳ vọng, phải tìm thấy ít
        // nhất 1 file đã quét thuộc đúng thư mục service đó; đỏ nếu thiếu 1 service nào.
        Assert.All(
            AuthorizationPolicyDeclaredScanner.AuthorizingServices,
            service => Assert.Contains(result.ScannedFiles, file => file.Contains(
                $"{Path.DirectorySeparatorChar}{service}{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)));

        // Assert.True(điều kiện, thông báo): xanh khi tìm được ít nhất 1 route đã map — nếu 0, scanner
        // coi như chưa quét được gì thật sự.
        Assert.True(result.Findings.Count > 0, "Expected at least one mapped route across every service.");
    }

    /// <summary>
    /// Kiểm tra: mọi `IConsumer&lt;T&gt;` (MassTransit message handler) trong toàn bộ service phải khai
    /// đúng nhãn nguồn tin cậy (`/// Trusted source: ...`).
    /// Lý do: research.md Decision 4 — lúc viết spec 015, repo chưa có `IConsumer&lt;T&gt;` nào nên test
    /// này từng xanh do rỗng ("vacuously"); đây chính là bộ khoá sẽ đỏ ngay khi message handler đầu tiên
    /// được thêm mà không khai nguồn tin cậy (contracts/message-handler-authorization-contract.md).
    /// Lưu ý: hiện ĐỎ — spec 024 (sau 015) thêm `IConsumer&lt;T&gt;` đầu tiên trong repo
    /// (`Orders.Api.IntegrationTests/Support/OrderPlacedVerificationConsumer.cs`, chỉ là helper phục vụ
    /// test outbox/inbox) mà chưa gắn nhãn — đúng kịch bản "Failure Modes" hợp đồng đã tự dự đoán trước.
    /// Xem QA_Debt mục 015 để biết chi tiết và điểm mơ hồ phạm vi (test helper vs. handler nghiệp vụ).
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — FR-002, contract message-handler-authorization.
    /// </summary>
    [Fact]
    public void EveryMessageConsumer_DeclaresATrustedSource()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanConsumers(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử nào
        // không đạt.
        Assert.All(result.Findings, finding => Assert.True(
            // Assert.True(điều kiện, thông báo): xanh khi consumer đã khai nguồn tin cậy, đỏ (kèm đúng
            // tên kiểu/file vi phạm) khi thiếu.
            finding.DeclaresTrustedSource,
            $"{finding.TypeDeclaration} in {finding.File} does not declare a trusted source "
            + "(contracts/message-handler-authorization-contract.md)."));
    }

    /// <summary>
    /// Kiểm tra: lượt quét consumer thật sự đã đi qua mọi service, không âm thầm bỏ sót.
    /// Lý do: scanner phải thật sự duyệt qua từng file của mọi service dù hôm nay có thể tìm thấy 0
    /// hoặc nhiều `IConsumer&lt;T&gt;` — nếu chỉ quét nhầm chỗ thì cũng "xanh do rỗng" y hệt trường hợp
    /// đúng, không phân biệt được.
    /// Lưu ý: hiện ĐỎ ở vế `Assert.Empty` — không phải vì scanner quét sai chỗ (vế `Assert.Contains`
    /// vẫn đúng), mà vì spec 024 đã thêm 1 consumer thật (xem test phía trên), nên `result.Findings`
    /// không còn rỗng nữa. Xem QA_Debt mục 015.
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — tự bảo vệ cơ chế thực thi FR-002.
    /// </summary>
    [Fact]
    public void ScanConsumers_ActuallyExaminesEveryService()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanConsumers(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        // Assert.All + Assert.Contains(phần tử, tập hợp): với mỗi service kỳ vọng, phải tìm thấy ít
        // nhất 1 file đã quét thuộc đúng thư mục service đó; đỏ nếu thiếu 1 service nào.
        Assert.All(
            AuthorizationPolicyDeclaredScanner.AuthorizingServices,
            service => Assert.Contains(result.ScannedFiles, file => file.Contains(
                $"{Path.DirectorySeparatorChar}{service}{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)));

        // Assert.Empty(tập hợp): xanh khi rỗng — kỳ vọng gốc lúc viết spec là "chưa có consumer nào
        // nên chưa có gì để báo cáo"; nay đỏ vì đã có 1 consumer thật (xem Lưu ý ở trên).
        Assert.Empty(result.Findings);
    }
}
