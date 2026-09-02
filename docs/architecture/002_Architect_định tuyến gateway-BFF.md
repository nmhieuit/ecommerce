# Kiến trúc: Định tuyến Gateway → BFF cho Products/Baskets/Orders/Parties

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-13 ("[WALK-1] Wire gateway → BFF routing"), đặc tả tại
[`specs/002-gateway-bff-routing/`](../../specs/002-gateway-bff-routing/), xây trên nền bốn service
shell của [001-scaffold-service-shells](../../specs/001-scaffold-service-shells/). Quyết định kiến
trúc gốc: [ADR-0002](../adr/0002-api-gateway.md) (chọn YARP làm gateway) và
[ADR-0003](../adr/0003-bff-implementation-pattern.md) (chọn Minimal APIs cho BFF).

**Trạng thái xác minh**: 65/65 task trong `tasks.md` đã hoàn thành, gồm cả một pha xử lý blocker giữa
chừng (mục 3 bên dưới) và một lỗi thật đã phát hiện + sửa khi chạy thử toàn luồng (mục 4).

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

## 3. Blocker giữa chừng — và cách được giải quyết

Tại thời điểm viết `spec.md`, một giả định bị sai: cả 4 domain service (từ 001) chỉ có 2 health probe,
**không có bất kỳ endpoint dữ liệu nào** — không có gì để BFF proxy tới. Điều này khiến FR-002/FR-003
/FR-004 không thể thoả mãn đầu-cuối cho tới khi có quyết định phạm vi rõ ràng.

**Quyết định phạm vi đã chốt** (ghi trong `tasks.md`, Phase 3 được thêm sau khi phát hiện blocker):
cả 4 domain service được bổ sung một bề mặt đọc (read surface) tối thiểu — đủ để route
product-listing (FR-004/SC-002) và các route còn lại hoạt động thật, thay vì trì hoãn dữ liệu thật
sang một feature khác. Phase 1-2 (khung Gateway/BFF) đã hoàn thành trước khi blocker này xuất hiện,
giữ nguyên số task cũ để truy vết được; phần đánh số lại bắt đầu từ sau T013.

## 4. Lỗi thật đã phát hiện và sửa khi chạy thử toàn luồng

**Mã số theo dõi (correlation ID) không sống sót qua chặng gateway.** Khi chạy thử `quickstart.md`
Scenario 4 đầu-cuối, header phản hồi `X-Correlation-Id` và `correlationId` trong body lỗi mang **hai
giá trị khác nhau**. Nguyên nhân: `CorrelationIdMiddleware` ghi ID đã xác định vào `HttpContext.Items`
và header response, nhưng KHÔNG ghi vào header của chính request — trong khi YARP chỉ forward header
request đã có sẵn. Kết quả: một ID do gateway sinh ra chỉ tồn tại ở response của chính gateway, còn
BFF tự sinh một ID khác không liên quan — vi phạm trực tiếp constitution Principle VII ("sinh ra ở
edge và lan truyền qua mọi lệnh gọi đồng bộ"). **Đã vá bằng đúng một dòng** trong
`shared/ServiceDefaults/CorrelationIdMiddleware.cs`, có test hồi quy riêng
(`Gateway.Api.IntegrationTests/CorrelationIdPropagationTests`) và được xác nhận không phá vỡ 14
project test khác dùng chung middleware này (vì đây là thư viện chia sẻ). Trường hợp caller tự cung
cấp ID đã hoạt động đúng từ trước — chỉ trường hợp ID do hệ thống tự sinh mới bị lỗi.

**Phát hiện về cold-start đã tự giải quyết, không cần sửa code.** Lần đo đầu tiên cho thấy request
đầu tiên qua gateway → BFF → products mất hơn 3 giây (vượt ngân sách). Điều tra lại cho thấy đây là
hiện tượng của bộ đo thử (chờ `/health/live` thay vì `/health/ready`) — Kubernetes thực tế chỉ mở
traffic khi `/health/ready` sẵn sàng, và với domain service, readiness đã mở kết nối database thật từ
trước (làm nóng EF model + connection pool). Đo lại đúng cách: request đầu tiên trả về **200 trong
1.07 giây** — trong ngân sách 3 giây. Cổng readiness sẵn có của nền tảng đã là biện pháp giảm thiểu
đủ, không cần thay đổi timeout nào.

## 5. Sơ đồ

Sơ đồ định tuyến gateway → BFF → 4 domain service:
[`docs/diagrams/002-gateway-bff-routing-component.drawio`](../diagrams/002-gateway-bff-routing-component.drawio).
