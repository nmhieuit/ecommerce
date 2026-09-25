# QA: Diễn tập chaos engineering — giết một pod / tiêm độ trễ để kiểm chứng resilience

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — kill-pod (`baskets`)**: không có mã ứng dụng mới; `kubectl delete pod -l app=baskets` → Kubernetes tái lập lịch (1 replica nên là cold-start lại), BFF retry/timeout theo cấu hình 020, quan sát qua telemetry `"Polly"`.
2. **US2 — inject-latency (`orders`)**: `ChaosLatencyInjectionMiddleware` đăng ký NGAY SAU `UseServiceDefaults()`, TRƯỚC `UseIdentityValidation()`; 2 lớp gate — cờ `Chaos:AllowLatencyInjection` (mặc định `false`) và header `X-Chaos-Latency-Ms`; giá trị không hợp lệ/≤ 0 coi như vắng mặt, trần `MaxInjectedLatencyMs = 30000`; luôn gọi `next`.
3. **US3 — bản ghi kết quả**: thư mục `docs/dien-tap-chaos-engineering/` (mẫu, README, `ket-qua/` gồm 3 bản ghi), tất cả kết luận `sai_lech`.
4. Quan sát SLO tái dùng dashboard 021 (`traces-generic.otel-default*`), không có dashboard riêng.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| Bất biến 1 — cờ tắt thì bỏ qua header | [`ChaosLatencyInjectionMiddlewareTests.cs:31`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L31) — `InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection` |
| Bất biến 2 — không header thì không trễ | [`:51`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L51) — `InvokeAsync_EnabledWithoutHeader_DoesNotDelay` | (lệnh như trên) |
| FR-002/US2 — trễ đúng giá trị yêu cầu | [`:70`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L70) — `InvokeAsync_EnabledWithValidHeader_DelaysByRequestedAmount` | (lệnh như trên) |
| Bất biến 4 — kẹp trần 30 000 ms | [`:91`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L91) — `InvokeAsync_HeaderAboveSafetyCap_ClampsToMax` | (lệnh như trên) |
| Bất biến 3 — giá trị sai/≤ 0 coi như vắng (3 ca) | [`:115`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L115) — `InvokeAsync_InvalidOrNonPositiveHeader_TreatedAsAbsent` | (lệnh như trên) |
| Bất biến 6 — luôn gọi `next` | [`:136`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L136) — `InvokeAsync_AlwaysCallsNext_RegardlessOfInjection` | (lệnh như trên) |

**Kết quả lượt QA này (2026-09-24)**: 8/8 test chaos xanh; `dotnet build Ecommerce.slnx` 0 lỗi — không đổi sau khi dịch comment. Cả project `Orders.Api.UnitTests` còn 1 test đỏ có sẵn `HealthCheckTests.HealthLive_ReturnsOk` (thiếu `ConnectionStrings__OrdersDb`, đã ghi ở mục 001/009). US1, cấu hình mặc định và thứ tự middleware không có test tự động (xem QA_Debt).

### Thủ công — stack Docker + cụm Kubernetes Docker Desktop

| Bước | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Bất biến 1–3 trên `orders-api` thật | Cờ tắt / bật (`Chaos__AllowLatencyInjection=true` bằng override tạm), gọi `/health/live` với các giá trị header | Chỉ header hợp lệ + cờ bật mới trễ | Cờ tắt + `2000`: 0.12 s; cờ bật: `2000` → 2.22 s; `abc`, `-5`, `0`, rỗng, `99999999999`, `1.5` → 0.01–0.09 s; `" 2000"` → 2.03 s |
| Bất biến 4 — trần | Header `40000` | Kẹp 30 s | 30.05 s |
| Bất biến 5 + 6 — trước xác thực, không đổi phản hồi | `GET /orders/{id}` không token, có/không header `2000` | Vẫn trễ rồi trả `401` như cũ | `401` trong 0.11 s (không header) và 2.05 s (có header) |
| Quickstart Bước 2 — tiêm độ trễ **qua BFF** | `GET gateway :5300/bff/orders/{id}` + header `2000`, ×3 | BFF timeout (`AttemptTimeout` 1 s) rồi breaker mở | **0.016–0.031 s**, như không có header — header không được chuyển tiếp; gọi thẳng orders với cùng header: 2.01 s (xem QA_Debt) |
| Quickstart Bước 3 — SLO gần thời gian thực | 6 request song song `X-Chaos-Latency-Ms: 2500` tới orders; truy vấn Elasticsearch | Tiêu hao SLO thấy được ngay | Span `Orders.Api` ≥ 2 s (p95 = 3117 ms) truy vấn được sau ≈ 12.7 s kể từ khi request kết thúc |
| Quickstart Bước 1 — kill-pod trên K8s thật | Nạp image thật vào worker, Deployment 1 replica (probe/`maxUnavailable: 0` theo mẫu repo, `initialDelaySeconds: 10`), thăm dò `/health/ready` mỗi 0.2 s, `kubectl delete pod` ×3 | Tái lập lịch tự động, phục hồi đo được, lặp lại được (SC-001) | Thăm dò lỗi trong **+13.2 s / +14.9 s / +13.8 s** sau khi xoá rồi ổn định, không cần can thiệp; bản ghi cũ 35–42 s gồm thao tác thủ công |
| Bước 1 — breaker ở BFF | Tải nền ~2 req/s như quickstart gợi ý | Breaker engage quan sát được | Chỉ retry/timeout; mở mạch cần ≥ ~10 req/s (`MinimumThroughput 100`, QA 020) |
| Mutation: chuyển middleware xuống sau `UseTenancy()`; `AllowLatencyInjection` mặc định `true` | Sửa `Program.cs` / `appsettings.json`, chạy test, hoàn tác | Test đỏ | **Vẫn xanh cả hai** — xem QA_Debt |
| Mutation: bỏ `Math.Min`; bỏ điều kiện `AllowLatencyInjection &&` | Sửa middleware, chạy test, hoàn tác | Test đỏ | Đỏ đúng `…ClampsToMax` và `…IgnoresHeaderEntirely` |

Đã dọn: xoá namespace `qa025`, gỡ image khỏi worker, ngắt worker khỏi mạng compose, tạo lại `orders-api` đúng theo compose gốc. Hai namespace `chaos-exercise*` còn `Terminating` từ lần diễn tập gốc (không do QA tạo).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Cơ chế tiêm độ trễ đúng hợp đồng (sống trên `orders-api` thật, 8/8 test xanh), kill-pod trên Kubernetes thật với image thật phục hồi ≈ 14 s và lặp lại được, độ trễ tiêm hiện trong Elasticsearch trong ~13 s. Ghi chú:
(1) header `X-Chaos-Latency-Ms` không được gateway/BFF chuyển tiếp nên công cụ không thể làm BFF timeout hay mở breaker qua đường BFF — kết luận "do `Task.Delay` không chặn thread" trong bản ghi kết quả là giả thuyết sai và Acceptance Criteria "breaker trips" không đạt được bằng thiết kế hiện tại;
(2) 3/3 bản ghi `sai_lech` nhưng chưa mở ticket — vi phạm chính hợp đồng bản ghi (FR-008/SC-004); (3) test không bảo vệ thứ tự middleware và cấu hình mặc định tắt; (4) số liệu kill-pod cũ (35–42 s) gồm thao tác thủ công, và tải nền ~2 req/s không thể làm breaker mở (SC-002); (5) 2 namespace diễn tập gốc còn kẹt `Terminating`.
Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 025.
