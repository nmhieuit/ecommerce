# Kiến trúc: SPA mua sắm tối thiểu — duyệt/giỏ hàng/thanh toán/xác nhận

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-14 ("[WALK-1] Minimal React SPA"), đặc tả tại
[`specs/004-minimal-shopping-spa/`](../../specs/004-minimal-shopping-spa/), xây trên
[002-gateway-bff-routing](../../specs/002-gateway-bff-routing/) và
[003-stub-identity-tenant-context](../../specs/003-stub-identity-tenant-context/). Đây là feature đầu
tiên thêm cả frontend LẪN dữ liệu nghiệp vụ thật (basket line item, order) — không chỉ SPA đơn thuần.

**Trạng thái xác minh**: 71/71 task trong `tasks.md` đã hoàn thành. Trích nguyên văn số liệu xác minh
cuối `tasks.md`:

> Verified: Playwright 4/4 in Chromium (SC-002, SC-005, SC-007, SC-008, SC-009, SC-010); frontend 45
> tests across 11 files; gateway integration 22; bundle 106.46 kB against the tightened 115 kB budget;
> quickstart Scenario 7 returned `504` in 3.02 s, inside SC-006's five-second ceiling; Scenario 9
> refused a gateway-bypassing call.

## 1. Phạm vi mở rộng — không chỉ SPA

**Assumptions đã ghi nhận trong spec.md**: tại thời điểm viết spec, BFF chỉ có 3 route đọc (list
products, get one basket, get one order) — không có add-to-basket, không có checkout, không có
seed data catalog. Vì vậy feature này **buộc phải bao gồm cả phần backend tối thiểu** (FR-019–FR-023):
basket line item + quantity, add-to-basket, place-order từ basket, và seed catalog — không phải chỉ
xây giao diện gọi vào một backend đã có sẵn đầy đủ.

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 1 | Frontend sống trong workspace Turborepo mới `frontend/` |
| 2 | Một app duy nhất, chưa có package design-system riêng; dùng thẳng Radix + Tailwind |
| 3 | `packages/api-client`: hook TanStack Query sinh tự động bằng Orval từ OpenAPI |
| 4 | Vitest + Testing Library cho hành vi component; Playwright cho walkthrough đầu-cuối thật |
| 5 | Ngân sách kích thước bundle thực thi bằng `size-limit`, fail build nếu vượt |
| 6 | Danh tính người gọi (subject) lan truyền theo cùng cơ chế header với tenant (mirror Decision 2 của 003) |
| 7 | Baskets lưu đơn giá tại thời điểm thêm vào giỏ — do BFF cung cấp, không phải Baskets tự tra giá |
| 8 | Orders tính tổng từ các dòng được gửi tới, chỉ lưu lại tổng — không lưu line item |
| 9 | Checkout là một quy trình 2 bước do BFF điều phối, có canh giữ quy tắc giỏ-hàng-trống |
| 10 | Seed catalog qua một EF Core migration với ID cố định |
| 11 | Storefront chỉ gọi tới gateway qua đúng một origin đã cấu hình |

Quyết định 7/8 đáng chú ý nhất về ranh giới trách nhiệm: BFF **không làm phép tính nào** — tổng tiền
luôn do chính Baskets/Orders tính, giữ đúng nguyên tắc "BFF không chứa business logic" đã đặt ra từ
[002](002_Architect_định%20tuyến%20gateway-BFF.md) FR-005.

## 3. Giới hạn phạm vi đã biết — một khoảng cách quan trọng chưa đóng

`tasks.md` ghi nhận rõ, mục "Not in scope for these tasks": **schema-per-tenant separation mà
[003-stub-identity-tenant-context](../../specs/003-stub-identity-tenant-context/) đã đặc tả và đánh
dấu hoàn thành trên giấy — thực tế CHƯA được triển khai.** `HasDefaultSchema` không xuất hiện ở đâu
trong mã nguồn, và mọi migration đều nhắm vào schema `dbo` mặc định. Feature này thêm dữ liệu nghiệp
vụ thuộc-về-tenant ĐẦU TIÊN của nền tảng ngay trên nền một khoảng cách đó. Việc đóng khoảng cách này
được mô tả là "contained" (resolve schema từ tenant context tại mỗi điểm gọi `AddDbContext`, cộng một
migration mỗi service) nhưng nằm ngoài phạm vi clarify của feature này, và đã được nêu ra để một
maintainer quyết định — **chưa có quyết định nào được đưa ra tại thời điểm này.**

Một quyết định phạm vi khác, tường minh: checkout theo kiểu event-driven (SCRUM-18/SCRUM-31) không
được xây ở đây vì chưa có hạ tầng messaging nào tồn tại — ghi nhận là một deviation có chủ đích, không
phải bị quên.

## 4. Sơ đồ

Luồng mua sắm bốn bước (duyệt → giỏ hàng → thanh toán → xác nhận), qua đúng một backend surface:
[`docs/diagrams/004-minimal-shopping-spa-flow.drawio`](../diagrams/004-minimal-shopping-spa-flow.drawio).
