# QA: TDD hồi tố giỏ hàng và đơn hàng

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Khác mọi spec trước: đây không phải 1 feature sinh code mới. Bản thân spec tự tuyên bố "không có gì
cần sửa" (research.md Decision 1) — mã tính giá giỏ hàng/tạo đơn đã đúng và đã có test bảo vệ từ
trước; việc "làm" của spec 009 là **chứng minh lại bằng thực nghiệm** (cố ý gỡ từng quy tắc, xác nhận
test đỏ, khôi phục) và ghi lại kỷ luật commit cho tương lai. Vì vậy QA ở đây rà lại chính quy trình chứng
minh đó, và quan sát 6 quy tắc tính tiền/tạo đơn trên hệ thống chạy thật.

## Luồng happy-case đã rà soát

1. **6 quy tắc (FR-001–006)**: số lượng ≥ 1 (Basket.AddItem), giữ giá đã chốt lúc thêm đầu tiên, gộp dòng cùng sản phẩm,
   chặn đơn 0 dòng, chặn dòng số lượng/giá không hợp lệ, tổng đơn do server tự tính (Order.PlaceFrom).
2. **Bảo vệ 2 lớp**: mỗi quy tắc có unit test ở tầng domain; cố ý gỡ từng guard thì đúng test tương ứng chuyển đỏ (SC-001, SC-004).
3. **Lịch sử commit**: implementation và unit test của 4 file cốt lõi nằm gộp trong cùng commit, không có test tới muộn (SC-002).
4. **Đặt đơn từ giỏ rỗng bị chặn ở cả tầng domain (`Order.PlaceFrom`) lẫn tầng HTTP (`POST /orders`)** (SC-003).
5. `docs/engineering/test-first-commits.md` tồn tại, đã commit, khớp research.md Decision 3; không có thay đổi production code nào sống sót.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — bấm Postman trên hệ thống chạy thật

**Không có công tắc cấu hình** cho spec này: 6 quy tắc nằm trong mã domain (`Basket.AddItem`, `Order.PlaceFrom`), không đổi được bằng `.env`/compose.
Việc cần làm chỉ là dựng stack và bấm Postman:

```bash
docker compose -f docker-compose.local.yml up -d --wait baskets-api orders-api   # kéo theo identity-api và DB tương ứng
```

Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**;
chạy `00 - Xác thực & phân quyền → 01 Lấy access token` một lần, rồi folder **`09 - Quy tắc giỏ hàng & đơn hàng (TDD hồi tố)`** (bước 01 → 10; bước 01 và 06 dọn giỏ nên chạy lại được; bước 10 tạo thêm 1 đơn thật).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| FR-001 — số lượng < 1 bị chặn | (không có công tắc) | `09` bước 02 (số lượng -1) · `Basket → Thêm hàng với số lượng 0 bị từ chối` | `400` "Quantity must be at least 1" | Đúng `400` cả hai ca |
| FR-002 + FR-003 — gộp dòng, giữ giá đã chốt | (không có công tắc) | `09` bước 03 (Notebook 12.50 × 1) rồi bước 04 (Notebook **20.00** × 2) | Vẫn 1 dòng, số lượng 3, giá **12.50**, tổng 37.50 | Đúng: `items` có 1 dòng, `quantity: 3`, `unitPrice: 12.50`, `total: 37.50` (không phải 60.00) |
| Giá âm ở tầng HTTP của baskets | (không có công tắc) | `09` bước 05 | `400` "Unit price cannot be negative" | Đúng `400` |
| FR-004 — đơn 0 dòng bị chặn | (không có công tắc) | `09` bước 07 · `Order → Đặt hàng không có dòng nào bị từ chối` | `400` "An order needs at least one line." | Đúng `400` |
| FR-005 — dòng số lượng 0 / giá âm bị chặn | (không có công tắc) | `09` bước 08 và 09 · `Order → Đặt hàng với số lượng 0 bị từ chối` | `400` | Đúng `400` cả hai. **Ghi nhận**: thân `400` là thông điệp ngoại lệ thô, vd `lines ('0') must be greater than or equal to '1'. (Parameter 'lines')` (xem QA_Debt) |
| FR-006 — tổng do server tự tính | (không có công tắc) | `09` bước 10 (3 dòng, body cố ý gửi `"total": 1`) · `Order → Đặt hàng trực tiếp` | `201`, tổng 203.25 = 2×12.50 + 1×34.25 + 3×48.00 | Đúng `201`, `total: 203.25` — `"total": 1` của caller bị bỏ qua |
| SC-001/SC-004 — gỡ guard FR-006 rồi khôi phục *(ngoại lệ: sửa mã domain, dựng lại image)* | Sửa `Total = lines.Sum(...)` → `Total = 0m` trong `Order.PlaceFrom`, `docker compose … up -d --build orders-api`; xong `git checkout --` và dựng lại | Chạy lại folder `09` | Bước 10 đỏ; khôi phục thì xanh | **Đỏ đúng bước 10**: `expected +0 to equal 203.25`; unit test đỏ 3/8 (`PlaceFrom_MultipliesQuantityByUnitPrice`, `PlaceFrom_SumsEveryLine`, `PlaceFrom_IsExact_ForAmountsThatFloatingPointWouldRound`); khôi phục → folder 12/12 xanh, unit 8/8 |
| SC-001/SC-004 — gỡ guard FR-001 rồi khôi phục *(ngoại lệ như trên)* | Comment `ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);` trong `Basket.AddItem`, dựng lại `baskets-api`; xong `git checkout --` | Chạy lại folder `09` | Bước 02 đỏ (nếu guard là lớp bảo vệ duy nhất) | **Unit test đỏ 2/8** (`AddItem_Rejects_AQuantityBelowOne` với 0 và -1) nhưng **Postman vẫn 12/12 xanh**: `POST /baskets/current/items` tự kiểm `Quantity < 1` ở tầng HTTP trước khi tới domain, nên guard domain chỉ còn được unit test bảo vệ (xem QA_Debt) |
| SC-002 — audit lịch sử commit *(ngoại lệ: không có runtime)* | (không có) — chạy `git log --oneline --follow -- <file>` cho `Basket.cs`, `BasketLineMergeTests.cs`, `Order.cs`, `OrderTotalTests.cs` | (không có) | Impl + test đổi cùng commit (`1bc77a6`, `c99783c`, `b3873b5`) | Khớp: `Basket.cs`/`BasketLineMergeTests.cs` cùng đổi ở `1bc77a6`; `Order.cs`/`OrderTotalTests.cs` cùng đổi ở `c99783c`, `b3873b5`. Có thêm commit không phải logic: `59af85f` (khởi tạo mô hình ban đầu), `0351698` (spec 024 sửa comment `Order.cs`), `bfb0162`/`6c6e8bb` (dịch comment test) |
| SC-003 — đơn rỗng bị chặn ở cả 2 tầng | (không có) | `09` bước 07 (HTTP) — tầng domain kiểm bằng unit test ở bảng Tự động | Cả 2 tầng chặn | HTTP `400` đúng; domain: `PlaceFrom_Rejects_AnEmptyLineSet` PASS |
| Dọn dẹp | Trả compose/`.env` về mặc định (không đổi gì) | `09` bước 06 dọn giỏ | Không dữ liệu dư | Đã xoá đơn do QA tạo (giữ đơn gốc QA 017); `baskets-api`/`orders-api` dựng lại từ mã sạch, `git status` trên `services/*/src` sạch |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Đúng các test mà research.md Decision 1 và quickstart.md đã nêu tên.

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001 — chặn số lượng < 1 | [`BasketLineMergeTests.cs:140`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L140) — `AddItem_Rejects_AQuantityBelowOne` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_Rejects_AQuantityBelowOne` |
| FR-002 — giữ giá đã chốt lúc thêm đầu tiên | [`BasketLineMergeTests.cs:116`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L116) — `AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` |
| FR-003 — gộp dòng, không tạo trùng | [`BasketLineMergeTests.cs:51`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L51) — `AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket` · [`:92`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L92) — `AddItem_AccumulatesQuantities_AcrossManyAdditions` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter "FullyQualifiedName~AddItem_IncrementsTheExistingLine\|FullyQualifiedName~AddItem_AccumulatesQuantities"` |
| Giá âm ở tầng domain của giỏ | [`BasketLineMergeTests.cs:157`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L157) — `AddItem_Rejects_ANegativeUnitPrice` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_Rejects_ANegativeUnitPrice` |
| FR-004 — chặn đơn 0 dòng | [`OrderTotalTests.cs:86`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L86) — `PlaceFrom_Rejects_AnEmptyLineSet` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_AnEmptyLineSet` |
| FR-005 — chặn dòng số lượng/giá không hợp lệ | [`OrderTotalTests.cs:101`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L101) — `PlaceFrom_Rejects_ALineWithANonPositiveQuantity` · [`:115`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L115) — `PlaceFrom_Rejects_ALineWithANegativePrice` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter "FullyQualifiedName~PlaceFrom_Rejects_ALineWithANonPositiveQuantity\|FullyQualifiedName~PlaceFrom_Rejects_ALineWithANegativePrice"` |
| FR-006 — tổng do hệ thống tự tính (và chính xác với số thập phân) | [`OrderTotalTests.cs:28`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L28) — `PlaceFrom_MultipliesQuantityByUnitPrice` · [`:45`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L45) — `PlaceFrom_SumsEveryLine` · [`:130`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L130) — `PlaceFrom_IsExact_ForAmountsThatFloatingPointWouldRound` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter "FullyQualifiedName~PlaceFrom_MultipliesQuantityByUnitPrice\|FullyQualifiedName~PlaceFrom_SumsEveryLine\|FullyQualifiedName~PlaceFrom_IsExact"` |
| SC-003 — chặn đơn rỗng ở tầng HTTP (không chỉ tầng domain) | [`PlaceOrderTests.cs:118`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L118) — `PlaceOrder_Rejects_ARequestWithNoLines` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter PlaceOrder_Rejects_ARequestWithNoLines` |
| Tổng do hệ thống tính, không nhận từ caller — chứng cứ ở tầng HTTP | [`PlaceOrderTests.cs:32`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L32) — `PlaceOrder_CreatesTheOrder_AndComputesItsTotal` · [`:136`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L136) — `PlaceOrder_Rejects_ALineWithANonPositiveQuantity` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~PlaceOrder_CreatesTheOrder_AndComputesItsTotal\|FullyQualifiedName~PlaceOrder_Rejects_ALineWithANonPositiveQuantity"` |

