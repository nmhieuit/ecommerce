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

Cần Docker Desktop chạy (SQL Server thật cho 3 provider host phía HTTP, research.md Decision 5); phần consumer-side
và event pilot không cần. Gần như nguyên văn `quickstart.md`, khác ở chỗ đã tự chạy thật và ghi kết quả đo được.

### Thủ công — chạy đúng như spec mô tả

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Sinh pact phía consumer | `dotnet test services/bff/tests/Bff.Api.ContractTests` và consumer event của orders | Ghi 3 file pact + pact event | 3/3 PASS; ghi `bff-products.json`, `bff-baskets.json`, `bff-orders.json`; consumer event PASS (không cần Docker) |
| SC-001 — đúng 4 boundary | `find pacts -name "*.json"` | 4 file | Đúng 4 file (`bff-products`, `bff-baskets`, `bff-orders`, `orders-basketcheckedout`), khớp `pacts/README.md` |
| SC-002 — build bên phát tự bắt lỗi | 4 lệnh `dotnet test` của 3 provider HTTP + provider event | PASS | **Lượt 2026-09-22 đỏ cả 4** (401 nền ở 3 provider HTTP; xung đột cổng Windows/Docker ở provider event); **sau khi vá 2026-09-23: 12/12 PASS** — xem QA_Debt |
| SC-002 — cổng còn tác dụng sau khi vá | Đổi `ProductResponse.Price` thành `UnitPrice`, chạy `Products.Api.ContractTests` | Đỏ vì lệch hợp đồng, không phải vì auth | Đỏ đúng: `status code 200 OK` + `Actual map is missing the following keys: price`; khôi phục → xanh |
| SC-003/SC-004 — gỡ 1 test bắt buộc | Đổi tên `ProductsProviderPactTests.cs` thành `.bak`, chạy `ContractCoverageTests` | Đỏ nêu tên boundary | Đỏ đúng: `Boundary = BFF-products`, `MissingPath = …/ProductsProviderPactTests.cs`; khôi phục (`mv`) → PASS, `git status` sạch |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

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

**Kết quả lượt QA này (2026-09-23, sau khi vá)**: toàn bộ **12 test hợp đồng PASS** + cổng coverage PASS xuyên suốt.

## Kết luận

**PASS** (cập nhật 2026-09-23). 4 nguồn mô tả đúng thiết kế/phạm vi, không mâu thuẫn nhau. Lượt 2026-09-22 từng hạ xuống **FAIL** vì
(1) cả 3 provider HTTP đỏ 401 nền do spec 015 làm hỏng patch của 014, và (2) provider event vướng xung đột cổng Windows/Docker Desktop;
(1) đã vá ngay trong ngày, còn (2) chạy lại thì xanh nhưng vẫn là rủi ro chập chờn trên máy Windows. Chi tiết nguyên nhân gốc,
hướng vá đã chọn và phát hiện phụ (interaction thừa trong `pacts/bff-products.json`): [QA_Debt.md](QA_Debt.md) mục 011.
