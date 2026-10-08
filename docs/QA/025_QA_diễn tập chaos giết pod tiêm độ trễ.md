# QA: Diễn tập chaos engineering — tiêm độ trễ để kiểm chứng resilience

> **Cập nhật spec 033**: công thức ngân sách loại span có đường dẫn bắt đầu bằng `/health`; việc tiêm lỗi/độ trễ nay đi vào route nghiệp vụ. Xem [033 QA](033_QA_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).

> **Cập nhật (spec 031, 2026-10-06)**: phần kill-pod/Kubernetes đã gỡ khỏi tài liệu này; còn lại chỉ phần tiêm độ trễ.

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — kill-pod (`baskets`)**: đã gỡ ở spec 031 (kịch bản trên Kubernetes không còn dùng; thay bằng nhóm 8 — container chết — của 031).
2. **US2 — inject-latency (`orders`)**: `ChaosLatencyInjectionMiddleware` đăng ký NGAY SAU `UseServiceDefaults()`, TRƯỚC `UseIdentityValidation()`; 2 lớp gate — cờ `Chaos:AllowLatencyInjection` (mặc định `false`) và header `X-Chaos-Latency-Ms`; giá trị không hợp lệ/≤ 0 coi như vắng mặt, trần `MaxInjectedLatencyMs = 30000`; luôn gọi `next`.
3. **US3 — bản ghi kết quả**: thư mục `docs/dien-tap-chaos-engineering/` (mẫu, README, `ket-qua/` gồm 1 bản ghi inject-latency), kết luận `sai_lech`.
4. Quan sát SLO tái dùng dashboard 021 (`traces-generic.otel-default*`), không có dashboard riêng.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ chaos rồi bấm Postman

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api orders-api elasticsearch otel-collector` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token) → 01`, rồi trong folder
**`25 - Diễn tập chaos: tiêm độ trễ orders-api`** chạy folder con `25a` (cờ TẮT), đổi cờ, rồi `25b` (cờ BẬT).

