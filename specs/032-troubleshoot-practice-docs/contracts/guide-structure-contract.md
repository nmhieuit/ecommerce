# Hợp đồng: cấu trúc file hướng dẫn nhóm (09–16) và file gợi ý (17)

## File nhóm `NN-*.md` (NN = 08 + id nhóm)

Tiêu đề cấp 1 `# NN — <tên nhóm>`; dòng đầu tiên sau đó là ghi chú điều kiện (file cần đã làm file nào). Các mục
cấp 2, theo thứ tự:

| # | Mục (`##`) | Nội dung bắt buộc |
|---|---|---|
| 1 | `Bạn sẽ thấy` | Tóm tắt triệu chứng quan sát được, văn phong file 01–08 |
| 2 | `Điều kiện` | Cờ `.env` cần bật, lệnh `-Load` nếu cần, stack đang chạy |
| 3 | `Loại <code> — <tên>` (một mục cho MỖI loại thuộc nhóm) | Đích hợp lệ, lệnh `-Inject`, nơi nhìn (dashboard Xử lý sự cố, Discover, log), truy vấn `_search`/`_count`/ES|QL tự đối chiếu, số đo (ngày + điều kiện), khôi phục `-Restore`, cách xác nhận đã khỏi |
| 4 | `Giới hạn đã biết` | Giới hạn của loại (nguồn QA_Debt/technical-debt); nhiễu khởi động nguội 5–7 phút và cách phân biệt |
| 5 | `Bài tập tự làm` | Việc làm được trên stack, không cần xem đáp án ngoài file |
| 6 | `Đã đạt khi` | Checklist `- [ ]` đo được: đúng nhóm, đúng đích, khôi phục, SLO sạch 15 phút |
| 7 | `Xem thêm` | Link tới quy trình triage và `mau-ban-ghi-su-co.md` của 028, truy vấn 15 phút của file 08, file 17 |

Quy tắc: không chép quy trình triage (chỉ link); mọi con số có ngày và điều kiện đo; không chứa mật khẩu hay token thật.

## File 17

Tiêu đề `# 17 — Gợi ý theo triệu chứng`; cảnh báo đầu file: đang làm bài mù thì đọc từng mức, đừng mở file nhóm.
Mỗi triệu chứng một mục `## <triệu chứng>` gồm ba đoạn theo thứ tự:

1. `**Mức 1 — Gợi ý triệu chứng**`: quan sát + cách kiểm (panel/truy vấn); không mã loại, không tên service.
2. `**Mức 2 — Nhóm lỗi**`: tên nhóm; không mã loại, không tên service.
3. `**Mức 3 — Đáp án (Loại <code>)**`: đáp án, cách khôi phục; có thể link tới file nhóm.

Mỗi loại A–I xuất hiện ở ít nhất một đoạn mức 3. Mức 3 của một triệu chứng có thể liệt kê nhiều loại bằng cách lặp
dòng `**Mức 3 — Đáp án (Loại <code>)**`. Các mục không phải triệu chứng: `## Cách dùng file này`, `## Truy vấn dùng chung` (truy vấn Q1–Q9 để mức 1 không phải link sang file nhóm làm lộ đáp án) và một mục riêng `## Bẫy và nhiễu thường gặp` liệt kê tình huống thật đã gặp, mỗi
cái có nguồn.
