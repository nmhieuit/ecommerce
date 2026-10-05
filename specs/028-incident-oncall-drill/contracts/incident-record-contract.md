# Contract: Bản ghi sự cố và quy trình triage

**Feature**: [../spec.md](../spec.md) (FR-007 – FR-013) | **Mẫu**:
`docs/dien-tap-chaos-engineering/mau-ban-ghi-su-co.md` | **Nơi lưu**:
`docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-su-co-<runId>.md`.

## Trường bắt buộc

| Trường | Nội dung |
|---|---|
| `run_id` | `runId` của script |
| `ma_bam_niem_phong` | SHA-256 in ra lúc `-Start` (khớp mô tả Kibana Case) |
| `kibana_case` | link Case |
| `severity` | `SEV1` / `SEV2` / `SEV3` + một câu lý do theo tiêu chí |
| `dong_thoi_gian` | bảng `thời điểm (+07:00) | đã quan sát | đã làm`, mỗi dòng có timestamp |
| `moc_tiem_loi` | lấy từ `injected-at.txt` sau khi reveal |
| `moc_alert_ban` | `kibana.alert.start` của rule `incident-fast-detection` |
| `moc_phat_hien` | lúc người vận hành nhận biết cảnh báo |
| `moc_xac_dinh_severity` | |
| `moc_xac_dinh_nguyen_nhan` | |
| `moc_giam_thieu` | thời điểm merge PR phòng ngừa vào master + link PR |
| `moc_giai_quyet` | thời điểm cuối của 15 phút liên tục đạt SLO + bằng chứng telemetry |
| `baseline_alert_den_giam_thieu` | `moc_giam_thieu − moc_alert_ban` (phút), giống giá trị ghi trong Case |
| `doi_chieu_niem_phong` | lựa chọn sau reveal so với nguyên nhân tự tìm: khớp / không khớp |

## Bất biến

| # | Bất biến |
|---|---|
| 1 | Không trường nào được để trống. |
| 2 | `moc_tiem_loi ≤ moc_alert_ban ≤ moc_phat_hien ≤ moc_xac_dinh_severity`, `moc_xac_dinh_nguyen_nhan ≤ moc_giam_thieu < moc_giai_quyet`. |
| 3 | `moc_phat_hien`, `moc_giam_thieu`, `moc_giai_quyet` là ba timestamp khác nhau (Jira test scenario 3). |
| 4 | `moc_giai_quyet` chỉ hợp lệ khi kèm bằng chứng: 15 phút liên tục có traffic, 5xx < 1%, p95/p99 trong ngưỡng, rule không còn active cho service. |
| 5 | Reveal chỉ được chạy sau khi có `moc_giai_quyet`. |
| 6 | Bản ghi được thêm vào mục "Lịch sử chạy" của `docs/dien-tap-chaos-engineering/README.md`. |
| 7 | Postmortem và ticket follow-up KHÔNG thuộc bản ghi này (SCRUM-37). |

## Tiêu chí severity (spec FR-007)

| Mức | Khi nào |
|---|---|
| SEV1 | Luồng đặt hàng (browse → giỏ → checkout → đơn) hỏng hoàn toàn |
| SEV2 | Một chức năng giảm cấp rõ rệt |
| SEV3 | Ảnh hưởng nhỏ hoặc có cách vòng |
