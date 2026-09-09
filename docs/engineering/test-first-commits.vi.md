# Commit theo kiểu Test-First: Tính giá giỏ hàng và tạo đơn hàng

*(Bản dịch tiếng Việt của [`test-first-commits.md`](test-first-commits.md) — bản gốc tiếng Anh vẫn
được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái**: Ghi chú thực hành đang áp dụng (Active practice note)
**Áp dụng cho**: `services/baskets/src/Baskets.Api/Data/` và `services/orders/src/Orders.Api/Data/`
**Hiện thực hoá**: Constitution [Principle III — Test-First Development (NON-NEGOTIABLE)](../../.specify/memory/constitution.md)
**Nguồn gốc**: [009-retrofit-tdd-basket-order](../../specs/009-retrofit-tdd-basket-order/spec.md) (Jira SCRUM-19)

Đây là 1 ghi chú thực hành (practice note), không phải ADR. Nó không ghi nhận 1 quyết định kiến trúc
nào — `docs/adr/` vẫn là nơi dành cho việc đó. Việc nó làm là biến 1 nguyên tắc hiến pháp (constitution)
đã có sẵn thành 1 điều **kiểm tra được** cho 1 khu vực code cụ thể, để người review có thể tự xác nhận
việc tuân thủ từ chính repository, thay vì phải nhớ hoặc dựa vào 1 quy ước truyền miệng.

## Vì sao ghi chú này tồn tại

Principle III đã yêu cầu Red-Green-Refactor (Đỏ-Xanh-Tái cấu trúc) trên toàn nền tảng từ trước. Các
quy tắc tính giá giỏ hàng và tạo đơn hàng mà nó bao phủ đều đúng và thực sự có test — nhưng 1 lượt rà
soát `git log --follow` trong lúc làm `009` phát hiện các test của chúng nằm **bên trong** cùng 1
commit tính năng lớn với phần cài đặt (`1bc77a6`, `c99783c`, `b3873b5`), chưa từng xuất hiện như 1
commit test-đỏ đi trước.

Đây là 1 kiểu làm việc, không phải 1 kiểu làm sai — và nó vẫn đạt đúng chuẩn mà ticket gốc thực sự đặt
ra (test không được "tới sau vài ngày" — các test này tới sau **0 ngày**). Nhưng nó không để lại gì
trong repository cho người đóng góp sau này biết hình dạng commit được kỳ vọng là gì, nên kiểu gộp
chung này nhiều khả năng sẽ lặp lại. Lịch sử đã có **cố tình KHÔNG** bị viết lại để chèn thêm các
commit-đỏ giả tạo: bịa ra 1 trình tự sự kiện chưa từng xảy ra sẽ làm sai lệch quyền tác giả và không
sửa được gì cả. Khoảng trống này thay vào đó được đóng lại **hướng về tương lai** — bằng chính ghi chú
này.

## Quy tắc

1 commit chạm vào logic tính giá giỏ hàng hoặc tạo đơn hàng **bắt buộc** phải được đi trước bởi, hoặc
đến cùng lúc với, 1 test đang thất bại (đỏ) mà thay đổi đó làm nó thành công (xanh).

Nó **không được phép** tới dưới dạng 1 commit "thêm test" theo sau, dù cùng ngày hay muộn hơn.

Cụ thể, với bất kỳ thay đổi nào vào `Basket.AddItem`, `Basket.Total`, `Order.PlaceFrom`, hoặc các
entity mà chúng sở hữu:

- **Ưu tiên nhất** — 2 commit: test đang thất bại trước, rồi tới thay đổi làm nó thành công. Đây là
  cách làm cho Red-Green-Refactor nhìn thấy được từng commit một.
- **Chấp nhận được** — 1 commit chứa cả test lẫn phần cài đặt, trong đó có thể chứng minh test sẽ thất
  bại nếu thiếu nửa cài đặt đó.
- **KHÔNG chấp nhận được** — phần cài đặt nằm trong 1 commit, test của nó nằm trong bất kỳ commit nào
  sau đó. 1 test được viết SAU đoạn code nó bao phủ chưa bao giờ được quan sát thấy thất bại, nên
  không có gì chứng minh nó có thể bắt được đúng lỗi hồi quy mà nó tuyên bố sẽ canh giữ.

Điểm cuối cùng chính là toàn bộ quy tắc. 1 test chỉ từng ở trạng thái xanh là 1 khẳng định về hình
dạng *hiện tại* của code, không phải 1 lớp phòng vệ cho hình dạng *tương lai* của nó.

## Xác minh việc tuân thủ

Chạy đúng lượt rà soát mà `009` đã dùng. Lịch sử của mỗi file phải cho thấy file test của nó thay đổi
trong cùng 1 commit với, hoặc ở 1 commit SỚM HƠN, phần cài đặt của nó — không bao giờ muộn hơn:

```bash
git log --oneline --follow -- services/baskets/src/Baskets.Api/Data/Basket.cs
git log --oneline --follow -- services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs
git log --oneline --follow -- services/orders/src/Orders.Api/Data/Order.cs
git log --oneline --follow -- services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs
```

Để xác nhận 1 commit cụ thể có gộp chung test của nó thay vì trì hoãn:

```bash
git show --stat --format="" <sha>
```

## Xác nhận các test THẬT SỰ bắt được lỗi hồi quy

Hình dạng commit chứng minh được **chủ đích**; nó không chứng minh được các test có **cắn thật**. Để
chứng minh điều đó, tạm thời làm yếu 1 lớp bảo vệ, xem test tương ứng chuyển đỏ, rồi khôi phục lại.
[`specs/009-retrofit-tdd-basket-order/quickstart.md`](../../specs/009-retrofit-tdd-basket-order/quickstart.md)
đi qua đủ cả 6 quy tắc theo cách này từ đầu tới cuối và là quy trình xác thực đầy đủ cho khu vực này.
Bản rút gọn:

| Quy tắc | Làm yếu chỗ này | Kỳ vọng đỏ |
|---|---|---|
| Ngưỡng sàn số lượng | `ThrowIfLessThan(quantity, 1)` trong `Basket.AddItem` | `AddItem_Rejects_AQuantityBelowOne` |
| Giữ nguyên giá | Ghi đè `existing.UnitPrice` ở nhánh gộp dòng | `AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged` |
| Gộp trùng dòng | Bỏ nhánh `existing is not null` | `AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket` |
| Từ chối đơn hàng rỗng | Bỏ kiểm tra `lines.Count == 0` trong `Order.PlaceFrom` | `PlaceFrom_Rejects_AnEmptyLineSet` |
| Từ chối dòng không hợp lệ | Bỏ các kiểm tra `ArgumentOutOfRangeException` theo từng dòng | `PlaceFrom_Rejects_ALineWithANonPositiveQuantity` |
| Tổng tiền tính toán | Gán cứng `Total` thay vì cộng dồn các dòng | `PlaceFrom_SumsEveryLine` |

Luôn khôi phục lại bằng `git checkout -- <file>` và chạy lại bộ test tới khi xanh trước khi commit.
Không lớp bảo vệ nào đã bị làm yếu được phép tồn tại trong 1 commit.

## Phạm vi

Ghi chú này ràng buộc việc tính giá giỏ hàng và tạo đơn hàng — 2 khu vực mà `009` đã rà soát. Principle
III đã ràng buộc mọi thứ khác từ trước; mở rộng quy tắc hình dạng-commit của ghi chú này ra toàn nền
tảng sẽ là 1 tu chính hiến pháp (constitution amendment), việc mà mục Governance dành riêng cho những
người duy trì nền tảng (platform maintainers).
