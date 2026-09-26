# QA: Consumer-driven contract test giữa BFF/service

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **3 boundary HTTP** (BFF↔products, BFF↔baskets, BFF↔orders): BFF (consumer) ghi kỳ vọng thành file Pact;
   service (provider) tự verify hành vi HTTP thật của chính mình đối chiếu file đó, trong build của chính provider.
2. **1 cặp event thí điểm** (`BasketCheckedOut`, baskets producer ↔ orders consumer): verify payload thật do
   `BasketCheckedOutMapper.ToEvent` dựng ra, không qua MassTransit/broker.
3. **Trường thêm mới mà consumer không đọc không làm test đỏ** (tolerant-reader, FR-007).
4. **4 boundary liệt kê và đối chiếu chéo được**: `pacts/README.md` có bảng 4 dòng, `tests/ContractCoverageTests`
   quét filesystem đối chiếu đúng bảng đó.
5. **Gỡ 1 test bắt buộc thì cổng coverage bắt được**, nêu tên boundary.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — làm "provider verification" bằng Postman trên hệ thống chạy thật

Spec 011 là kiểm thử hợp đồng nên **không có công tắc cấu hình**. Điều làm tay được là gọi thẳng 3 service phía sau BFF và kiểm đúng những gì BFF đã ghi trong `pacts/bff-*.json` (mã trạng thái và kiểu từng trường — đặc biệt `price`/`total`/`unitPrice` phải là **số**). Dựng stack rồi bấm Postman:

