# Kiến trúc: Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-33 ("[RESILIENCE-4] Audit for N+1 queries, unbounded queries, missing
pagination"), đặc tả tại
[`specs/023-audit-n1-unbounded-pagination/`](../../specs/023-audit-n1-unbounded-pagination/). 7 quyết
định kiến trúc ở [`research.md`](../../specs/023-audit-n1-unbounded-pagination/research.md).

**Trạng thái xác minh**: 25/25 task hoàn thành `[X]`. Ba user story xác thực độc lập bằng test thật
qua Testcontainers SQL Server (sau khi Docker Desktop khởi động lại giữa phiên): 23/23
`Products.Api.IntegrationTests`, 22/22 `Bff.Api.UnitTests`, 8/8 `QueryCoverageTests`, 1/1
`BasketQueryCountTests`. 1 bug thật phát hiện khi triển khai và 1 lần flake hạ tầng không liên quan —
xem [technical-debt.md](technical-debt.md).

## 1. Kiểm kê thật trước khi thiết kế — điểm nghiêm trọng nhất không phải N+1 kiểu cổ điển

`research.md` Decision 1 kiểm kê toàn bộ endpoint danh sách và truy vấn quan hệ trong 4 service + BFF
trước khi thiết kế bất kỳ gì:

| # | Vị trí | Trạng thái TRƯỚC | Trạng thái SAU |
|---|---|---|---|
| 1 | `GET /products` (products) | Không phân trang — trả toàn bộ bảng | Phân trang `page`/`pageSize`, mặc định 20 |
| 2 | `GET /bff/products` (BFF) | Kế thừa #1, forward toàn bộ | Forward `page`/`pageSize`, envelope thêm `Page`/`PageSize`/`TotalCount` |
| 3 | `GET /orders/{id}`, `GET /parties/{id}` | Chỉ get-by-id, không có endpoint danh sách nào | Không đổi — không có gì để phân trang |
| 4 | `GET /baskets/{id}` (`.Include(LineItems)`) | Eager load đúng cách, KHÔNG N+1 ở tầng EF Core — nhưng chưa từng được rà soát bằng log truy vấn | Khoá lại thành regression test qua `QueryCountInterceptor` |
| 5 | `BasketsEndpoints.ToResponseAsync`/`POST /basket/items` (BFF) | Gọi `products.GetProductsAsync()` — TOÀN BỘ catalog mỗi lần render basket, chỉ để join tên sản phẩm | Gọi `GetProductsByIdsAsync(ids)` — đúng 1 lời gọi HTTP bất kể basket có bao nhiêu dòng |

Khoảng hở nặng nhất không phải N+1 kiểu EF Core cổ điển (N truy vấn SQL nhỏ) mà là #5 — một mẫu hình
"over-fetch xuyên service": BFF tải nguyên catalog qua HTTP chỉ để join tên sản phẩm, tệ hơn khi
catalog lớn dần và không tự nhiên tốt hơn khi basket có nhiều dòng.

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 2 | Phân trang offset-based (`page`/`pageSize`), không cursor — catalog đọc nhiều/ghi hiếm, đã có `OrderBy(Name)` ổn định sẵn làm điều kiện tiên quyết |
| 3 | `DefaultPageSize = 20`, `MaxPageSize = 100` — hằng số cố định trong code, không đọc từ configuration (không ai yêu cầu đổi được không cần build lại) |
| 4 | Filter `ids` (danh sách GUID phân tách dấu phẩy) chặn thẳng trên `GET /products` — sửa over-fetch mà không thêm endpoint `GET /products/{id}` hay `POST /products/batch` riêng |
| 5 | `GET /products` đổi từ mảng trần sang envelope `PagedProductsResponse` — chấp nhận là 1 phần bắt buộc của FR-001/FR-004 (đúng 1 consumer là BFF, sinh lại cùng PR), không phải ngoại lệ Principle II cần versioning riêng |
| 6 | `QueryCountInterceptor` (`DbCommandInterceptor` đếm `ReaderExecuting`) — hạ tầng test dùng chung mới ở `shared/IntegrationTestSupport`, chỉ đăng ký trong test host, không đụng cấu hình production |
| 7 | `tests/QueryCoverageTests` — đúng khuôn mẫu `ResilienceCoverageTests`: danh sách tường minh (`ExpectedListEndpoints`/`ExpectedBoundedQuerySites`) + marker bắt buộc, populate ngay từ Foundational thay vì dần theo từng story |

## 3. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/023-audit-n1-unbounded-pagination-component.drawio`](../diagrams/023-audit-n1-unbounded-pagination-component.drawio)
- Sơ đồ trình tự (render basket nhiều dòng → 1 lời gọi `ids` thay vì full-fetch, gồm nhánh pageSize
  vượt trần): [`docs/diagrams/023-audit-n1-unbounded-pagination-sequence.drawio`](../diagrams/023-audit-n1-unbounded-pagination-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/023-audit-n1-unbounded-pagination-flow-nghiep-vu.drawio`](../diagrams/023-audit-n1-unbounded-pagination-flow-nghiep-vu.drawio)

1 bug thật phát hiện khi triển khai (gói NuGet thiếu) và giới hạn phạm vi đã biết (giới hạn của khuôn
mẫu scanner): xem [technical-debt.md](technical-debt.md).
