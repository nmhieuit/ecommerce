# Kiến trúc: OpenAPI spec cho route BFF + sinh client tự động

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-17 ("[CONTRACT-2] OpenAPI specs for BFF routes + generated clients"), đặc
tả tại [`specs/007-bff-openapi-contracts/`](../../specs/007-bff-openapi-contracts/). Quyết định kiến
trúc gốc: [ADR-0004](../adr/0004-openapi-client-codegen.md) (chọn công cụ sinh client TypeScript từ
OpenAPI), thực thi kỹ thuật đã có từ [002-gateway-bff-routing](../../specs/002-gateway-bff-routing/)
Decision 6 (dùng document builder OpenAPI có sẵn của ASP.NET Core).

**Trạng thái xác minh**: 15/15 task trong `tasks.md` đã hoàn thành. Bốn checkpoint xác nhận từng
user story độc lập (SC-001 tới SC-005) — xem mục 2.

## 1. Bản chất feature — xác nhận và củng cố, không phải xây mới

`research.md` Decision 1 nêu rõ phạm vi: **đóng những khoảng hở có thể kiểm chứng được, không xây lại
pipeline.** Decision 4 xác nhận: **không có thay đổi production code nào được kỳ vọng.** Cơ chế
contract-first (BFF tự sinh tài liệu OpenAPI, SPA sinh client từ đó — ADR-0004) đã tồn tại từ trước
nhờ [002-gateway-bff-routing](../../specs/002-gateway-bff-routing/). Việc thật sự cần làm ở đây chỉ có
một: đảm bảo client sinh ra **chấp nhận field lạ mà không sập** (tolerant reader) — phần còn lại là
xác nhận lại những gì đã đúng vẫn còn đúng, không phải triển khai từ đầu.

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 1 | Giới hạn phạm vi vào việc đóng các khoảng hở có thể kiểm chứng, không xây lại pipeline |
| 2 | Xác minh route ↔ spec khớp nhau (SC-001) bằng CÁCH DỰNG (by construction — spec sinh ra từ chính route), không thêm một automated check mới |
| 3 | Thêm test tolerant-reader — mỗi mảng nghiệp vụ một case, viết trong file test đã có sẵn, không tạo file mới |
| 4 | Không có thay đổi production code nào |

## 3. Ghi chú cho người bảo trì tương lai

`tasks.md` ghi rõ: nếu bất kỳ task xác minh nào (T004-T009) phát hiện một sai lệch thực sự giữa route
và spec, đó là một **defect thật, nằm ngoài giả định phạm vi của feature này** — cần dừng lại và định
phạm vi lại, không được âm thầm vá bên trong một task "chỉ xác minh". Không có sai lệch nào được ghi
nhận trong lượt triển khai này — cả bốn checkpoint (SC-001, SC-002/003/005, SC-004) đều xác nhận PASS.

## 4. Giới hạn phạm vi đã biết

Phạm vi chỉ giới hạn ở ba mảng nghiệp vụ products/baskets/orders (đúng như Jira issue) — route parties,
checkout, và health-check nằm ngoài phạm vi, theo đúng Assumptions đã ghi trong `spec.md`.

## 5. Sơ đồ

Không có diagram riêng — đây là một bước xác nhận hợp đồng đã có sẵn (không thay đổi kiến trúc), mô tả
bằng bảng quyết định ở mục 2 là đủ.
