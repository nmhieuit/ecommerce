# QA: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài (outbound call)

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **Kiểm kê trước khi thiết kế** (research.md Decision 1): chỉ 3/5 loại điểm gọi trong Jira thật sự tồn tại — gateway→BFF, BFF→4 service, backchannel identity.
2. **Gateway → BFF**: `HealthCheck:Passive` (`TransportFailureRate`, `ReactivationPeriod 30s`) + `AvailableDestinationsPolicy: "HealthyAndUnknown"` + `UsePassiveHealthChecks()`/`UseLoadBalancing()` trong `Program.cs`.
3. **Backchannel identity**: `HttpClient` tường minh `"IdentityBackchannel"`, `AttemptTimeout 5s`, `TotalRequestTimeout 15s`.
4. **BFF → 4 downstream**: `AttemptTimeout 1s`, `TotalRequestTimeout 3s`, `MaxRetryAttempts 2` (delay 200 ms), retry CHỈ cho `GET`/`HEAD`; `POST` giữ timeout + breaker.
5. **Quan sát**: `AddSource("Polly")`/`AddMeter("Polly")` trong `ServiceDefaultsExtensions.cs`.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — tắt/bật service phía sau rồi bấm Postman

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api elasticsearch otel-collector` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền → 01 Lấy access token`, rồi folder
**`20 - Timeout / Retry / Circuit breaker`** bằng **Runner**: **TẮT** `baskets-api` trước, chạy bước 01 → 06; **BẬT** lại rồi chạy bước 07.

