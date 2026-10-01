# QA: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu đối chiếu ngân sách hiệu năng của hiến chương

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

*Ghi chú đánh số: tài liệu 026 (SCRUM-32) tương ứng thư mục spec `specs/026-load-performance-test-budgets/` (ban đầu tên `025-...`, trùng số với spec chaos; đã đổi thành 026 cho khớp).*

## Luồng happy-case đã rà soát

1. `tests/CriticalPathLoadTests` (NBomber 4.1.2, ghim vì bản 5.x/6.x tính phí) chạy kịch bản 4 bước qua gateway: `GET /bff/products` → `POST /bff/basket/items` → `POST /bff/checkout` → `GET /bff/orders/{id}`, 2 luồng/giây trong 30 giây, mỗi luồng bắt đầu bằng 1 lần `POST /bff/checkout` để dọn giỏ.
2. `CriticalPathStepBudgets` đọc p95/p99 (300/800 ms, `client-facing-bff`) từ `services/bff/.../service-manifest.yaml` qua `ServiceManifestFixture` (spec 021); manifest đã bổ sung 2 mục `endpoints` còn thiếu.
3. `LoadTestReportWriter` ghi `artifacts/performance/critical-path-load-test-<timestamp>.md` (không ghi đè, mốc nền cho lần sau); `BudgetAssertions` làm cả lần chạy thất bại nếu bất kỳ bước nào có p95 hoặc p99 vượt ngân sách.
4. `scripts/ci/run-dotnet-tests.sh` có tier `performance` riêng và loại `CriticalPathLoadTests` khỏi tier `unit`; `Jenkinsfile.performance` chạy theo lịch (`cron`), đăng check `ci/performance-gate`, tách khỏi PR gate.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — chạy luồng 4 bước bằng Runner, bật/tắt suy giảm hạ tầng rồi xem bước nào vượt ngân sách

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token) → 01`, rồi folder
**`26 - Ngân sách hiệu năng luồng trọng yếu (browse → giỏ → checkout → đơn)`** trong **Runner** với **Iterations = 30** (vòng đầu là làm ấm): mỗi request có assertion `responseTime` < 300 ms (ngân sách p95 của `client-facing-bff`, đọc từ manifest bff).
Bảng dưới lấy p95/p99 từ báo cáo JSON của newman (`newman run … --folder … -n 31`, bỏ vòng đầu). Mỗi vòng tạo 1 đơn hàng.

**Công tắc** (hạ tầng, không sửa mã): `docker update --cpus 0.02 ecomerce-local-baskets-api-1` (giảm CPU của baskets) / `docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps baskets-api` (trả về mặc định — `docker update --cpus 0` **không** bỏ giới hạn); `docker compose … up -d --force-recreate --no-deps bff-api gateway-api orders-api products-api baskets-api` (làm nguội).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Kịch bản 1 — baseline, dịch vụ đã ấm | Mặc định | `26` bước 00 → 04, 30 vòng | Mọi bước p95 ≤ 300 ms, p99 ≤ 800 ms | Lần chạy (sau vài lần gọi làm ấm): p95 **71 / 75 / 204 / 335 / 101 ms** (00/01/02/03/04), p99 ≤ 347 ms; chỉ 3/155 assertion đỏ (checkout p95 335 ms sát ngưỡng). Một lần chạy khác ngay trước đó (máy còn bận) đỏ 39/155 với p95 lên tới 769 / 799 ms ở bước 02, 03 — độ trễ **dao động mạnh giữa các lần chạy**, kết quả PASS/FAIL của cổng phụ thuộc trạng thái máy |
| Kịch bản 2 — chèn hồi quy vào 1 bước | `docker update --cpus 0.02 ecomerce-local-baskets-api-1` | `26` bước 00 → 04, 30 vòng | Thất bại, chỉ đúng bước bị chậm | Bước liên quan tới baskets đỏ: **00 p95 927 ms, 02 (thêm giỏ) p95 1018 ms, 03 (checkout) p95 1105 ms**; bước 01 (products, không qua baskets) vẫn p95 **28 ms**, 04 p95 43 ms; 43/155 assertion đỏ. Chỉ giảm còn 0.1 CPU thì gần như không đổi (02 p95 128 ms) — baskets nhẹ, cần bóp mạnh mới thấy |
| Lỗi thật trong lần suy giảm | (như trên) | (như trên) | Lỗi làm cổng đỏ | 2/30 vòng có `504` ở bước 02 và checkout `409`/`404` ở bước 03/04 — báo cáo NBomber không có cột lỗi, còn Postman thấy trực tiếp (xem QA_Debt) |
| Kịch bản 3 — khắc phục | `up -d --force-recreate --no-deps baskets-api` (bỏ giới hạn) | `26` bước 00 → 04, 30 vòng | Thành công trở lại, không đổi cấu hình | 0/155 assertion đỏ khi CPU chưa bị bóp / sau khi trả về mặc định (p95 02 = 132 ms, 03 = 162 ms) |
| Chạy ngay sau khi khởi động lại dịch vụ (không làm ấm) | `up -d --force-recreate --no-deps baskets-api bff-api orders-api products-api gateway-api`, chờ healthy | `26` 4 vòng, xem vòng 0 | Ổn định | Vòng 0: `00` **3574 ms**, `01` **2740 ms**, `03` **504** (checkout hết thời gian), `04` 404 vì không có mã đơn; vòng 1–3 bình thường (≤ 535 ms) — cổng đỏ "vì nguội"; assertion đỏ 4/20 |
| Tỷ lệ lỗi trong cổng | (như trên) | (như trên) | Lỗi làm cổng đỏ | Xem dòng "Lỗi thật" — cổng NBomber chỉ đọc `Ok.Latency` |
| Chạy nguyên trạng bài kiểm thử NBomber, không token *(ngoại lệ: không có công tắc token)* | `dotnet test tests/CriticalPathLoadTests` | (không có) | Đỏ vì 401 kèm báo cáo (theo tài liệu) | 1 đỏ `InvalidOperationException: Sequence contains no matching element`, **không có báo cáo** (`artifacts/performance` không được tạo) — xem QA_Debt |
| Mutation: hard-code 300/800 trong `LoadAll`; `<=` → `<` trong `StepResult.Passed` *(ngoại lệ: sửa mã test/công cụ)* | — | — | Test đỏ | **Không chạy lại ở lượt này.** Kết quả lượt trước còn nguyên (xem QA_Debt): hard-code → 6/6 vẫn xanh; `<=` → `<` đỏ đúng test biên |
| Loại khỏi tier `unit` | `find … \| grep -v CriticalPathLoadTests` | (không có) | Không lọt vào PR | Không chạy lại ở lượt này; kết quả lượt trước: 0 kết quả trong `unit`, có trong `performance` |
| Dọn dẹp | `up -d --force-recreate --no-deps baskets-api`; xoá đơn thử | (không có) | Không dữ liệu dư | Đã xoá **168** đơn do các lần chạy tạo (`Orders` còn 1 dòng gốc, outbox `0`); giỏ rỗng; baskets/bff/orders/products/gateway chạy lại với cấu hình mặc định |

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

**Kết quả lượt QA này (2026-09-27)**: 6 test thuần xanh; `CriticalPath_MeasuredAgainstDeclaredBudget` chạy trên stack sống **đỏ** đúng như QA_Debt mô tả (`InvalidOperationException: Sequence contains no matching element`, không có báo cáo — bài kiểm thử không đính token nên 401 hàng loạt). Tổng `CriticalPathLoadTests`: 6 xanh / 1 đỏ (35 giây). Số đo tải thật ở bảng thủ công bên trên dùng Postman/newman (có token) thay cho NBomber.

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Cơ chế đo → đối chiếu ngân sách hoạt động đúng khi có token: luồng 4 bước qua gateway chạy được bằng Postman, ngưỡng 300/800 ms đọc từ manifest; bóp CPU của `baskets-api` làm đỏ đúng các bước đi qua baskets (00/02/03) và không đụng bước products (p95 28 ms); trả về mặc định thì xanh lại. 6 test thuần xanh.
Ghi chú: (1) bài kiểm thử NBomber không đính token nên trên stack hiện tại luôn đỏ 401, và đỏ bằng exception khó hiểu không kèm báo cáo (trái hợp đồng bất biến 5) — lượt này tái hiện lại nguyên trạng; (2) cổng chỉ xét độ trễ của request thành công — trong lần suy giảm có `504`/`409` mà báo cáo NBomber không thấy;
(3) lần chạy đầu sau khi khởi động lại luôn đỏ vì không làm ấm (vòng 0: 3574 ms, 2740 ms, `504`), và độ trễ dao động mạnh giữa các lần chạy trên cùng cấu hình (p95 bước 03: 335 ms so với 799 ms) nên cổng nightly dễ nhấp nháy; (4) FR-008 "chặn phát hành" chưa hiện thực; (5) test không giữ bất biến "không hard-code ngân sách", và 6 test thuần chỉ chạy ở tier nightly.
Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 026.
