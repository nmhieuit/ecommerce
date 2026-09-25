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

**Kết quả lượt QA này (2026-09-24)**: `ResilienceCoverageTests` **5/5**; `Bff.Api.UnitTests` (retry + backchannel) **3/3**; `Gateway.Api.UnitTests` (passive + backchannel) **5/5**;
`Gateway.Api.IntegrationTests` **4/4**; `Bff.Api.IntegrationTests` (downstream + timeout) **8/8** — không đổi sau khi dịch comment.

### Thủ công — Jira Test Scenario 2 trên stack Docker (đã tự làm sống)

```bash
docker compose -f docker-compose.local.yml stop baskets-api
# 150 request song song 50 (≥100 mẫu / 10 s để vượt MinimumThroughput mặc định của Polly):
seq 1 150 | xargs -P 50 -I{} curl -s -o /dev/null -w "%{http_code} %{time_total}\n" \
  http://localhost:5300/bff/basket -H "Authorization: Bearer <token>"
# sau đó: mỗi GET đơn lẻ phải trả 502 trong vài chục mili giây (mạch đang mở)
docker compose -f docker-compose.local.yml start baskets-api   # mạch half-open rồi closed
curl "http://localhost:9200/logs-generic.otel-default*/_search?size=0" -H 'Content-Type: application/json' \
  -d '{"query":{"match":{"scope.name":"Polly"}},"aggs":{"ev":{"terms":{"field":"attributes.EventName"}}}}'
```

| Bước | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|
| 8 × `GET /bff/basket` tuần tự khi `baskets-api` dừng | `504` bị chặn ở `TotalRequestTimeout 3s` (SC-002) | Cả 8 đều `504` sau 3.2–4.0 s; **mạch KHÔNG mở** (xem QA_Debt) |
| Đếm sự kiện Polly theo correlation ID | Đúng 2 retry/request (US3-KB1) | 8 GET → **16 `OnRetry`** — đúng |
| 3 × `POST /bff/basket/items` | POST không bao giờ retry (FR-006) | 0 `OnRetry` từ pipeline `BasketsApi` — đúng |
| 150 × `GET /bff/basket`, song song 50, rồi GET đơn lẻ | Mạch mở, fail-fast (SC-003, US2-KB2) | `502`×116 + `504`×34; sau đó `502` trong **36–77 ms** |
| `docker compose start baskets-api`, gọi lại | Opened → HalfOpened → Closed (US2-KB3, FR-004) | Elastic ghi đủ `OnCircuitOpened` ×3, `OnCircuitHalfOpened` ×3, `OnCircuitClosed` ×1 — chu kỳ trọn vẹn |
| Sự kiện Polly trong Elasticsearch (FR-008) | Có `OnTimeout`/`OnRetry`/sự kiện mạch | `OnTimeout` 196, `OnRetry` 155 + đủ 3 sự kiện mạch |

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán với nhau và với mã thật; cả 3 chính sách (timeout tường minh, retry chỉ `GET`/`HEAD`, circuit breaker) đã được chứng minh sống bằng dữ liệu thật
(retry đúng 2 lần, POST không retry, chờ tối đa ~3 s, mạch mở → fail-fast → half-open → closed, sự kiện Polly tới Elasticsearch). 25 test tự động xanh. Ghi chú: (1) ngưỡng mở breaker
BFF→service dựa vào mặc định Polly `MinimumThroughput 100` nên ở lưu lượng thường mạch không bao giờ mở; (2) FR-007 chưa tự phát hiện điểm gọi mới — `Orders.Api → RabbitMQ` (024)
chưa timeout/retry/breaker tường minh mà scanner vẫn xanh; (3) `architecture/020` + `technical-debt.md` còn ghi quickstart Bước 6 chưa chạy trong khi `tasks.md` đã hoàn tất. Chi tiết và hướng vá:
[QA_Debt.md](QA_Debt.md) mục 020.
