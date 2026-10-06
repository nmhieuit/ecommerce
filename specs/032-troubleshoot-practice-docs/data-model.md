# Data Model: Tài liệu luyện troubleshoot theo nhóm lỗi (032)

Không có dữ liệu hệ thống mới. Các thực thể dưới đây là cấu trúc tài liệu và đầu vào của test.

## Nhóm lỗi / Loại lỗi (đã có — `scripts/incident-drill/catalog.json`, KHÔNG sửa)

| Trường dùng | Ý nghĩa |
|---|---|
| `groups[].id` (1–8), `groups[].name` | Khoá của file hướng dẫn: file số `08 + id` |
| `types[].code` (A–I), `types[].groupId` | Khoá của mục `## Loại <code>` trong file nhóm và mục mức 3 trong file 17 |

## File hướng dẫn nhóm (09–16)

| Thuộc tính | Quy tắc |
|---|---|
| Tên file | Theo [spec Clarifications](./spec.md); thứ tự = nhóm 1–8 |
| Mục | Đủ các mục của [guide-structure-contract.md](./contracts/guide-structure-contract.md) |
| Số loại | Mỗi loại thuộc nhóm có một mục `## Loại <code>`; nhóm 2 có hai (B, C) |
| Bằng chứng | Mỗi số đo ghi ngày, điều kiện, truy vấn; không số suy đoán |

## Mục gợi ý theo triệu chứng (file 17)

| Thuộc tính | Quy tắc |
|---|---|
| Triệu chứng | Tiêu đề `## <triệu chứng>` (vd 5xx, trễ, 401, mất traffic, container báo lỗi) |
| Mức 1 | Gợi ý triệu chứng + cách kiểm; KHÔNG mã loại/tên service |
| Mức 2 | Tên nhóm; KHÔNG mã loại/tên service |
| Mức 3 | Dòng `**Mức 3 — Đáp án (Loại <code>)**`: đáp án + khôi phục; mỗi loại A–I xuất hiện ít nhất một lần |
| Bẫy/nhiễu | Tình huống thật đã gặp, mỗi cái ghi nguồn QA_Debt/technical-debt |

## Số đo bằng chứng

Ngày đo, điều kiện (có tải nền `-Load` hay không), loại và đích, truy vấn, kết quả; ghi trong file nhóm tương ứng.

## Bài tập và checklist "đã đạt"

Mỗi file nhóm có mục `## Bài tập tự làm` (chạy được trên stack) và `## Đã đạt khi` (checklist tick được): đúng
nhóm, đúng đích, khôi phục, SLO sạch 15 phút (link file 08).
