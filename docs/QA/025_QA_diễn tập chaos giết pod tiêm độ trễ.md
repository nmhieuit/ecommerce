# QA: Diễn tập chaos engineering — giết một pod / tiêm độ trễ để kiểm chứng resilience

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — kill-pod (`baskets`)**: không có mã ứng dụng mới; `kubectl delete pod -l app=baskets` → Kubernetes tái lập lịch (1 replica nên là cold-start lại), BFF retry/timeout theo cấu hình 020, quan sát qua telemetry `"Polly"`.
2. **US2 — inject-latency (`orders`)**: `ChaosLatencyInjectionMiddleware` đăng ký NGAY SAU `UseServiceDefaults()`, TRƯỚC `UseIdentityValidation()`; 2 lớp gate — cờ `Chaos:AllowLatencyInjection` (mặc định `false`) và header `X-Chaos-Latency-Ms`; giá trị không hợp lệ/≤ 0 coi như vắng mặt, trần `MaxInjectedLatencyMs = 30000`; luôn gọi `next`.
3. **US3 — bản ghi kết quả**: thư mục `docs/dien-tap-chaos-engineering/` (mẫu, README, `ket-qua/` gồm 3 bản ghi), tất cả kết luận `sai_lech`.
4. Quan sát SLO tái dùng dashboard 021 (`traces-generic.otel-default*`), không có dashboard riêng.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ chaos rồi bấm Postman; kill-pod chạy trên cụm Kubernetes Docker Desktop

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api orders-api elasticsearch otel-collector` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token) → 01`, rồi trong folder
**`25 - Diễn tập chaos: tiêm độ trễ orders-api`** chạy folder con `25a` (cờ TẮT), đổi cờ, rồi `25b` (cờ BẬT).

