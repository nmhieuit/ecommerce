# QA: Consumer-driven contract test giữa BFF/service

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

**Nguồn đối chiếu**:
[`architecture/011`](../architecture/011_Architect_kiểm%20thử%20hợp%20đồng%20tiêu%20dùng.md) ·
[`development/011`](../development/011_Development_kiểm%20thử%20hợp%20đồng%20tiêu%20dùng.md) ·
[`summary/011`](../summary/011_PO_kiểm%20thử%20hợp%20đồng%20tiêu%20dùng.md) ·
[`spec-summary-vi/011`](../spec-summary-vi/011-consumer-contract-tests.json) — đối chiếu chéo cả 4,
kèm [ADR-0006](../adr/0006-contract-testing-tool.md),
[`specs/011-consumer-contract-tests/{spec,research,quickstart,data-model,tasks}.md`](../../specs/011-consumer-contract-tests/),
[`pacts/README.md`](../../pacts/README.md), và `docs/architecture/technical-debt.md` (mục 023, ghi
nhận cùng lỗi 401 nêu dưới đây) — rồi **tự chạy lại thật** toàn bộ 12 test hợp đồng + cổng coverage,
không suy diễn từ tài liệu.

## Luồng happy-case đã rà soát

1. **3 boundary HTTP** (BFF↔products, BFF↔baskets, BFF↔orders): phía BFF (consumer) ghi lại kỳ vọng
   thành file Pact; phía service (provider) tự verify **hành vi HTTP thật** của chính mình đối chiếu
   file đó, **trong build của chính provider** — không phải build của BFF.
2. **1 cặp event thí điểm** (`BasketCheckedOut`, baskets là producer ↔ orders là consumer): verify
   **payload thật được `BasketCheckedOutMapper.ToEvent` dựng ra**, KHÔNG qua MassTransit/broker (vì
   chưa có publisher/consumer thật — đúng theo Assumptions của spec).
3. **Trường thêm mới mà consumer không đọc tới không làm test đỏ** (tolerant-reader, FR-007) — ví dụ
   `orders` trả `tenantId` nhưng pact `bff-orders.json` không ghi trường đó, và
   `OrdersProviderPactTests` vẫn xanh.
4. **4 boundary liệt kê được và đối chiếu chéo được trong vài phút** — `pacts/README.md` có bảng 4
   dòng, và `tests/ContractCoverageTests` quét filesystem đối chiếu đúng bảng đó, không cần đọc mã
   nguồn từng service.
5. **Gỡ 1 test bắt buộc thì cổng coverage tự động bắt được, nêu tên boundary** — đã tự đổi tên
   `ProductsProviderPactTests.cs` thành `.cs.bak`, chạy `ContractCoverageTests`, xác nhận đỏ đúng như
   `quickstart.md` mô tả, rồi khôi phục.

## ⚠️ 2 phát hiện quan trọng khi tự chạy lại (không có trong tài liệu 4 nguồn)

Tài liệu 4 nguồn đều mô tả 011 là "đã hoàn thành, xác minh bằng kiểm tra tự động" và không có phần nào
ghi rằng bộ test hợp đồng hiện KHÔNG chạy được. Khi tự chạy lại (2026-09-22, Docker Desktop đang chạy,
build sạch), phát hiện:

### A. Cả 3 test provider phía HTTP (products/baskets/orders) đang ĐỎ 100%, từ lâu — vì `RequireAuthorization` thêm sau bởi spec 015

`dotnet test` từng project riêng lẻ đều FAIL:

```text
Products.Api.ContractTests.ProductsProviderPactTests.CatalogResponses_SatisfyTheBffsRecordedExpectations [FAIL]
Baskets.Api.ContractTests.BasketsProviderPactTests.BasketResponses_SatisfyTheBffsRecordedExpectations [FAIL]
Orders.Api.ContractTests.OrdersProviderPactTests.OrderResponses_SatisfyTheBffsRecordedExpectations [FAIL]
```

Log verifier nêu rõ: `expected 200 but was 401`, body trả về
`{"error":"unauthorized","message":"Authentication is required, or the supplied token is invalid."}`
cho MỌI interaction ở cả 3 boundary.