**Công tắc cấu hình**: biến `CHAOS_ALLOW_LATENCY_INJECTION` trong `.env` (xem `.env.example`; cả 2 file compose, mặc định `false` = tắt), rồi `docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps orders-api` và chờ healthy.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Bất biến 1 — cờ TẮT thì bỏ qua header | Mặc định (`CHAOS_ALLOW_LATENCY_INJECTION` không khai báo; container in `false`) | `25a` bước 01 (`GET orders /orders/{id}` + `X-Chaos-Latency-Ms: 2000`; spec 033 đổi từ `/health/live` vì span health không còn tính vào ngân sách) | Không trễ | nhanh, `401` (route cần token); số đo cũ `200` trong **61 ms** là của `/health/live` |
| FR-002/US2 — cờ BẬT, trễ đúng giá trị | `CHAOS_ALLOW_LATENCY_INJECTION=true` trong `.env` rồi tạo lại `orders-api` (container in `true`) | `25b` bước 01 (`2000`) | Trễ ~2 giây | `200` sau **2 s** |
| Bất biến 3 — giá trị sai coi như vắng | (như trên) | `25b` bước 02–05 (`abc`, `0`, `-5`, `1.5`) | Không trễ | Cả 4 `200` trong **5–7 ms** |
| Bất biến 4 — trần 30 giây | (như trên) | `25b` bước 06 (`40000`) | Kẹp 30 giây | `200` sau **30 s** |
| Bất biến 5 + 6 — trước xác thực, không đổi phản hồi | (như trên) | `25b` bước 07 (`GET /orders/{id}` không token + `2000`) | Vẫn trễ rồi `401` như cũ | `401` sau **2 s** |
| Quickstart Bước 2 — tiêm độ trễ **qua BFF** | (như trên) | `25b` bước 08 (`GET gateway /bff/orders/{id}` + `2000`) | BFF timeout (`AttemptTimeout` 1 s) rồi breaker mở | `404` (đơn không tồn tại) trong **492 ms** — header không được chuyển tiếp, không trễ (xem QA_Debt) |
| Quickstart Bước 3 — SLO gần thời gian thực | (như trên) | `25b` bước 09, 10 (Elasticsearch) | Tiêu hao SLO thấy được ngay | Sau ~20 giây có **4** span `Orders.Api` ≥ 2 giây (bước 01, 06, 07, 09) |
| Bước 1 — breaker ở BFF | Tải nền ~2 req/s như quickstart gợi ý | (không có) | Breaker engage quan sát được | Chưa đo lại ở lượt này; theo QA 020 mở mạch cần tải song song (`MinimumThroughput 100`) nên ~2 req/s chỉ thấy retry/timeout |
| Mutation trên mã (chuyển middleware xuống sau `UseTenancy()`; `AllowLatencyInjection` mặc định `true`; bỏ `Math.Min`; bỏ điều kiện `AllowLatencyInjection &&`) *(ngoại lệ: sửa mã production)* | — | — | Test đỏ | **Không chạy lại ở lượt này** (sửa mã production bị chặn bởi quyền của phiên). Kết quả lượt trước còn nguyên (xem QA_Debt): 2 mutation đầu vẫn xanh; 2 mutation sau đỏ đúng `…ClampsToMax` và `…IgnoresHeaderEntirely` |
| Dọn dẹp | Bỏ `CHAOS_ALLOW_LATENCY_INJECTION` khỏi `.env`, tạo lại `orders-api` | (không có) | Không dữ liệu dư | `orders-api` in `false` |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| Bất biến 1 — cờ tắt thì bỏ qua header | [`ChaosLatencyInjectionMiddlewareTests.cs:31`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L31) — `InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection` |
| Bất biến 2 — không header thì không trễ | [`:51`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L51) — `InvokeAsync_EnabledWithoutHeader_DoesNotDelay` | (lệnh như trên) |
| FR-002/US2 — trễ đúng giá trị yêu cầu | [`:70`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L70) — `InvokeAsync_EnabledWithValidHeader_DelaysByRequestedAmount` | (lệnh như trên) |
| Bất biến 4 — kẹp trần 30 000 ms | [`:91`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L91) — `InvokeAsync_HeaderAboveSafetyCap_ClampsToMax` | (lệnh như trên) |
| Bất biến 3 — giá trị sai/≤ 0 coi như vắng (3 ca) | [`:115`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L115) — `InvokeAsync_InvalidOrNonPositiveHeader_TreatedAsAbsent` | (lệnh như trên) |
| Bất biến 6 — luôn gọi `next` | [`:136`](../../services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs#L136) — `InvokeAsync_AlwaysCallsNext_RegardlessOfInjection` | (lệnh như trên) |

**Kết quả lượt QA này (2026-09-27)**: 8/8 test chaos xanh. Cấu hình mặc định và thứ tự middleware không có test tự động (xem QA_Debt).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Cơ chế tiêm độ trễ đúng hợp đồng (cờ TẮT bỏ qua, cờ BẬT trễ đúng, giá trị sai coi như vắng, trần 30 giây, chạy trước xác thực — đo bằng Postman trên `orders-api` thật, 8/8 test xanh), độ trễ tiêm hiện trong Elasticsearch trong ~20 s. Ghi chú:
(1) header `X-Chaos-Latency-Ms` không được gateway/BFF chuyển tiếp (bước 08: `404` trong 492 ms) nên công cụ không thể làm BFF timeout hay mở breaker qua đường BFF — kết luận "do `Task.Delay` không chặn thread" trong bản ghi kết quả là giả thuyết sai và Acceptance Criteria "breaker trips" không đạt được bằng thiết kế hiện tại;
(2) bản ghi inject-latency `sai_lech` nhưng chưa mở ticket — vi phạm chính hợp đồng bản ghi (FR-008/SC-004); (3) test không bảo vệ thứ tự middleware và cấu hình mặc định tắt; (4) tải nền ~2 req/s không thể làm breaker mở.
Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 025.
