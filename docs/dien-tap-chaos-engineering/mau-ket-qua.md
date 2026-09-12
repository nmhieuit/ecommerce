# Mẫu bản ghi kết quả bài tập chaos

Sao chép file này thành `ket-qua/<YYYY-MM-DD>-<kich-ban>.md` (ví dụ `ket-qua/2026-09-12-kill-pod.md`)
sau mỗi lần chạy một trong hai kịch bản của
[SCRUM-34](https://nmhieuit.atlassian.net/browse/SCRUM-34), điền đủ các trường bên dưới, rồi thêm nó
vào mục "Lịch sử chạy" của [README.md](./README.md).

Đủ trường là bắt buộc — xem
[contracts/exercise-outcome-writeup-contract.md](../../specs/025-chaos-pod-kill-latency/contracts/exercise-outcome-writeup-contract.md)
của tính năng 025-chaos-pod-kill-latency.

---

- **ngay_chay**: <!-- YYYY-MM-DD -->
- **kich_ban**: <!-- kill-pod | inject-latency -->
- **nguoi_thuc_hien**: <!-- ai chạy bài tập -->
- **quan_sat**: <!-- mô tả quan sát được: thời gian phục hồi đo được (kill-pod) hoặc mức ngân sách
  SLO bị tiêu hao quan sát trên dashboard (inject-latency), kèm bằng chứng (ảnh chụp/link Kibana) -->
- **ket_luan**: <!-- dat | sai_lech -->
- **jira_ticket**: <!-- bắt buộc nếu ket_luan = sai_lech; để trống nếu ket_luan = dat -->