**Nguyên nhân xác nhận bằng `git log`/`git show`**: `PactProviderHost.cs` (cả 3 service) tắt
`AuthorizationOptions.FallbackPolicy` (commit `4940818`, 2026-09-02 — phần vá của spec 014 vì
interaction Pact ghi từ trước khi có xác thực token thật, đúng như `architecture/011` §"Nguồn gốc" đã
nêu). Nhưng 1 ngày sau, commit `be79cbf` "Implement authorization policy tests and enforce
authorization requirements across services" (2026-09-03, spec 015) thêm
`.RequireAuthorization(AuthorizationPolicies.ApiScope)` **thẳng vào từng endpoint**
(`CatalogEndpoints.cs`, `BasketEndpoints.cs`, `OrderEndpoints.cs`) — 1 policy gắn trực tiếp vào
endpoint không bị `FallbackPolicy = null` vô hiệu hoá (`FallbackPolicy` chỉ áp dụng cho endpoint
KHÔNG tự khai policy nào). Từ đó tới nay, patch của 014 không còn tác dụng, và không ai vá lại.

**Đây không phải phát hiện mới của riêng tôi** — `technical-debt.md` mục 023 đã ghi nhận đúng hiện
tượng này ("`Products.Api.ContractTests`/`Baskets.Api.ContractTests` fail 401 trên mọi route") khi
audit spec 023, và xác nhận không phải hồi quy do 023 bằng cách chạy lại trên baseline `10ea911`.
Nhưng phát hiện đó nằm lẫn trong ghi chú của spec 023, **chưa từng được ghi lại dưới chính spec 011 —
nơi test này thuộc về** — nên một người chỉ đọc tài liệu 011 sẽ không biết bộ test hợp đồng phía HTTP
của chính spec này đang hỏng.