**Kết quả lượt QA này (2026-09-26)**: `BasketLineMergeTests` **8/8**, `OrderTotalTests` **8/8**, `PlaceOrderTests` **8/8** (Testcontainers; lần chạy đầu 5/8 đỏ vì `SqlException: Execution Timeout Expired` khi máy đang bận dựng image — chạy lại lúc rảnh 8/8, xem QA_Debt). Chạy cả project `Baskets.Api.UnitTests`/`Orders.Api.UnitTests` còn 1 test đỏ có sẵn không liên quan (`HealthCheckTests.HealthLive_ReturnsOk`, thiếu `ConnectionStrings__…Db` — mục 001/009 ở QA_Debt).

## Kết luận

**PASS kèm ghi chú.** Cả 6 quy tắc chạy đúng trên hệ thống thật (12/12 assertion Postman), gỡ guard FR-006 làm cả unit test lẫn Postman đỏ rồi khôi phục xanh, lịch sử commit khớp research.md Decision 2. Ghi chú: (1) guard `quantity ≥ 1` ở domain bị lớp kiểm tra HTTP che nên chỉ unit test mới phát hiện được khi gỡ nó; (2) thân `400` trả nguyên thông điệp ngoại lệ (lộ tên tham số nội bộ `lines`, gọi nhầm là "lines" cho số lượng/giá); (3) test tích hợp có thể timeout SQL khi máy bận; (4) 1 test đỏ có sẵn không liên quan trong project unit. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 009.