```bash
docker compose -f docker-compose.local.yml up -d --wait products-api baskets-api orders-api   # kéo theo identity-api và DB tương ứng
```

Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền → 01 Lấy access token` một lần, rồi folder
**`11 - Hợp đồng BFF ↔ service (Pact)`** (bước 01 → 08; bước 07 tạo 1 đơn thật). Pact ở [`pacts/`](../../pacts/README.md) liệt kê 4 boundary.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| SC-001 — đúng 4 boundary | (không có công tắc) — mở thư mục `pacts/` | (không có) | 4 file, khớp bảng `pacts/README.md` | Đúng: `bff-products`, `bff-baskets`, `bff-orders`, `orders-basketcheckedout` |
| products khớp pact `bff-products` | (không có công tắc) | `11` bước 01 | `200`; `items` ≥ 1 có id (UUID), name (chuỗi), price (số); page/pageSize/totalCount là số | Đúng, 1 assertion xanh |
| baskets khớp pact `bff-baskets` (thêm, đọc, dọn, dọn lần hai) | (không có công tắc) | `11` bước 02 → 06 | `200` hình dạng giỏ (id UUID, customerRef, items[], total số); dọn `204`; dọn lần hai `409` kèm `error` chuỗi | Đúng cả 4 request (giỏ `unitPrice`/`lineTotal`/`total` đều kiểu số) |
| orders khớp pact `bff-orders` (đặt, đọc lại) | (không có công tắc) | `11` bước 07 → 08 | `201` / `200`, thân có id UUID, placedAtUtc date-time, total số | Đúng |
| **Toàn folder** | (không có công tắc) | `11` bước 01 → 08 | Mọi assertion xanh | **10/10 xanh** |
| SC-002 — provider tự bắt thay đổi phá vỡ *(ngoại lệ: sửa mã, dựng lại image)* | Đổi `ProductResponse(Guid Id, string Name, decimal Price)` thành `…decimal UnitPrice)` trong `CatalogEndpoints.cs`, `docker compose … up -d --build products-api`; xong `git checkout --` và dựng lại | Chạy lại folder `11` | Bước 01 đỏ vì lệch hợp đồng | **Đỏ đúng bước 01** (thiếu `price`); provider test đỏ `$.items[0] -> Actual map is missing the following keys: price`; khôi phục + dựng lại → 10/10 xanh |
| SC-003/SC-004 — gỡ 1 test bắt buộc *(ngoại lệ: thao tác file)* | Đổi tên `ProductsProviderPactTests.cs` thành `.bak`, chạy `ContractCoverageTests`; khôi phục | (không có) | Đỏ nêu tên boundary | Đỏ đúng: `Boundary = BFF-products`, `MissingPath = …/ProductsProviderPactTests.cs`; khôi phục → 6/6 xanh |
| Hợp đồng sự kiện `BasketCheckedOut` *(ngoại lệ: chưa có luồng thật)* | — | (không có) | Có luồng phát/nhận | Không kiểm tay được: chỉ có `BasketCheckedOutMapper` dựng payload cho provider test; xem QA_Debt mục 008 |
| Dọn dẹp | Trả compose/`.env` về mặc định (không đổi gì) | (không có) | Không dữ liệu dư; `pacts/` sạch | Đã xoá đơn do QA tạo (giữ đơn gốc QA 017); chạy consumer làm 3 file pact bị `M` → `git checkout -- pacts`; `products-api` dựng lại từ mã sạch |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Cần Docker Desktop chạy (SQL Server thật cho 3 provider phía HTTP); consumer-side và event pilot không cần.

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002 — BFF kỳ vọng gì ở products | [`ProductsConsumerPactTests.cs:30`](../../services/bff/tests/Bff.Api.ContractTests/ProductsConsumerPactTests.cs#L30) — `GetProducts_DependsOnIdNameAndPrice` | `dotnet test services/bff/tests/Bff.Api.ContractTests --filter GetProducts_DependsOnIdNameAndPrice` |
| FR-001/FR-002 — BFF kỳ vọng gì ở baskets | [`BasketsConsumerPactTests.cs:46`](../../services/bff/tests/Bff.Api.ContractTests/BasketsConsumerPactTests.cs#L46) — `BasketInteractions_DependOnIdCustomerRefItemsAndTotal` | `dotnet test services/bff/tests/Bff.Api.ContractTests --filter BasketInteractions_DependOnIdCustomerRefItemsAndTotal` |
| FR-001/FR-002 — BFF kỳ vọng gì ở orders | [`OrdersConsumerPactTests.cs:34`](../../services/bff/tests/Bff.Api.ContractTests/OrdersConsumerPactTests.cs#L34) — `OrderInteractions_DependOnIdPlacedAtUtcAndTotal` | `dotnet test services/bff/tests/Bff.Api.ContractTests --filter OrderInteractions_DependOnIdPlacedAtUtcAndTotal` |
| FR-001/FR-003/FR-005 — products tự verify | [`ProductsProviderPactTests.cs:41`](../../services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs#L41) — `CatalogResponses_SatisfyTheBffsRecordedExpectations` | `dotnet test services/products/tests/Products.Api.ContractTests --filter CatalogResponses_SatisfyTheBffsRecordedExpectations` |
| FR-002/FR-003/FR-005 — baskets tự verify | [`BasketsProviderPactTests.cs:37`](../../services/baskets/tests/Baskets.Api.ContractTests/BasketsProviderPactTests.cs#L37) — `BasketResponses_SatisfyTheBffsRecordedExpectations` | `dotnet test services/baskets/tests/Baskets.Api.ContractTests --filter BasketResponses_SatisfyTheBffsRecordedExpectations` |
| FR-003/FR-005 — orders tự verify | [`OrdersProviderPactTests.cs:45`](../../services/orders/tests/Orders.Api.ContractTests/OrdersProviderPactTests.cs#L45) — `OrderResponses_SatisfyTheBffsRecordedExpectations` | `dotnet test services/orders/tests/Orders.Api.ContractTests --filter OrderResponses_SatisfyTheBffsRecordedExpectations` |
| FR-004 — orders kỳ vọng gì ở event `BasketCheckedOut` | [`BasketCheckedOutConsumerPactTests.cs:41`](../../services/orders/tests/Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs#L41) — `BasketCheckedOut_DependsOnTheIdentifiersTenantLinesAndTotal` | `dotnet test services/orders/tests/Orders.Api.ContractTests --filter BasketCheckedOut_DependsOnTheIdentifiersTenantLinesAndTotal` |
| FR-004/FR-005/FR-006 — baskets tự verify payload event | [`BasketCheckedOutProviderPactTests.cs:46`](../../services/baskets/tests/Baskets.Api.ContractTests/BasketCheckedOutProviderPactTests.cs#L46) — `CheckedOutPayload_SatisfiesTheOrdersServicesRecordedExpectations` | `dotnet test services/baskets/tests/Baskets.Api.ContractTests --filter CheckedOutPayload_SatisfiesTheOrdersServicesRecordedExpectations` |
| FR-008/SC-001/SC-003 — 4 boundary có đủ pact + test verify | [`ContractCoverageTests.cs:22`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L22) · [`:44`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L44) | `dotnet test tests/ContractCoverageTests --filter "FullyQualifiedName~AllThinSliceBoundaries\|FullyQualifiedName~Scan_ActuallyExamines"` |
| FR-009/SC-004 — scanner tự bảo vệ | [`ContractCoverageTests.cs:70`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L70) (3 case) · [`:111`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L111) | `dotnet test tests/ContractCoverageTests --filter "FullyQualifiedName~Scan_FlagsABoundaryMissingEitherFile\|FullyQualifiedName~Scan_ReportsNoViolations"` |

**Kết quả lượt QA này (2026-09-26)**: consumer BFF **3/3**, consumer event **1/1**, provider baskets **2/2**, provider orders **2/2**, `ContractCoverageTests` **6/6**; provider products **1/1** ở 3 lần chạy riêng nhưng **đỏ 1 lần** khi chạy nối tiếp sau các consumer (không tái hiện, xem QA_Debt). Mỗi lần chạy consumer BFF làm 3 file pact bị `M` (token giả ký lại) — cần `git checkout -- pacts`.

## Kết luận

**PASS kèm ghi chú.** 4 nguồn mô tả đúng thiết kế/phạm vi; 3 provider HTTP đã xanh trở lại sau bản vá 2026-09-22; trên hệ thống thật, cả 3 service trả đúng hình dạng mà BFF dựa vào (10/10 Postman) và 1 thay đổi phá vỡ thật (`Price` → `UnitPrice`) bị bắt cả ở Postman lẫn provider test, hoàn tác thì xanh lại. Ghi chú: (1) 1 lần đỏ chưa giải thích ở provider products; (2) mỗi lần chạy consumer làm bẩn 3 file pact; (3) hợp đồng sự kiện chưa có luồng thật để kiểm tay; (4) lịch sử: lượt 2026-09-22 từng FAIL vì 401 nền (đã vá). Chi tiết: [QA_Debt.md](QA_Debt.md) mục 011.