**Công tắc cấu hình**: biến `CHAOS_ALLOW_LATENCY_INJECTION` trong `.env` (xem `.env.example`; cả 2 file compose, mặc định `false` = tắt), rồi `docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps orders-api` và chờ healthy.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Bất biến 1 — cờ TẮT thì bỏ qua header | Mặc định (`CHAOS_ALLOW_LATENCY_INJECTION` không khai báo; container in `false`) | `25a` bước 01 (`GET orders /health/live` + `X-Chaos-Latency-Ms: 2000`) | Không trễ | `200` trong **61 ms** |
| FR-002/US2 — cờ BẬT, trễ đúng giá trị | `CHAOS_ALLOW_LATENCY_INJECTION=true` trong `.env` rồi tạo lại `orders-api` (container in `true`) | `25b` bước 01 (`2000`) | Trễ ~2 giây | `200` sau **2 s** |
| Bất biến 3 — giá trị sai coi như vắng | (như trên) | `25b` bước 02–05 (`abc`, `0`, `-5`, `1.5`) | Không trễ | Cả 4 `200` trong **5–7 ms** |
| Bất biến 4 — trần 30 giây | (như trên) | `25b` bước 06 (`40000`) | Kẹp 30 giây | `200` sau **30 s** |
| Bất biến 5 + 6 — trước xác thực, không đổi phản hồi | (như trên) | `25b` bước 07 (`GET /orders/{id}` không token + `2000`) | Vẫn trễ rồi `401` như cũ | `401` sau **2 s** |
| Quickstart Bước 2 — tiêm độ trễ **qua BFF** | (như trên) | `25b` bước 08 (`GET gateway /bff/orders/{id}` + `2000`) | BFF timeout (`AttemptTimeout` 1 s) rồi breaker mở | `404` (đơn không tồn tại) trong **492 ms** — header không được chuyển tiếp, không trễ (xem QA_Debt) |
| Quickstart Bước 3 — SLO gần thời gian thực | (như trên) | `25b` bước 09, 10 (Elasticsearch) | Tiêu hao SLO thấy được ngay | Sau ~20 giây có **4** span `Orders.Api` ≥ 2 giây (bước 01, 06, 07, 09) |
| Quickstart Bước 1 — kill-pod trên K8s thật *(ngoại lệ: dùng `kubectl`; đã nạp image thật vào worker, Deployment `baskets` 1 replica theo mẫu repo — probe `initialDelaySeconds: 10`, `maxUnavailable: 0`)* | `kubectl delete pod` ×3, thăm dò `/health/ready` qua API-server proxy mỗi 0,2 giây | (không có) | Tái lập lịch tự động, phục hồi đo được, lặp lại được (SC-001) | Lần đầu Ready sau 14 s; lỗi bắt đầu **+0,6–0,7 s** sau khi xoá, phục hồi sau **+13,6 s / +14,3 s / +12,9 s** rồi ổn định, không cần can thiệp |
| Bước 1 — breaker ở BFF | Tải nền ~2 req/s như quickstart gợi ý | (không có) | Breaker engage quan sát được | Chưa đo lại ở lượt này; theo QA 020 mở mạch cần tải song song (`MinimumThroughput 100`) nên ~2 req/s chỉ thấy retry/timeout |
| Mutation trên mã (chuyển middleware xuống sau `UseTenancy()`; `AllowLatencyInjection` mặc định `true`; bỏ `Math.Min`; bỏ điều kiện `AllowLatencyInjection &&`) *(ngoại lệ: sửa mã production)* | — | — | Test đỏ | **Không chạy lại ở lượt này** (sửa mã production bị chặn bởi quyền của phiên). Kết quả lượt trước còn nguyên (xem QA_Debt): 2 mutation đầu vẫn xanh; 2 mutation sau đỏ đúng `…ClampsToMax` và `…IgnoresHeaderEntirely` |
| Dọn dẹp | Bỏ `CHAOS_ALLOW_LATENCY_INJECTION` khỏi `.env`, tạo lại `orders-api`; xoá namespace `qa025`, gỡ image khỏi worker, ngắt worker khỏi mạng compose | (không có) | Không dữ liệu dư | `orders-api` in `false`; `kubectl get ns` không còn `qa025`. Hai namespace `chaos-exercise*` vẫn `Terminating` từ lần diễn tập gốc (không do QA tạo) |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| Bất biến 1 — cờ tắt thì bỏ qua header | [`ChaosLatencyInjectionMiddlewareTests.cs:31`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L31) — `InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection` |
| Bất biến 2 — không header thì không trễ | [`:51`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L51) — `InvokeAsync_EnabledWithoutHeader_DoesNotDelay` | (lệnh như trên) |
| FR-002/US2 — trễ đúng giá trị yêu cầu | [`:70`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L70) — `InvokeAsync_EnabledWithValidHeader_DelaysByRequestedAmount` | (lệnh như trên) |
| Bất biến 4 — kẹp trần 30 000 ms | [`:91`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L91) — `InvokeAsync_HeaderAboveSafetyCap_ClampsToMax` | (lệnh như trên) |
| Bất biến 3 — giá trị sai/≤ 0 coi như vắng (3 ca) | [`:115`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L115) — `InvokeAsync_InvalidOrNonPositiveHeader_TreatedAsAbsent` | (lệnh như trên) |
| Bất biến 6 — luôn gọi `next` | [`:136`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L136) — `InvokeAsync_AlwaysCallsNext_RegardlessOfInjection` | (lệnh như trên) |

**Kết quả lượt QA này (2026-09-27)**: 8/8 test chaos xanh. US1, cấu hình mặc định và thứ tự middleware không có test tự động (xem QA_Debt).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Cơ chế tiêm độ trễ đúng hợp đồng (cờ TẮT bỏ qua, cờ BẬT trễ đúng, giá trị sai coi như vắng, trần 30 giây, chạy trước xác thực — đo bằng Postman trên `orders-api` thật, 8/8 test xanh), kill-pod trên Kubernetes thật với image thật phục hồi ≈ 13–14 s và lặp lại được, độ trễ tiêm hiện trong Elasticsearch trong ~20 s. Ghi chú:
(1) header `X-Chaos-Latency-Ms` không được gateway/BFF chuyển tiếp (bước 08: `404` trong 492 ms) nên công cụ không thể làm BFF timeout hay mở breaker qua đường BFF — kết luận "do `Task.Delay` không chặn thread" trong bản ghi kết quả là giả thuyết sai và Acceptance Criteria "breaker trips" không đạt được bằng thiết kế hiện tại;
(2) 3/3 bản ghi `sai_lech` nhưng chưa mở ticket — vi phạm chính hợp đồng bản ghi (FR-008/SC-004); (3) test không bảo vệ thứ tự middleware và cấu hình mặc định tắt; (4) tải nền ~2 req/s không thể làm breaker mở (SC-002); (5) 2 namespace diễn tập gốc còn kẹt `Terminating`.
Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 025.
