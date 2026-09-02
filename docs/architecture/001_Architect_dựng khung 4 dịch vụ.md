# Kiến trúc: Dựng khung 4 service Parties/Products/Baskets/Orders

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-11, đặc tả tại
[`specs/001-scaffold-service-shells/`](../../specs/001-scaffold-service-shells/). Đây là feature nền
móng đầu tiên của nền tảng — mọi feature backend sau này (002 định tuyến gateway/BFF, 003 tenant
context...) đều xây trực tiếp trên bốn service shell này.

**Trạng thái xác minh**: 51/51 task trong `tasks.md` đã hoàn thành. Ba user story được kiểm thử độc
lập: US1 (mỗi service tự khởi động một mình, health check phản ánh đúng khả năng kết nối database),
US2 (không service nào chạm được dữ liệu service khác — kiểm chứng cấu trúc, không phải runtime
check), US3 (mã nguồn tổ chức theo vertical-slice, xác nhận bằng structure test).

## 1. Kiến trúc tổng thể

Bốn service ASP.NET Core Minimal API độc lập (Parties/Products/Baskets/Orders), mỗi service:

- Một project `src/{Name}.Api` + `tests/{Name}.Api.UnitTests` + `tests/{Name}.Api.IntegrationTests`,
  cấu trúc giống hệt nhau (`services/{name}/src/` + `services/{name}/tests/`).
- Một `DbContext` EF Core riêng, trỏ tới một database/schema chỉ chính service đó có quyền truy cập
  (constitution Principle I — "mỗi service PHẢI sở hữu dữ liệu của mình độc quyền").
- Tham chiếu duy nhất một dependency dùng chung: `shared/ServiceDefaults` (OpenTelemetry — constitution
  Principle VII). Không có thư viện chia sẻ nào khác giữa 4 service ở giai đoạn này.
- Hai health endpoint tách biệt: `/health/live` (tiến trình còn sống) và `/health/ready` (kèm kiểm
  tra kết nối database thật qua `AddDbContextCheck<T>()`) — xem
  [`contracts/health-check.md`](../../specs/001-scaffold-service-shells/contracts/health-check.md).

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

- **API style**: ASP.NET Core Minimal APIs — nhất quán với lựa chọn đã có cho BFF (ADR-0003), không
  đánh giá lại từ đầu vì không có lợi ích khi dùng hai phong cách khác nhau giữa BFF và service.
- **Test framework**: xUnit — chuẩn mặc định hệ sinh thái .NET hiện tại, có hỗ trợ Testcontainers
  tốt nhất, khớp với giả định sẵn có của ADR-0006 (Pact .NET SDK).
- **Health check**: middleware `Microsoft.Extensions.Diagnostics.HealthChecks` có sẵn của framework,
  KHÔNG dùng một endpoint `/health` gộp chung — vì gộp chung không thể phân biệt "tiến trình sống
  nhưng database chưa kết nối được" (liveness fail → Kubernetes restart pod; readiness fail → chỉ
  ngừng route traffic, hai phản ứng khác nhau).

## 3. Ranh giới dữ liệu — đảm bảo cấu trúc, không phải quy ước

`data-model.md` ghi rõ: tính năng này **không có entity nghiệp vụ nào** — Party/Product/Basket/Order
thật sự sẽ do các feature sau xây. Điều tính năng này thiết lập là **ranh giới sở hữu dữ liệu**: không
`DbContext`/connection string nào của một service được phép trỏ tới database của service khác (spec
FR-004/FR-005). Đây được kiểm chứng bằng cách thử kết nối chéo và xác nhận không có credential/route
nào cho phép — không phải một runtime check, mà một đảm bảo có thể lặp lại kiểm tra được.

## 4. Giới hạn phạm vi đã biết

- Tenant-keyed schema/connection resolution (Principle V) **không** thuộc phạm vi này — mỗi
  `DbContext` hiện trỏ tới một connection mặc định duy nhất, có cấu trúc sẵn để một connection
  resolver theo tenant thay thế sau này mà không cần thiết kế lại (giao cho SCRUM-12 /
  [003-stub-identity-tenant-context](../../specs/003-stub-identity-tenant-context/)).
- Outbox table của Orders (constitution Principle IV) chưa được tạo trong feature này — sẽ xuất hiện
  cùng lúc với event đầu tiên nền tảng thực sự phát hành.
- Chạy đồng thời cả 4 service bằng một lệnh duy nhất KHÔNG nằm trong phạm vi này (đó là SCRUM-15 /
  [005-one-command-local-run](../../specs/005-one-command-local-run/)) — phạm vi ở đây chỉ là mỗi
  service tự chạy độc lập được.

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/001-scaffold-service-shells-component.drawio`](../diagrams/001-scaffold-service-shells-component.drawio)
- Sơ đồ trình tự (khởi động một service, health check phân biệt liveness/readiness):
  [`docs/diagrams/001-scaffold-service-shells-sequence.drawio`](../diagrams/001-scaffold-service-shells-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/001-scaffold-service-shells-flow-nghiep-vu.drawio`](../diagrams/001-scaffold-service-shells-flow-nghiep-vu.drawio)

Sơ đồ kiến trúc tổng thể của nền tảng (bao gồm cả bốn service này trong bối cảnh gateway/BFF) xem
[`docs/diagrams/kien-truc-3-nhom.drawio`](../diagrams/kien-truc-3-nhom.drawio).