**Hệ quả thật**: FR-001/FR-002/FR-003/FR-005 và nửa HTTP của SC-002 — lời hứa cốt lõi của 011 ("build
của bên phát tự bắt lỗi") — **hiện không kiểm chứng được** cho 3/4 boundary, vì bản thân build của bên
phát luôn đỏ bất kể hợp đồng có đúng hay không. Một thay đổi phá vỡ thật (như T013 mô tả) sẽ không thể
phân biệt được với lỗi 401 nền.

### B. Test provider phía event (`BasketCheckedOutProviderPactTests`) không chạy được trên máy này — xung đột cổng Windows/Docker Desktop, không phải lỗi mã

```text
Baskets.Api.ContractTests.BasketCheckedOutProviderPactTests.CheckedOutPayload_SatisfiesTheOrdersServicesRecordedExpectations [FAIL]
PactNet.Exceptions.PactFailureException : Unable to start the internal messaging server
---- System.Net.HttpListenerException : The process cannot access the file because it is being used by another process.
```

PactNet cố mở `http://localhost:49152/pact-messages/`. Chạy
`netsh interface ipv4 show excludedportrange protocol=tcp` xác nhận cổng `49152` nằm NGAY TRONG dải
`49152–49251` mà Hyper-V/mạng WSL2 của Docker Desktop đã giữ trước (excluded port range) — đúng loại
xung đột cổng động đã biết giữa `HttpListener` của .NET và Docker Desktop chạy nền WSL2 trên Windows,
không liên quan gì tới đúng/sai của `BasketCheckedOutMapper`. `tasks.md` T020 xác nhận test này từng
PASS lúc viết feature, nên đây là vấn đề máy/thời điểm, không phải lỗi test hay lỗi mã nguồn.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Cần Docker Desktop chạy (SQL Server thật cho 3 provider host phía HTTP, theo research.md Decision 5).
Không cần cho phần consumer-side hay event pilot — chỉ đọc/ghi JSON cục bộ. Gần như nguyên văn
`quickstart.md` — khác ở chỗ đã **tự chạy thật** và ghi lại đúng kết quả đo được, kể cả khi đỏ.

### Thủ công — sinh pact phía consumer (BFF, orders cho event)

```bash
dotnet test services/bff/tests/Bff.Api.ContractTests
dotnet test services/orders/tests/Orders.Api.ContractTests --filter FullyQualifiedName~BasketCheckedOutConsumerPactTests
```

**Kết quả đo thật**: `Bff.Api.ContractTests` 3/3 PASS, ghi lại `pacts/bff-products.json`,
`pacts/bff-baskets.json`, `pacts/bff-orders.json`. Consumer pact của event pilot cũng PASS (không cần
Docker, không cần HTTP).

### Thủ công — SC-001: 4 file pact, đúng 4 boundary

```bash
find pacts -name "*.json"
```

**Kết quả đo thật**: đúng 4 file (`bff-products.json`, `bff-baskets.json`, `bff-orders.json`,
`orders-basketcheckedout.json`), khớp bảng `pacts/README.md`.

### Thủ công — SC-002: build của bên phát tự bắt lỗi (xem mục "⚠️ 2 phát hiện" ở trên)

```bash
dotnet test services/products/tests/Products.Api.ContractTests
dotnet test services/baskets/tests/Baskets.Api.ContractTests --filter BasketResponses_SatisfyTheBffsRecordedExpectations
dotnet test services/orders/tests/Orders.Api.ContractTests --filter OrderResponses_SatisfyTheBffsRecordedExpectations
dotnet test services/baskets/tests/Baskets.Api.ContractTests --filter CheckedOutPayload_SatisfiesTheOrdersServicesRecordedExpectations
```

**Kết quả đo thật**: cả 4 lệnh trên đều FAIL, vì 2 lý do khác nhau đã nêu ở mục A/B — không phải vì
hợp đồng thật sự bị phá vỡ. Cổng coverage (dưới đây) thì vẫn hoạt động đúng, tách biệt với 2 vấn đề
này vì nó chỉ đọc filesystem.

### Thủ công — SC-003/SC-004: coverage liệt kê được, và gỡ 1 test bắt buộc thì bị bắt

```bash
find pacts -name "*.json" | sed 's#pacts/##; s#\.json$##'
mv services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs \
   services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs.bak
dotnet test tests/ContractCoverageTests --filter AllThinSliceBoundaries_HaveAPactFileAndAVerificationTest
```

**Kết quả đo thật**: danh sách 4 tên khớp bảng boundary. Sau khi đổi tên file, test đỏ đúng như
`quickstart.md` mô tả, nêu rõ `Boundary = BFF-products`,
`MissingPath = services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs`.
Khôi phục bằng `mv ... .cs.bak ... .cs` (không dùng `git checkout` vì đây là đổi tên file, không phải
đổi nội dung), chạy lại → PASS, `git status` sạch.

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002 — BFF kỳ vọng gì ở products | [`ProductsConsumerPactTests.cs:30`](../../services/bff/tests/Bff.Api.ContractTests/ProductsConsumerPactTests.cs#L30) — `GetProducts_DependsOnIdNameAndPrice` | `dotnet test services/bff/tests/Bff.Api.ContractTests --filter GetProducts_DependsOnIdNameAndPrice` |
| FR-001/FR-002 — BFF kỳ vọng gì ở baskets | [`BasketsConsumerPactTests.cs:46`](../../services/bff/tests/Bff.Api.ContractTests/BasketsConsumerPactTests.cs#L46) — `BasketInteractions_DependOnIdCustomerRefItemsAndTotal` | `dotnet test services/bff/tests/Bff.Api.ContractTests --filter BasketInteractions_DependOnIdCustomerRefItemsAndTotal` |
| FR-001/FR-002 — BFF kỳ vọng gì ở orders | [`OrdersConsumerPactTests.cs:34`](../../services/bff/tests/Bff.Api.ContractTests/OrdersConsumerPactTests.cs#L34) — `OrderInteractions_DependOnIdPlacedAtUtcAndTotal` | `dotnet test services/bff/tests/Bff.Api.ContractTests --filter OrderInteractions_DependOnIdPlacedAtUtcAndTotal` |
| FR-001/FR-003/FR-005 — products tự verify (hiện ĐỎ, xem mục A) | [`ProductsProviderPactTests.cs:37`](../../services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs#L37) — `CatalogResponses_SatisfyTheBffsRecordedExpectations` | `dotnet test services/products/tests/Products.Api.ContractTests --filter CatalogResponses_SatisfyTheBffsRecordedExpectations` |
| FR-002/FR-003/FR-005 — baskets tự verify (hiện ĐỎ, xem mục A) | [`BasketsProviderPactTests.cs:33`](../../services/baskets/tests/Baskets.Api.ContractTests/BasketsProviderPactTests.cs#L33) — `BasketResponses_SatisfyTheBffsRecordedExpectations` | `dotnet test services/baskets/tests/Baskets.Api.ContractTests --filter BasketResponses_SatisfyTheBffsRecordedExpectations` |
| FR-003/FR-005 — orders tự verify (hiện ĐỎ, xem mục A) | [`OrdersProviderPactTests.cs:41`](../../services/orders/tests/Orders.Api.ContractTests/OrdersProviderPactTests.cs#L41) — `OrderResponses_SatisfyTheBffsRecordedExpectations` | `dotnet test services/orders/tests/Orders.Api.ContractTests --filter OrderResponses_SatisfyTheBffsRecordedExpectations` |
| FR-004 — orders kỳ vọng gì ở event `BasketCheckedOut` | [`BasketCheckedOutConsumerPactTests.cs:41`](../../services/orders/tests/Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs#L41) — `BasketCheckedOut_DependsOnTheIdentifiersTenantLinesAndTotal` | `dotnet test services/orders/tests/Orders.Api.ContractTests --filter BasketCheckedOut_DependsOnTheIdentifiersTenantLinesAndTotal` |
| FR-004/FR-005/FR-006 — baskets tự verify payload event (hiện ĐỎ trên máy này, xem mục B) | [`BasketCheckedOutProviderPactTests.cs:46`](../../services/baskets/tests/Baskets.Api.ContractTests/BasketCheckedOutProviderPactTests.cs#L46) — `CheckedOutPayload_SatisfiesTheOrdersServicesRecordedExpectations` | `dotnet test services/baskets/tests/Baskets.Api.ContractTests --filter CheckedOutPayload_SatisfiesTheOrdersServicesRecordedExpectations` |
| FR-008/SC-001/SC-003 — 4 boundary có đủ pact + test verify | [`ContractCoverageTests.cs:22`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L22) — `AllThinSliceBoundaries_HaveAPactFileAndAVerificationTest` · [`:44`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L44) — `Scan_ActuallyExaminesAllFourExpectedBoundaries` | `dotnet test tests/ContractCoverageTests --filter "FullyQualifiedName~AllThinSliceBoundaries\|FullyQualifiedName~Scan_ActuallyExamines"` |
| FR-009/SC-004 — scanner tự bảo vệ, phát hiện đúng thiếu pact/thiếu test | [`ContractCoverageTests.cs:70`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L70) — `Scan_FlagsABoundaryMissingEitherFile` (3 case) · [`:111`](../../tests/ContractCoverageTests/ContractCoverageTests.cs#L111) — `Scan_ReportsNoViolations_WhenBothFilesArePresent` | `dotnet test tests/ContractCoverageTests --filter "FullyQualifiedName~Scan_FlagsABoundaryMissingEitherFile\|FullyQualifiedName~Scan_ReportsNoViolations"` |

## Kết luận

**FAIL** (4 nguồn tài liệu mô tả đúng thiết kế/phạm vi, không mâu thuẫn nhau — nhưng hành vi thật ở
mã nguồn hiện tại không còn giữ được lời hứa cốt lõi của spec). Khi tự chạy lại thật (không suy diễn),
phát hiện **3/4 boundary (cả 3 boundary HTTP) hiện không kiểm chứng được**: build của products/
baskets/orders luôn đỏ vì lỗi 401 nền, bất kể hợp đồng có đúng hay không — đúng nghĩa đen phủ định
FR-005 ("build của bên phát MUST fail khi lệch — không được chỉ lộ ở build của bên tiêu thụ") vì hiện
build của bên phát fail LUÔN, không phân biệt được lệch thật với nhiễu. Đây không phải lỗi tài liệu mô
tả sai, mà là hạ tầng test của chính spec 011 đã bị 1 spec sau (015) làm hỏng và chưa ai vá lại kể từ
2026-09-03. Cơ chế coverage-audit (US3, FR-007–009) hoạt động đúng hoàn toàn và độc lập với vấn đề
trên — nếu chỉ tính riêng US3 thì PASS. Cả 2 phát hiện đã ghi vào [QA_Debt.md](QA_Debt.md).