**Công tắc** (hạ tầng, không sửa mã): `docker compose -f docker-compose.local.yml stop baskets-api` / `start baskets-api`; `stop bff-api` / `start bff-api`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| FR-001/SC-002 — không treo vô hạn | **TẮT** `baskets-api` | `20` bước 01 (`GET /bff/basket`) | `504`/`502` trong ≤ ~3 giây | `504` sau **3.0 giây** |
| FR-005/US3-KB1 — GET retry đúng 2 lần | **TẮT** `baskets-api` | `20` bước 02, 03 (Elasticsearch, theo correlation ID) | 2 `OnRetry`, 3 `OnTimeout` (1 lần đầu + 2 retry, mỗi lần cắt ở 1 giây) | Đúng: **2 `OnRetry`, 3 `OnTimeout`** |
| FR-006 — POST không bao giờ retry | **TẮT** `baskets-api` | `20` bước 04, 05 | `504`/`502`, 0 `OnRetry` của `BasketsApi` | `504` sau 3.6 giây; **0 `OnRetry`** |
| Cô lập lỗi | **TẮT** `baskets-api` | `20` bước 06 (`GET /bff/products`) | Route khác vẫn `200` | `200` |
| **BẬT** lại — phục hồi (US2-KB3) | `start baskets-api` | `20` bước 07 | `200` | Vài lần đầu `502` (mạch còn mở) rồi `200`; toàn folder 8/9 khi baskets tắt, 9/9 sau khi bật |
| FR-002/FR-003/SC-003 — mạch mở khi lỗi dày *(ngoại lệ: cần tải song song, Postman/Runner tuần tự không đủ — breaker BFF→service cần ≥ 100 mẫu/10 giây)* | **TẮT** `baskets-api`; `seq 1 150 \| xargs -P 50 -I{} curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5300/bff/basket -H "Authorization: Bearer <token>"` | (không có) | Mạch mở, request sau đó fail-fast | `502` ×120 + `504` ×30; `GET` đơn lẻ ngay sau: `502` trong **20–95 ms**; 8 `GET` tuần tự trước đó vẫn `504` × 3.0 giây (mạch KHÔNG mở) |
| Chu kỳ Opened → HalfOpened → Closed (FR-004) | `start baskets-api` sau bước trên | `20` bước 07 | Đóng mạch khi service phục hồi | `502` (1.1 s) → `502` ×2 rất nhanh → `200`; Elasticsearch: `OnCircuitOpened` 3, `OnCircuitHalfOpened` 3, `OnCircuitClosed` 1 |
| Gateway → BFF (FR-002/FR-004) *(ngoại lệ: tải song song)* | `stop bff-api`; 12 `GET /bff/products` tuần tự, rồi 40 song song | (không có) | `503` fail-fast khi BFF sập | Tuần tự: cả 12 `502` mỗi lần **8.0 giây** (không fail-fast); song song: `504` ×39 + `502` ×1, sau đó `503` trong **6–32 ms**; bật lại `bff-api` thì gateway vẫn `503` cho tới khi hết `ReactivationPeriod` (~30 giây, `200` ở lần thử sau 32 giây) |
| Dọn dẹp | `start` `baskets-api` và `bff-api` | (không có) | Không dữ liệu dư | Đã bật lại cả hai; stack về mặc định |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-007/SC-001 — rà soát điểm gọi lặp lại được (5 test, gồm 2 test tự bảo vệ) | [`ResilienceCoverageTests.cs:25`](../../tests/ResilienceCoverageTests/ResilienceCoverageTests.cs#L25) · [`:40`](../../tests/ResilienceCoverageTests/ResilienceCoverageTests.cs#L40) · [`:57`](../../tests/ResilienceCoverageTests/ResilienceCoverageTests.cs#L57) · [`:86`](../../tests/ResilienceCoverageTests/ResilienceCoverageTests.cs#L86) · [`:117`](../../tests/ResilienceCoverageTests/ResilienceCoverageTests.cs#L117) | `dotnet test tests/ResilienceCoverageTests` |
| FR-005/FR-006/US3 — GET retry đúng 3 lần, POST không bao giờ retry | [`RetryMethodPolicyTests.cs:38`](../../services/bff/tests/Bff.Api.UnitTests/RetryMethodPolicyTests.cs#L38) · [`:59`](../../services/bff/tests/Bff.Api.UnitTests/RetryMethodPolicyTests.cs#L59) | `dotnet test services/bff/tests/Bff.Api.UnitTests --filter FullyQualifiedName~RetryMethodPolicyTests` |
| FR-001 — backchannel identity có pipeline resilience (BFF+4 service) | [`Bff.Api.UnitTests/IdentityBackchannelResilienceTests.cs:46`](../../services/bff/tests/Bff.Api.UnitTests/IdentityBackchannelResilienceTests.cs#L46) | `dotnet test services/bff/tests/Bff.Api.UnitTests --filter FullyQualifiedName~IdentityBackchannelResilienceTests` |
| FR-001 — backchannel identity của gateway | [`Gateway.Api.UnitTests/IdentityBackchannelResilienceTests.cs:35`](../../services/gateway/tests/Gateway.Api.UnitTests/IdentityBackchannelResilienceTests.cs#L35) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter FullyQualifiedName~IdentityBackchannelResilienceTests` |
| FR-002/FR-004 — cấu hình passive health check gateway→BFF (4 test) | [`PassiveHealthCheckConfigurationTests.cs:27`](../../services/gateway/tests/Gateway.Api.UnitTests/PassiveHealthCheckConfigurationTests.cs#L27) · [`:45`](../../services/gateway/tests/Gateway.Api.UnitTests/PassiveHealthCheckConfigurationTests.cs#L45) · [`:67`](../../services/gateway/tests/Gateway.Api.UnitTests/PassiveHealthCheckConfigurationTests.cs#L67) · [`:99`](../../services/gateway/tests/Gateway.Api.UnitTests/PassiveHealthCheckConfigurationTests.cs#L99) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter FullyQualifiedName~PassiveHealthCheckConfigurationTests` |
| FR-002/FR-003/US2 — mạch gateway→BFF mở thật, `503` fail-fast | [`PassiveHealthCheckCircuitBreakerTests.cs:38`](../../services/gateway/tests/Gateway.Api.IntegrationTests/PassiveHealthCheckCircuitBreakerTests.cs#L38) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter FullyQualifiedName~PassiveHealthCheckCircuitBreakerTests` |
| FR-001/SC-002 — BFF→downstream: `502` khi không tới được, `504` khi không trả lời (test của spec 002) | [`DownstreamUnavailableTests.cs:50`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L50) · [`:78`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L78) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter FullyQualifiedName~DownstreamUnavailable` |

**Kết quả lượt QA này (2026-09-26)**: `ResilienceCoverageTests` **5/5**; `Bff.Api.UnitTests` (retry + backchannel) **3/3**; `Gateway.Api.UnitTests` (passive + backchannel) **5/5**;
`Gateway.Api.IntegrationTests` **4/4**; `Bff.Api.IntegrationTests` (downstream + timeout) **8/8** — không đổi sau khi dịch comment.

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán với nhau và với mã thật; cả 3 chính sách (timeout tường minh, retry chỉ `GET`/`HEAD`, circuit breaker) được chứng minh sống bằng dữ liệu thật và bằng Postman + Elasticsearch: `GET` retry đúng 2 lần, POST không retry, chờ tối đa ~3 giây, mạch mở → fail-fast 20–95 ms → half-open → closed khi phục hồi. 25 test tự động xanh.
Ghi chú: (1) ngưỡng mở breaker BFF→service dựa vào mặc định Polly `MinimumThroughput 100` nên ở lưu lượng thường mạch không bao giờ mở (8 GET tuần tự vẫn 3 giây mỗi lần); (2) gateway → BFF cũng chỉ fail-fast khi đủ mẫu lỗi (12 request tuần tự × 8 giây), và sau khi BFF sống lại còn bị chặn ~30 giây;
(3) FR-007 chưa tự phát hiện điểm gọi mới — `Orders.Api → RabbitMQ` (024) chưa timeout/retry/breaker tường minh mà scanner vẫn xanh; (4) `architecture/020` + `technical-debt.md` còn ghi quickstart Bước 6 chưa chạy trong khi `tasks.md` đã hoàn tất. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 020.
