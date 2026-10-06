# Hợp đồng: test quy ước tài liệu (`tests/TroubleshootGuideConventionTests`)

Test đọc file trên đĩa, không dựng container, không tham chiếu service. Gốc repo tìm bằng marker `Ecommerce.slnx`.
Nguồn sự thật cho nhóm/loại: `scripts/incident-drill/catalog.json` (chỉ đọc).

| # | Bất biến | Xanh khi | Đỏ khi |
|---|---|---|---|
| 1 | Mỗi nhóm trong `groups` có đúng một file `docs/kibana-quan-sat-he-thong/<08+id>-*.md` | tồn tại, tiêu đề `# <08+id> —` | thiếu hoặc trùng file |
| 2 | Mỗi loại trong `types` có mục `## Loại <code>` trong file của nhóm chứa nó | có | thiếu |
| 3 | Mỗi file nhóm có đủ các mục `Bạn sẽ thấy`, `Điều kiện`, `Giới hạn đã biết`, `Bài tập tự làm`, `Đã đạt khi`, `Xem thêm` | đủ | thiếu mục |
| 4 | File 17 có ít nhất một dòng `**Mức 3 — Đáp án (Loại <code>)**` cho MỖI loại A–I | đủ 9 loại | thiếu loại |
| 5 | Mỗi mục triệu chứng trong file 17 có đủ ba đoạn mức 1, 2, 3 theo thứ tự | đủ | thiếu hoặc sai thứ tự |
| 6 | Khối mức 1 và 2 không chứa `Loại <chữ cái>` hay tên một trong 7 service (`Gateway.Api`, `Bff.Api`, `Identity.Api`, `Parties.Api`, `Products.Api`, `Baskets.Api`, `Orders.Api` và dạng `*-api`) | không chứa | chứa |
| 7 | Mọi link nội bộ (`[text](đường dẫn)`, bỏ qua `http(s)://` và `mailto:`) trong file 09–17 và `00-tong-quan-lo-trinh.md` trỏ tới file/thư mục có thật; neo `#...` trỏ tới tiêu đề có thật trong file đích | đúng | đích không có |
| 8 | `00-tong-quan-lo-trinh.md` liệt kê link tới đủ file 09–17 | đủ | thiếu |

KHÔNG kiểm: nội dung khớp giữa tài liệu và `catalog.json`; tính đúng của số đo; "không lộ đáp án" ngoài dấu hiệu rẻ ở bất biến 6.

Mọi test có comment tiếng Việt theo nếp QA 008+ (kiểm gì, vì sao, task nguồn).
