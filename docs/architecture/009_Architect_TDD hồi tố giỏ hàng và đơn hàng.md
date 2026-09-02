# Kiến trúc: Retrofit TDD cho basket pricing và order creation

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-19 ("[CONTRACT-2] Retrofit TDD for basket pricing and order creation"), đặc
tả tại [`specs/009-retrofit-tdd-basket-order/`](../../specs/009-retrofit-tdd-basket-order/).

**Trạng thái xác minh**: Tất cả task đã hoàn thành qua 3 user story + Polish. **Không có thay đổi
production code nào sống sót sau feature này** — mọi lần revert ở Phase 3-4 đều được khôi phục lại
trong cùng task, xác nhận bởi T013/T014 ở Polish.

## 1. Phát hiện quan trọng nhất: không có gì cần sửa (research.md Decision 1)

Một cuộc audit mã nguồn xác nhận **cả 6 quy tắc (FR-001–FR-006) đã được triển khai đúng VÀ đã có unit
test bảo vệ từ trước**:

| FR | Quy tắc | Nơi triển khai | Test bảo vệ |
|---|---|---|---|
| FR-001 | Từ chối quantity < 1 | `Basket.AddItem` | `BasketLineMergeTests.AddItem_Rejects_AQuantityBelowOne` |
| FR-002 | Giữ giá đã chốt lúc thêm đầu tiên | `Basket.AddItem` (bỏ qua `unitPrice` mới nếu line đã có) | `BasketLineMergeTests.AddItem_KeepsTheOriginallyCapturedPrice_...` |
| FR-003 | Gộp vào line đã có, không tạo trùng | `Basket.AddItem` (`existing.Quantity += quantity`) | `BasketLineMergeTests.AddItem_IncrementsTheExistingLine_...` |
| FR-004 | Từ chối order 0 dòng | `Order.PlaceFrom` | `OrderTotalTests.PlaceFrom_Rejects_AnEmptyLineSet` |
| FR-005 | Từ chối dòng invalid (qty<1 hoặc giá âm) | `Order.PlaceFrom` | `OrderTotalTests.PlaceFrom_Rejects_A...` (2 test) |
| FR-006 | Tổng do hệ thống tính, không nhận từ caller | `Order.PlaceFrom` (`Total = lines.Sum(...)`; `PlaceOrderRequest` không có field total) | `OrderTotalTests.PlaceFrom_MultipliesQuantityByUnitPrice`, `PlaceFrom_SumsEveryLine` |

**Quyết định**: KHÔNG viết lại `Basket`/`Order` từ đầu theo TDD "sạch" — mã đang chạy đúng, viết lại
chỉ tạo rủi ro hồi quy mà không mang lại giá trị chức năng nào, và đi ngược nguyên tắc chống
over-engineering của dự án ("một ticket retrofit mà mã hoá ra không cần sửa thì không cần sửa").

## 2. Khoảng cách thật sự: lịch sử commit, không phải mã nguồn (research.md Decision 2)

`git log --follow` xác nhận: cả implementation lẫn unit test của cả hai service đều nằm gộp trong
**cùng một commit lớn** (ví dụ `1bc77a6`, `c99783c`, `b3873b5`) — đúng "Phase 1 shortcut" mà Jira
SCRUM-19 nhắm tới: test tồn tại và đúng, nhưng không đi theo đúng thứ tự đỏ-trước-xanh mà Principle
III yêu cầu ở CẤP COMMIT.

**Quyết định quan trọng về tính chính trực**: KHÔNG `git rebase` để tách mỗi commit gộp thành một cặp
test-rồi-implementation giả tạo. Viết lại lịch sử đã commit để chèn một "commit đỏ" tổng hợp trước mỗi
commit xanh sẽ **bịa ra một trình tự sự kiện chưa từng xảy ra** — bị loại trừ theo chính chính sách vận
hành của repository (yêu cầu uỷ quyền tường minh, phạm vi hẹp, chưa được cấp ở đây), và kể cả có uỷ
quyền cũng sẽ làm sai lệch tác giả thay vì sửa được gì. Khoảng cách được đóng lại **hướng về tương
lai** thay vì sửa quá khứ: một ghi chú kỷ luật bằng văn bản cho các commit sau này, cộng một quy trình
kiểm tra lặp lại được cho reviewer.

## 3. Nơi ghi lại kỷ luật đi tiếp (research.md Decision 3)

`docs/engineering/test-first-commits.md` — một ghi chú thực hành ngắn, **không phải** sửa constitution
và **không phải** một ADR. Lý do: Principle III đã bắt buộc Test-First toàn nền tảng từ trước; tài
liệu này chỉ cụ thể hoá nó cho một vùng mã (basket pricing, order creation) với một quy tắc hình dạng
commit cụ thể — hẹp hơn một thay đổi hiến pháp (đòi hỏi quy trình amendment của Governance), và cũng
không phải một quyết định kiến trúc có ý nghĩa (không có lựa chọn công nghệ, không có đánh đổi cấu
trúc) nên không thuộc `docs/adr/`.

## 4. Cách chứng minh "test thất bại khi quy tắc bị gỡ" (research.md Decision 4)

`quickstart.md` ghi một quy trình thủ công: với mỗi FR-001–006, tạm thời làm yếu guard tương ứng (ví
dụ comment dòng kiểm tra quantity floor), chạy đúng unit test liên quan, xác nhận nó THẤT BẠI, rồi
khôi phục guard và xác nhận test lại PASS. Đây chính là bằng chứng thực nghiệm cho SC-002/SC-004 —
không suy đoán "test chắc là đủ tốt", mà chứng minh bằng cách cố tình phá vỡ rồi quan sát.

## 5. Giới hạn phạm vi đã biết

- Phạm vi chỉ ở mức unit test (domain logic cô lập), khớp đúng acceptance criteria gốc của Jira ticket
  ("when unit tests run") — coverage integration/contract cho hai service này thuộc phạm vi quản lý
  riêng của Principle III, không bị định phạm vi lại ở đây.
- File net-new duy nhất của feature này là `docs/engineering/test-first-commits.md` — không có thay
  đổi domain logic nào tồn tại sau khi feature hoàn thành.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/009-retrofit-tdd-basket-order-component.drawio`](../diagrams/009-retrofit-tdd-basket-order-component.drawio)
- Sơ đồ trình tự (cố tình gỡ một guard → test đỏ → khôi phục → test xanh, lặp lại cho cả 6 quy tắc):
  [`docs/diagrams/009-retrofit-tdd-basket-order-sequence.drawio`](../diagrams/009-retrofit-tdd-basket-order-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/009-retrofit-tdd-basket-order-flow-nghiep-vu.drawio`](../diagrams/009-retrofit-tdd-basket-order-flow-nghiep-vu.drawio)
