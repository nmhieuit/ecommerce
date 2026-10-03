# Mẫu bản ghi sự cố (diễn tập on-call)

Sao chép file này thành `ket-qua/<YYYY-MM-DD>-su-co-<runId>.md`, ví dụ
`ket-qua/2026-10-02-su-co-20261002-140000.md`, cho mỗi buổi diễn tập sự cố của
[SCRUM-36](https://nmhieuit.atlassian.net/browse/SCRUM-36). Điền đủ các trường bên dưới, rồi thêm bản
ghi vào mục "Lịch sử chạy" của [README.md](./README.md).

Đủ trường là bắt buộc, và các mốc phải đúng thứ tự. Xem
[contracts/incident-record-contract.md](../../specs/028-incident-oncall-drill/contracts/incident-record-contract.md)
của tính năng 028-incident-oncall-drill.

Mọi thời điểm ghi theo giờ Việt Nam, dạng `YYYY-MM-DD HH:mm:ss +07:00`.

Postmortem không đổ lỗi và ticket follow-up **không** thuộc bản ghi này. Chúng thuộc SCRUM-37.

---

- **run_id**: <!-- runId do `./scripts/incident-drill.ps1 -Start` in ra -->
- **ma_bam_niem_phong**: <!-- SHA256 do -Start in ra; phải giống hệt dòng đã dán vào mô tả Kibana Case -->
- **kibana_case**: <!-- link tới Case (Stack Management → Cases) -->
- **severity**: <!-- SEV1 | SEV2 | SEV3, kèm một câu lý do theo bảng tiêu chí trong README.md -->

## Các mốc

| Mốc | Thời điểm (+07:00) | Nguồn |
|---|---|---|
| `moc_tiem_loi` | <!-- --> | `injected-at.txt`, chỉ đọc được sau `-Reveal` |
| `moc_alert_ban` | <!-- --> | `kibana.alert.start` của alert `incident-fast-detection` cho service gặp sự cố |
| `moc_phat_hien` | <!-- --> | lúc người vận hành nhận biết cảnh báo (mở dashboard, thấy bảng phát hiện nhanh) |
| `moc_xac_dinh_severity` | <!-- --> | lúc chốt SEV và ghi vào Case |
| `moc_xac_dinh_nguyen_nhan` | <!-- --> | lúc tìm ra nguyên nhân từ telemetry/log, TRƯỚC khi reveal |
| `moc_giam_thieu` | <!-- --> | lúc merge PR phòng ngừa tái diễn vào master |
| `moc_giai_quyet` | <!-- --> | phút cuối của 15 phút liên tục đạt SLO, rule không còn active cho service |

- **link_pr_giam_thieu**: <!-- link PR đã merge -->
- **bang_chung_giai_quyet**: <!-- ảnh chụp hoặc link Discover của truy vấn "Xác nhận khôi phục 15 phút"
  (docs/kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md), cho thấy 15 dòng liên tiếp
  dat = true, và rule không còn alert active cho service -->
- **baseline_alert_den_giam_thieu**: <!-- moc_giam_thieu − moc_alert_ban, tính bằng phút; phải giống giá
  trị trong comment "baseline: alert→merge = N phút" của Case -->
- **doi_chieu_niem_phong**: <!-- khớp | không khớp — nguyên nhân tự tìm ra so với lựa chọn in ra bởi
  -Reveal (service, loại hỏng hóc, tham số) -->

## Dòng thời gian

Ghi mọi việc đã quan sát và đã làm, mỗi dòng có thời điểm, gồm cả các alert bị xác định là nhiễu khởi
động nguội.

| Thời điểm (+07:00) | Đã quan sát | Đã làm |
|---|---|---|
| <!-- --> | <!-- --> | <!-- --> |

## Kiểm tra trước khi lưu

- [ ] Không trường nào để trống.
- [ ] `moc_tiem_loi ≤ moc_alert_ban ≤ moc_phat_hien ≤ moc_xac_dinh_severity`, và `moc_xac_dinh_nguyen_nhan ≤ moc_giam_thieu < moc_giai_quyet`.
- [ ] `moc_phat_hien`, `moc_giam_thieu`, `moc_giai_quyet` là ba thời điểm khác nhau.
- [ ] `-Reveal` chỉ chạy sau `moc_giai_quyet`.
- [ ] Đã thêm vào mục "Lịch sử chạy" của README.md.
- [ ] `CHAOS_ALLOW_FAULT_INJECTION` đã về tắt trong `.env`.
