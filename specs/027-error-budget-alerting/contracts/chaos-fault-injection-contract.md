# Contract: Tiêm lỗi 5xx có kiểm soát (`ChaosFaultInjectionMiddleware`)

**Feature**: [../spec.md](../spec.md) (FR-014, FR-015) | **Người tiêu thụ**: `shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests`

Middleware nằm trong `shared/ServiceDefaults`, được `UseServiceDefaults()` gắn ngay sau
`CorrelationIdMiddleware`, nên có mặt ở cả 7 service. Theo đúng mẫu hai lớp chặn của
`specs/025-chaos-pod-kill-latency/contracts/chaos-latency-injection-contract.md`.

## Bất biến

| # | Bất biến |
|---|---|
| 1 | `Chaos:AllowFaultInjection` mặc định `false`; KHÔNG được commit là `true` trong bất kỳ cấu hình nào đại diện cho production. Compose truyền qua `CHAOS_ALLOW_FAULT_INJECTION`, mặc định `false`. |
| 2 | Khi cấu hình `false`: mọi request đi tiếp nguyên vẹn, kể cả có header `X-Chaos-Fault`. |
| 3 | Khi cấu hình `true` và request có header `X-Chaos-Fault: 5xx`: middleware trả `500` ngay, KHÔNG gọi phần còn lại của pipeline (không chạm dữ liệu nghiệp vụ). |
| 4 | Khi cấu hình `true` nhưng header vắng mặt hoặc có giá trị khác `5xx`: request đi tiếp nguyên vẹn. |
| 5 | Response `500` do middleware tạo phải được instrumentation OTel ghi nhận như mọi 5xx thật (span server có `http.response.status_code = 500`). |
| 6 | Middleware không thay đổi bất kỳ response nào khi không tiêm lỗi (FR-015). |
