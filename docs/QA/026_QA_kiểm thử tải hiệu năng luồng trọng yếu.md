# QA: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu đối chiếu ngân sách hiệu năng của hiến chương

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

*Ghi chú đánh số: tài liệu 026 (SCRUM-32) tương ứng thư mục spec `specs/025-load-performance-test-budgets/` (trùng số với spec chaos).*

## Luồng happy-case đã rà soát

1. `tests/CriticalPathLoadTests` (NBomber 4.1.2, ghim vì bản 5.x/6.x tính phí) chạy kịch bản 4 bước qua gateway: `GET /bff/products` → `POST /bff/basket/items` → `POST /bff/checkout` → `GET /bff/orders/{id}`, 2 luồng/giây trong 30 giây, mỗi luồng bắt đầu bằng 1 lần `POST /bff/checkout` để dọn giỏ.
2. `CriticalPathStepBudgets` đọc p95/p99 (300/800 ms, `client-facing-bff`) từ `services/bff/.../service-manifest.yaml` qua `ServiceManifestFixture` (spec 021); manifest đã bổ sung 2 mục `endpoints` còn thiếu.
3. `LoadTestReportWriter` ghi `artifacts/performance/critical-path-load-test-<timestamp>.md` (không ghi đè, mốc nền cho lần sau); `BudgetAssertions` làm cả lần chạy thất bại nếu bất kỳ bước nào có p95 hoặc p99 vượt ngân sách.
4. `scripts/ci/run-dotnet-tests.sh` có tier `performance` riêng và loại `CriticalPathLoadTests` khỏi tier `unit`; `Jenkinsfile.performance` chạy theo lịch (`cron`), đăng check `ci/performance-gate`, tách khỏi PR gate.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-004/US2-KB3 — mọi bước đạt (kể cả đúng bằng ngưỡng) thì không lỗi | [`BudgetAssertionsTests.cs:23`](../../tests/CriticalPathLoadTests/BudgetAssertionsTests.cs#L23) — `AssertAllStepsWithinBudget_DoesNotThrow_WhenEveryStepIsWithinBudget` | `dotnet test tests/CriticalPathLoadTests --filter FullyQualifiedName~BudgetAssertionsTests` |
| FR-004/US2-KB1 — p95 vượt thì thất bại | [`:43`](../../tests/CriticalPathLoadTests/BudgetAssertionsTests.cs#L43) — `AssertAllStepsWithinBudget_Throws_WhenP95ExceedsBudget` | (lệnh như trên) |
| FR-004 — p99 vượt thì thất bại | [`:62`](../../tests/CriticalPathLoadTests/BudgetAssertionsTests.cs#L62) — `AssertAllStepsWithinBudget_Throws_WhenP99ExceedsBudget` | (lệnh như trên) |
| Bất biến 5 — nêu tên mọi bước vi phạm | [`:83`](../../tests/CriticalPathLoadTests/BudgetAssertionsTests.cs#L83) — `AssertAllStepsWithinBudget_ThrowsOnce_NamingEveryViolatingStep_NotJustTheFirst` | (lệnh như trên) |
| FR-001/FR-002 — 1 ngân sách cho mỗi bước, đúng thứ tự | [`CriticalPathStepBudgetsTests.cs:18`](../../tests/CriticalPathLoadTests/CriticalPathStepBudgetsTests.cs#L18) — `LoadAll_ReturnsOneBudgetPerStep_InOrder` | `dotnet test tests/CriticalPathLoadTests --filter FullyQualifiedName~CriticalPathStepBudgetsTests` |
| FR-003 — ngưỡng khớp `client-facing-bff` 300/800 ms | [`:36`](../../tests/CriticalPathLoadTests/CriticalPathStepBudgetsTests.cs#L36) — `LoadAll_MatchesTheClientFacingBffDefaultDeclaredInTheManifest` | (lệnh như trên) |
| FR-001…FR-005 — chạy tải thật, ghi báo cáo, fail nếu vượt (cần stack sống) | [`CriticalPathLoadTest.cs:30`](../../tests/CriticalPathLoadTests/CriticalPathLoadTest.cs#L30) — `CriticalPath_MeasuredAgainstDeclaredBudget` | `dotnet test tests/CriticalPathLoadTests --filter FullyQualifiedName~CriticalPathLoadTest.CriticalPath` |

**Kết quả lượt QA này (2026-09-24)**: 6 test thuần xanh (0.3–0.5 s) và `dotnet build Ecommerce.slnx` 0 lỗi — không đổi sau khi dịch comment. Test tải thật cần stack sống: xem bảng thủ công (không token thì đỏ vì 401).

### Thủ công — stack `docker-compose.local.yml` (cổng gateway `:5300`)

| Bước | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Chạy nguyên trạng, không token | `dotnet test tests/CriticalPathLoadTests --filter …CriticalPath` | Đỏ vì 401 kèm báo cáo (theo tài liệu) | 60/60 `401`; đỏ bằng `InvalidOperationException: Sequence contains no matching element` (`CriticalPathLoadTest.cs:38`), **không có báo cáo** — xem QA_Debt |
| Kịch bản 1 — baseline (có token: vá tạm `GatewayClient` đọc `LOADTEST_TOKEN`) | Chạy 2 lần liên tiếp | `PASS` + báo cáo có 4 bước, ngưỡng đọc từ manifest | Lần 1 (nguội): `FAIL` p95 564.7/829.4/207.7 ms; lần 2: **`PASS`** p95 29.7/100.2/110.5/27.3 ms, ngưỡng 300/800 khớp manifest; mỗi lần 1 file báo cáo mới, không ghi đè |
| Kịch bản 2 — chèn hồi quy | `Task.Delay(600)` trong handler `POST /baskets/current/items` của baskets, dựng lại image, chạy 2 lần | Thất bại, chỉ đúng bước bị chậm | **`FAIL`, exit 1**: `POST /bff/basket/items` p95 930.3 ms và 675.3 ms (> 300), 3 bước còn lại `Pass` |
| Kịch bản 3 — khắc phục | Bỏ `Task.Delay`, dựng lại image, chạy 2 lần | Thành công trở lại, không đổi cấu hình | **`PASS`, exit 0** cả 2 lần (p95 add-item 136.6 / 116.0 ms) |
| Chạy ngay sau khi khởi động lại dịch vụ | Restart `bff/baskets/orders/products/gateway`, đợi `healthy`, chạy 3 lần | Ổn định | Lần 1: **60/60 `503`/`504`** (crash không báo cáo); lần 2 `FAIL` (45 % checkout lỗi); lần 3 `PASS` — cổng đỏ "vì nguội" |
| Tỷ lệ lỗi trong cổng | Đọc báo cáo/số liệu NBomber | Lỗi làm cổng đỏ | 4–27 luồng lỗi/60 vẫn ra `Overall: PASS` — báo cáo không có cột lỗi (xem QA_Debt) |
| Mutation: hard-code 300/800 trong `LoadAll` | Sửa, chạy 6 test, hoàn tác | Test đỏ | **6/6 vẫn xanh** |
| Mutation: `<=` → `<` trong `StepResult.Passed` | Sửa, chạy 6 test, hoàn tác | Test đỏ | Đỏ đúng test biên |
| Loại khỏi tier `unit` | `find … \| grep -v CriticalPathLoadTests` | Không lọt vào PR | Đúng: 0 kết quả trong `unit`, có trong `performance` |

Đã dọn: hoàn tác `GatewayClient`/baskets/bff (git sạch), rebuild `baskets-api` từ mã sạch, xoá `artifacts/performance` và ~457 đơn/outbox do các lần chạy tạo (giữ đơn gốc QA 017).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Khi có token, cơ chế đo → báo cáo → cổng chạy đúng cả 3 kịch bản Jira (baseline PASS; hồi quy 600 ms làm FAIL đúng bước; khắc phục PASS lại); ngưỡng đọc từ manifest; tier `performance` tách khỏi PR gate. Ghi chú:
(1) bài kiểm thử không đính token nên trên stack hiện tại luôn đỏ 401, và đỏ bằng exception khó hiểu không kèm báo cáo (trái hợp đồng bất biến 5) — tài liệu còn mô tả "không có cách lấy token" dù từ spec 014 đã có;
(2) cổng chỉ xét độ trễ của request thành công — 4–27/60 luồng lỗi vẫn PASS; (3) lần chạy đầu sau khi khởi động lại luôn đỏ vì không làm ấm; (4) FR-008 "chặn phát hành" chưa hiện thực (chỉ đăng 1 trạng thái GitHub không ai đọc; Jenkins chưa từng chạy); (5) test không giữ bất biến "không hard-code ngân sách", và 6 test thuần chỉ chạy ở tier nightly.
Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 026.
