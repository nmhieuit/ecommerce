# Quickstart: Xác thực phân trang, chặn N+1/over-fetch, và ép trần kích thước trang

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Hướng dẫn này xác thực rằng các khoảng hở nêu ở `plan.md` Summary đã được khép kín, ứng với 3 test
scenario gốc của SCRUM-33 và acceptance scenario của spec.md's User Story 1-3.

## Điều kiện tiên quyết

- .NET 10 SDK đã cài; solution build được (`dotnet build Ecommerce.slnx`).
- SQL Server cục bộ (Testcontainers tự khởi chạy container khi chạy integration test — không cần
  cài đặt tay, xem README gốc).
- Bộ test tĩnh (scanner) dưới đây PASS trước khi thử kịch bản động — điều kiện cần, không phải tuỳ
  chọn.

## Bước 1 — Rà soát tĩnh: `tests/QueryCoverageTests`

```bash
dotnet test tests/QueryCoverageTests
```

**Kỳ vọng**: PASS. Test này đọc trực tiếp danh sách ở
[contracts/query-coverage-inventory-contract.md](./contracts/query-coverage-inventory-contract.md)
và xác nhận mỗi `SourceFile` chứa đủ `RequiredMarkers` — tương đương thao tác rà soát thủ công mã
nguồn cho từng endpoint danh sách và từng điểm gọi tải dữ liệu quan hệ, nhưng lặp lại được trong CI.

## Bước 2 — Jira Test Scenario 1: seed 500 sản phẩm, gọi không tham số

```bash
dotnet test services/products/tests/Products.Api.IntegrationTests --filter FullyQualifiedName~ProductListingPaginationTests
```

**Kỳ vọng**: PASS. Test seed 500 sản phẩm vào SQL Server (Testcontainers), gọi
`GET /products` KHÔNG truyền `page`/`pageSize`, xác nhận `items.length == DefaultPageSize (20)` (không
phải 500) và `totalCount == 500` — khớp nguyên văn Jira Test Scenario 1 và spec User Story 1 Acceptance
Scenario 1/SC-001.

## Bước 3 — Jira Test Scenario 3: `pageSize` lớn bất thường bị ép trần

```bash
dotnet test services/products/tests/Products.Api.UnitTests --filter FullyQualifiedName~ProductListingPaginationTests
```

**Kỳ vọng**: PASS. Gọi `GET /products?pageSize=1000000` xác nhận số bản ghi trả về bị giới hạn ở
`MaxPageSize (100)`, không phản ánh giá trị caller yêu cầu — khớp Jira Test Scenario 3 và spec User
Story 3 Acceptance Scenario 1/SC-003.

## Bước 4 — Jira Test Scenario 2 (tầng BFF): basket nhiều dòng → đúng 1 lời gọi products

```bash
dotnet test services/bff/tests/Bff.Api.UnitTests --filter FullyQualifiedName~ProductLookupBatchingTests
```

**Kỳ vọng**: PASS. Test dựng `BasketsEndpoints` thật với một `ProductsApiClient` giả lập đếm số lần
gọi, render một basket có N dòng khác sản phẩm (N > 1) qua `GET /bff/baskets/{id}`, xác nhận
`ProductsApiClient` chỉ bị gọi ĐÚNG 1 LẦN (dùng `GetProductsByIdsAsync` với đúng tập id của basket),
không phải N lần và không phải một lần fetch toàn bộ catalog như trước khi sửa — khớp nguyên văn Jira
Test Scenario 2 ("confirm one query... not one query per item") ở tầng nơi khoảng hở thực sự tồn tại.

## Bước 5 — Jira Test Scenario 2 (tầng EF Core): khoá hành vi `.Include()` đúng hiện có thành regression test

```bash
dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter FullyQualifiedName~BasketQueryCountTests
```

**Kỳ vọng**: PASS. Test seed một basket với 1 dòng và một basket khác với 5 dòng, tải cả hai qua
`GET /baskets/{id}` thật (SQL Server qua Testcontainers, `QueryCountInterceptor` đếm câu lệnh), xác
nhận số câu lệnh SQL phát sinh KHÔNG tăng theo số dòng (hằng số, không tỷ lệ N) — biến "đã rà soát
bằng log truy vấn, không N+1" từ một khẳng định đọc-code thành một test tự động thật, khớp spec FR-002/
SC-002.

## Bước 6 — Acceptance Scenario bổ sung: basket rỗng không gọi products (Edge Case)

```bash
dotnet test services/bff/tests/Bff.Api.UnitTests --filter FullyQualifiedName~ProductLookupBatchingTests.EmptyBasket
```

**Kỳ vọng**: PASS — một basket 0 dòng render mà KHÔNG gọi `ProductsApiClient` lần nào (hành vi này đã
đúng từ trước — `ToResponseAsync` return sớm khi `Items.Count == 0` — test này khoá nó lại như một
phần của cùng bộ test batching, không phải một khoảng hở mới).

## Dọn dẹp

Không có tài nguyên hạ tầng nào được tạo riêng ngoài container Testcontainers tự dọn khi test kết
thúc. Không có migration nào cần rollback (spec/plan không đổi schema).
