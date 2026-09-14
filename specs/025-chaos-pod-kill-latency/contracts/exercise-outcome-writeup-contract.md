# Hợp đồng: Bản ghi kết quả bài tập chaos (Exercise Outcome Write-up)

Áp dụng cho mẫu tài liệu tại `docs/dien-tap-chaos-engineering/mau-ket-qua.md` và mọi bản ghi kết quả
điền từ mẫu đó (data-model.md mục 3; research.md Quyết định 3). Hợp đồng này được kiểm bằng mắt qua
`quickstart.md`, không có test tự động — bản chất là kỷ luật tài liệu, không phải mã chạy được.

## Bất biến bắt buộc

1. **Một bài tập — một bản ghi**: mỗi lần chạy một trong hai kịch bản chaos (kill-pod, inject-latency)
   PHẢI tạo đúng một file mới tại `docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-<kich-ban>.md`
   trước khi coi bài tập là hoàn tất (spec FR-007).
2. **Đủ trường bắt buộc**: mỗi bản ghi PHẢI điền đủ `ngay_chay`, `kich_ban`, `nguoi_thuc_hien`,
   `quan_sat`, `ket_luan` (data-model.md mục 3) — không được để trống bằng một giá trị giữ chỗ.
3. **Sai lệch PHẢI có liên kết**: nếu `ket_luan = sai_lech`, bản ghi PHẢI có trường `jira_ticket`
   khác rỗng, trỏ tới một bug ticket mô tả đúng sai lệch quan sát được (spec FR-008). Một bản ghi
   `sai_lech` không có `jira_ticket` VI PHẠM hợp đồng này.
4. **Tra cứu được**: `docs/dien-tap-chaos-engineering/README.md` PHẢI liệt kê mọi file đang có trong
   `ket-qua/`, mới nhất trước, kèm liên kết trực tiếp tới file và kết luận tóm tắt của nó (spec
   FR-009). Thêm một bản ghi mới mà không cập nhật README VI PHẠM hợp đồng này.

## Không thuộc phạm vi hợp đồng này

- Không quy định định dạng/công cụ mà bug ticket ở mục 3 phải dùng (Jira hay hệ thống khác) — chỉ
  yêu cầu một liên kết tra cứu được (research.md Quyết định 3).
- Không yêu cầu bản ghi kết quả phải máy đọc được (JSON/YAML có schema) — markdown theo mẫu là đủ vì
  không có hệ thống nào trong repo cần đọc lập trình các bản ghi này.
