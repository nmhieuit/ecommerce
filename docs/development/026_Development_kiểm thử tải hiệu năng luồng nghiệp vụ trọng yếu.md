# Bước 026: Thay đổi nghiệp vụ so với bước 025

## Phạm vi

Tài liệu này mô tả phần code được tạo bởi bước 026 (SCRUM-32, thư mục spec thật là
`specs/025-load-performance-test-budgets/` — đánh số tài liệu 026 vì trùng số thư mục với
025-chaos-pod-kill-latency/SCRUM-34, xem ghi chú đánh số ở
[026_Architect_*.md](../architecture/026_Architect_kiểm%20thử%20tải%20hiệu%20năng%20luồng%20nghiệp%20vụ%20trọng%20yếu.md))
so với trạng thái code sau bước 025.

Ranh giới commit: toàn bộ tính năng nằm trong đúng 1 commit —
`fc6c377 Add critical path load test implementation with budget assertions` — phát triển trên một
nhánh tách riêng từ trước khi bước 025 (chaos) merge vào `master`, merge sau (PR #38) qua
`da61130 Merge pull request #38 from nmhieuit/code/Load-performance-test-against-constitution-budgets`.
Không đụng `services/orders/src/Orders.Api/Features/Chaos/` hay bất kỳ file nào của bước 025.

## 1. Dự án kiểm thử tải mới — `tests/CriticalPathLoadTests`

Không có service runtime mới, không có `.csproj` service nào bị sửa — toàn bộ mã mới nằm trong 1 dự
án xUnit mới, tái dùng `ServiceManifestFixture` của 021 (`ProjectReference` tới
`tests/ServiceManifestSloConventionTests`) thay vì parse YAML lần hai:

[tests/CriticalPathLoadTests/BudgetAssertions.cs](../../tests/CriticalPathLoadTests/BudgetAssertions.cs)
(mới):

```csharp
public static class BudgetAssertions
{
    // 026: logic thuần, không tự gọi HTTP — nhận 1 LoadTestRunResult đã đo xong, ném lỗi liệt kê MỌI
    // bước vi phạm (không chỉ bước đầu tiên), trỏ tới báo cáo đã ghi để điều tra không cần chạy lại.
    public static void AssertAllStepsWithinBudget(LoadTestRunResult result, string reportPath)
    {
        var violations = result.Steps.Where(step => !step.Passed).ToList();
        if (violations.Count == 0) return;

        Assert.Fail($"{violations.Count} step(s) exceeded their declared budget — see {reportPath}: ...");
    }
}
```

`CriticalPathScenario.cs` định nghĩa kịch bản NBomber 4 bước thật theo đúng thứ tự
(`GET /bff/products` → `POST /bff/basket/items` → `POST /bff/checkout` → `GET /bff/orders/{orderId}`)
qua `GatewayClient` (gọi qua gateway, KHÔNG tự đính bearer token — xem
[technical-debt.md](../architecture/technical-debt.md) về phát hiện 401 khi chạy thật).
`CriticalPathStepBudget.cs` đọc ngưỡng p95/p99 từ khối `slos.latency` cấp service của `bff` qua
`ServiceManifestFixture` — áp dụng đồng nhất cho cả 4 bước, không mở rộng model để parse riêng khối
`endpoints:` (thuộc phạm vi feature khác).

## 2. Khoảng trống manifest thật đã lấp trước khi đo được

[services/bff/src/Bff.Api/service-manifest.yaml](../../services/bff/src/Bff.Api/service-manifest.yaml):

```yaml
endpoints:
  # 026: 2 mục này THIẾU trước bước này — route đã tồn tại và hoạt động từ 004/006, nhưng manifest
  # (tài liệu mô tả, viết thủ công) chưa được cập nhật theo kịp. Dùng đúng ngân sách client-facing-bff
  # có sẵn của mọi route khác trong file (p95 300ms/p99 800ms).
  - path: POST /bff/basket/items
    latency: { p95_ms: 300, p99_ms: 800 }
  - path: POST /bff/checkout
    latency: { p95_ms: 300, p99_ms: 800 }
```

Comment "Scope note" ở đầu file cũng được sửa — trước đó vẫn nói "the health endpoints are the only
surface this service exposes today", không còn đúng thực tế từ lâu.

## 3. Tier CI "performance" mới — tách biệt hoàn toàn PR gate hiện có

`scripts/ci/run-dotnet-tests.sh` thêm case `performance` khớp `CriticalPathLoadTests.csproj`, và
`grep -v 'CriticalPathLoadTests'` vào case `unit` hiện có — nếu thiếu bước loại trừ này, dự án kiểm
thử tải (chậm hơn nhiều bậc, cần stack sống) sẽ tự động lọt vào tier `unit` chạy trên MỌI PR, làm hỏng
chính cổng chặn PR đang được branch protection thực thi (013). `Jenkinsfile.performance` (mới, ở gốc
repo) là 1 pipeline TÁCH BIỆT khỏi `Jenkinsfile` — kích hoạt theo lịch (`triggers { cron(...) }`),
KHÔNG thêm stage vào 5 stage bắt buộc của 013.

## Tóm tắt 025 → 026

| Khu vực | Bước 025 | Bước 026 |
|---|---|---|
| `tests/` | Không có `CriticalPathLoadTests` | Dự án mới — NBomber 4.1.2, đo + đối chiếu + fail |
| `services/bff/.../service-manifest.yaml` | Thiếu 2 mục `endpoints` | Đủ 4 mục cho luồng trọng yếu |
| `scripts/ci/run-dotnet-tests.sh` | Không có tier `performance` | + tier `performance`, loại trừ khỏi `unit` |
| `Jenkinsfile.performance` | Không tồn tại | Pipeline mới, theo lịch, tách biệt PR gate |
| `services/orders/.../Chaos/` (025) | Có | Không đổi |

**Kết luận:** bước 026 không thêm nghiệp vụ mua hàng mới cho người dùng cuối — nó thêm 1 công cụ đo
lường/cổng chặn hiệu năng đứng ngoài luồng response thật. Lần chạy thật đầu tiên của chính công cụ này
lại phát hiện 1 khoảng trống xác thực của toàn nền tảng (401 qua gateway) — xem
[technical-debt.md](../architecture/technical-debt.md).

## Shared project trong bước 026

Không tạo, không sửa shared project production nào. `tests/CriticalPathLoadTests` chỉ thêm
`ProjectReference` (test-only) tới `tests/ServiceManifestSloConventionTests` đã có, để tái dùng
`ServiceManifestFixture` — không có `PackageReference`/`ProjectReference` production mới nào ở bất kỳ
service nào.
