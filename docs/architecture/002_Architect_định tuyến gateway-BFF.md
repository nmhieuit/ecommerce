# Kiến trúc: Định tuyến Gateway → BFF cho Products/Baskets/Orders/Parties

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-13 ("[WALK-1] Wire gateway → BFF routing"), đặc tả tại
[`specs/002-gateway-bff-routing/`](../../specs/002-gateway-bff-routing/), xây trên nền bốn service
shell của [001-scaffold-service-shells](../../specs/001-scaffold-service-shells/). Quyết định kiến
trúc gốc: [ADR-0002](../adr/0002-api-gateway.md) (chọn YARP làm gateway) và
[ADR-0003](../adr/0003-bff-implementation-pattern.md) (chọn Minimal APIs cho BFF).

**Trạng thái xác minh**: 65/65 task trong `tasks.md` đã hoàn thành, gồm 1 blocker giữa chừng và 2 phát
hiện thật đã sửa khi chạy thử toàn luồng — xem [technical-debt.md](technical-debt.md). Gateway→BFF sau
này còn được bổ sung circuit breaker thật ở spec 020, cũng ghi ở đó.

## 1. Kiến trúc tổng thể

```
Client (SPA)  ──▶  Gateway (YARP)  ──▶  BFF (Minimal API)  ──▶  Products / Baskets / Orders / Parties
                    định tuyến theo         aggregation + shaping        (mỗi service, HttpClient
                    path, không lộ           KHÔNG business logic         có resilience handler chuẩn)
                    topology nội bộ
```

- **Gateway** dùng YARP (ADR-0002), cấu hình khai báo qua `appsettings.json` (research.md Decision 2
  — không code-first), route mọi request tới BFF, không route trực tiếp tới 4 service (research.md
  Decision 1).
- **BFF** dùng ASP.NET Core Minimal API (ADR-0003), gọi 4 service qua `HttpClient` có gắn resilience
  handler chuẩn (research.md Decision 3), chỉ làm nhiệm vụ tổng hợp/định hình response — không chứa
  business logic (spec FR-005, kiểm chứng bằng code review theo SC-004).
- **Lỗi downstream** trả về `ProblemDetails` có cấu trúc, không phải exception thô hay treo vô thời
  hạn (research.md Decision 4).

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| Quyết định | Tóm tắt |
|---|---|
| 1 | Gateway chỉ route tới BFF, không route thẳng tới 4 service — BFF là điểm tổng hợp duy nhất |
| 2 | Cấu hình YARP khai báo qua `appsettings.json`, không code-first |
| 3 | BFF gọi downstream qua `HttpClient` có resilience handler chuẩn (`Microsoft.Extensions.Http.Resilience`) |
| 4 | Lỗi downstream → `ProblemDetails`, không phải exception thô/treo |
| 5 | Integration test của BFF host thật các service downstream trong-tiến-trình, không dùng mock server |
| 6 | Sinh tài liệu OpenAPI bằng document builder có sẵn của ASP.NET Core, không dùng Swashbuckle |
| 7 | Lan truyền header tenant/correlation chỉ một chiều (forward-only) trong phạm vi feature này |

## 3. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/002-gateway-bff-routing-component.drawio`](../diagrams/002-gateway-bff-routing-component.drawio)
- Sơ đồ trình tự (request → route → aggregate, gồm nhánh downstream không khả dụng):
  [`docs/diagrams/002-gateway-bff-routing-sequence.drawio`](../diagrams/002-gateway-bff-routing-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/002-gateway-bff-routing-flow-nghiep-vu.drawio`](../diagrams/002-gateway-bff-routing-flow-nghiep-vu.drawio)
