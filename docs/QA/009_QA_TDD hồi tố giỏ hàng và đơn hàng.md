# QA: TDD hồi tố giỏ hàng và đơn hàng

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Khác mọi spec trước: đây không phải 1 feature sinh code mới. Bản thân spec tự tuyên bố "không có gì
cần sửa" (research.md Decision 1) — mã tính giá giỏ hàng/tạo đơn đã đúng và đã có test bảo vệ từ
trước; việc "làm" của spec 009 là **chứng minh lại bằng thực nghiệm** (cố ý gỡ từng quy tắc, xác nhận
test đỏ, khôi phục) và ghi lại kỷ luật commit cho tương lai. Vì vậy QA ở đây không rà một luồng nghiệp
vụ người dùng, mà rà lại chính quy trình chứng minh đó.

## Luồng happy-case đã rà soát

1. **6 quy tắc tính tiền (FR-001–006) đều đã có unit test bảo vệ**, và cố ý gỡ từng guard thì đúng 1
   test tương ứng chuyển đỏ — đã tự tay làm lại 2/6 quy tắc (không phải chỉ đọc tài liệu, xem mục
   "Thủ công" bên dưới), khớp với bảng research.md Decision 1 (SC-001, SC-004).
2. **Lịch sử commit của 4 file cốt lõi khớp đúng tuyên bố của research.md Decision 2**: implementation
   và unit test của cả 2 service nằm gộp trong cùng 1 commit lớn (`1bc77a6`, `c99783c`, `b3873b5`),
   không có test nào tới sau nhiều ngày — đã tự chạy lại `git log --follow` để xác nhận, không suy
   diễn từ tài liệu (SC-002).
3. **Đặt đơn từ giỏ rỗng bị chặn ở cả tầng domain (`Order.PlaceFrom`) lẫn tầng HTTP (`POST /orders`)**
   — đã tự chạy lại cả 2 test để xác nhận (SC-003).
4. **`docs/engineering/test-first-commits.md` tồn tại, đã commit, và nội dung khớp đúng quy tắc
   research.md Decision 3 mô tả** (không phải ADR, không sửa constitution — chỉ là ghi chú thực hành
   cho 2 vùng mã cụ thể).
5. **Không có thay đổi production code nào sống sót**: sau khi tự làm lại 2 lượt "gỡ rồi khôi phục",
   `git status`/`git diff` trên `services/baskets/src` và `services/orders/src` sạch — đúng tuyên bố
   của tasks.md T013/T014.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Không cần Docker Desktop/Postman như phần lớn spec trước — bộ unit test chạy thẳng bằng `dotnet test`
từ máy có .NET 10 SDK. Chỉ riêng Scenario 3 (test tích hợp) cần Docker chạy sẵn (SQL Server
Testcontainers). Đây gần như nguyên văn `quickstart.md` của spec — không cần viết lại kịch bản khác,
chỉ **tự chạy thật** thay vì tin vào mô tả.

### Thủ công — Scenario 1: audit lịch sử commit (SC-002)

```bash
git log --oneline --follow -- services/baskets/src/Baskets.Api/Data/Basket.cs
git log --oneline --follow -- services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs
git log --oneline --follow -- services/orders/src/Orders.Api/Data/Order.cs
git log --oneline --follow -- services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs
```

**Kết quả đo thật** (2026-09-22): `Basket.cs` và `BasketLineMergeTests.cs` cùng đổi ở commit `1bc77a6`
("Implement basket total computation and BFF integration for shopping basket"); `Order.cs` và
`OrderTotalTests.cs` cùng đổi ở `c99783c` ("feat: Implement checkout workflow in BFF") và lại cùng đổi
tiếp ở `b3873b5` ("feat: Add tenant attribution to orders") — đúng 3 mã commit research.md Decision 2
nêu tên, không lệch.

### Thủ công — Scenario 2: cố ý gỡ 1 guard, xác nhận test đỏ, khôi phục (SC-001, SC-004)

`quickstart.md` có bảng đủ cho cả 6 quy tắc; đã tự làm lại 2 quy tắc làm mẫu (2 quy tắc còn lại theo
đúng cùng khuôn, không lặp lại ở đây):

- **FR-001 (quantity floor)**: comment dòng `ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);`
  trong `Basket.AddItem` (`services/baskets/src/Baskets.Api/Data/Basket.cs`), chạy
  `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_Rejects_AQuantityBelowOne`
  → cả 2 case (`quantity: 0`, `quantity: -1`) **FAIL** đúng như kỳ vọng. `git checkout -- ...Basket.cs`
  rồi chạy lại `--filter FullyQualifiedName~BasketLineMergeTests` → 8/8 **PASS**.
