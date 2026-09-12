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

## 1. Kiến trúc tổng thể

SPA React tối thiểu (Turborepo `frontend/`) phủ đúng một luồng: duyệt sản phẩm → giỏ hàng → checkout
2 bước → xác nhận. Vì tại thời điểm viết spec BFF chỉ có 3 route đọc, feature này kèm theo cả phần
backend tối thiểu để luồng chạy được thật (FR-019–FR-023): basket line item + quantity, add-to-basket,
place-order từ basket, và seed catalog — bối cảnh mở rộng phạm vi này xem
[technical-debt.md](technical-debt.md).

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

## 3. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/004-minimal-shopping-spa-component.drawio`](../diagrams/004-minimal-shopping-spa-component.drawio)
- Sơ đồ trình tự (duyệt → giỏ hàng → checkout 2 bước → xác nhận, gồm nhánh giỏ trống bị chặn và
  double-submit): [`docs/diagrams/004-minimal-shopping-spa-sequence.drawio`](../diagrams/004-minimal-shopping-spa-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/004-minimal-shopping-spa-flow-nghiep-vu.drawio`](../diagrams/004-minimal-shopping-spa-flow-nghiep-vu.drawio)

Giới hạn phạm vi đã biết (khoảng cách schema-per-tenant chưa đóng): xem
[technical-debt.md](technical-debt.md).