- **FR-006 (tổng tự tính)**: đổi `Total = lines.Sum(line => line.Quantity * line.UnitPrice),` thành
  `Total = 0m,` trong `Order.PlaceFrom` (`services/orders/src/Orders.Api/Data/Order.cs`), chạy
  `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_SumsEveryLine` →
  **FAIL** đúng như kỳ vọng. `git checkout -- ...Order.cs` rồi chạy lại
  `--filter FullyQualifiedName~OrderTotalTests` → 8/8 **PASS**.

Sau cả 2 lượt: `git status --porcelain services/baskets/src services/orders/src` rỗng — không còn
thay đổi sót lại.

### Thủ công — Scenario 3: đặt đơn từ giỏ rỗng bị chặn ở cả 2 tầng (SC-003)

```bash
dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_AnEmptyLineSet
dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter PlaceOrder_Rejects_ARequestWithNoLines
```

Đã tự chạy lại (Docker Desktop đang bật) — cả 2 **PASS** (test tích hợp cần container SQL Server, mất
khoảng 18s để dựng).

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Đúng các test mà research.md Decision 1 và quickstart.md đã nêu tên — đều nằm trong 3 file đã được
rà soát chi tiết (và viết lại comment kiểu gọn) khi làm QA cho spec 004 và 006.

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001 — chặn số lượng < 1 | [`BasketLineMergeTests.cs:140`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L140) — `AddItem_Rejects_AQuantityBelowOne` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_Rejects_AQuantityBelowOne` |
| FR-002 — giữ giá đã chốt lúc thêm đầu tiên | [`BasketLineMergeTests.cs:116`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L116) — `AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` |
| FR-003 — gộp dòng, không tạo trùng | [`BasketLineMergeTests.cs:51`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L51) — `AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket` · [`:92`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L92) — `AddItem_AccumulatesQuantities_AcrossManyAdditions` | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter "FullyQualifiedName~AddItem_IncrementsTheExistingLine\|FullyQualifiedName~AddItem_AccumulatesQuantities"` |
| FR-004 — chặn đơn 0 dòng | [`OrderTotalTests.cs:86`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L86) — `PlaceFrom_Rejects_AnEmptyLineSet` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter PlaceFrom_Rejects_AnEmptyLineSet` |
| FR-005 — chặn dòng số lượng/giá không hợp lệ | [`OrderTotalTests.cs:101`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L101) — `PlaceFrom_Rejects_ALineWithANonPositiveQuantity` · [`:115`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L115) — `PlaceFrom_Rejects_ALineWithANegativePrice` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter "FullyQualifiedName~PlaceFrom_Rejects_ALineWithANonPositiveQuantity\|FullyQualifiedName~PlaceFrom_Rejects_ALineWithANegativePrice"` |
| FR-006 — tổng do hệ thống tự tính | [`OrderTotalTests.cs:28`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L28) — `PlaceFrom_MultipliesQuantityByUnitPrice` · [`:45`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L45) — `PlaceFrom_SumsEveryLine` | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter "FullyQualifiedName~PlaceFrom_MultipliesQuantityByUnitPrice\|FullyQualifiedName~PlaceFrom_SumsEveryLine"` |
| SC-003 — chặn đơn rỗng ở tầng HTTP (không chỉ tầng domain) | [`PlaceOrderTests.cs:118`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L118) — `PlaceOrder_Rejects_ARequestWithNoLines` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter PlaceOrder_Rejects_ARequestWithNoLines` |
| Tổng do hệ thống tính, không nhận từ caller — chứng cứ ở tầng HTTP | [`PlaceOrderTests.cs:32`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L32) — `PlaceOrder_CreatesTheOrder_AndComputesItsTotal` · [`:136`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L136) — `PlaceOrder_Rejects_ALineWithANonPositiveQuantity` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~PlaceOrder_CreatesTheOrder_AndComputesItsTotal\|FullyQualifiedName~PlaceOrder_Rejects_ALineWithANonPositiveQuantity"` |

Chạy cả 2 project unit test cùng lúc để xem tổng quan (kết quả thật, xem "Kết luận" bên dưới về 1 test
đỏ không liên quan):

```bash
dotnet test services/baskets/tests/Baskets.Api.UnitTests
dotnet test services/orders/tests/Orders.Api.UnitTests
```

## Kết luận

**PASS** — cả 3 nguồn (architecture/summary/spec-summary-vi) mô tả cùng 1 nội dung, không mâu thuẫn
nhau, và đã tự chạy lại thật (không suy diễn) cả audit lịch sử commit lẫn 2/6 lượt gỡ-guard-rồi-khôi
phục: kết quả khớp 100% với những gì research.md/quickstart.md tuyên bố. Đây là spec đầu tiên trong
loạt rà soát này không phát sinh phát hiện mới nào về nội dung nghiệp vụ.

Có 1 ghi chú nhỏ (không phải lỗi của riêng spec 009) — xem [QA_Debt.md](QA_Debt.md).
