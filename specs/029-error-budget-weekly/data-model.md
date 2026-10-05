# Data Model: Ngân sách lỗi theo tuần lịch giờ Việt Nam

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Không thêm bảng, index hay dữ liệu nghiệp vụ nào. Spec này chỉ đổi giá trị và phạm vi thời gian của các thực thể cấu hình/vận hành mà 027 và 028 đã tạo. Thực thể nào không nêu ở đây thì giữ nguyên như `specs/027-error-budget-alerting/data-model.md`:
- connector Index;
- mapping của `slo-error-budget-events`;
- cấu hình tiêm lỗi `Chaos:AllowFaultInjection`.

## 1. SLO mặc định nền tảng (hiến chương 2.0.0 ↔ `PlatformSloDefaults`)

| Hồ sơ | Khả dụng | 5xx tối đa | p95 | p99 |
|---|---|---|---|---|
| `client-facing-bff` | `99%` (tuần) | `1%` | `300ms` | `800ms` |
| `internal-service-api` | `99%` (tuần) | `1%` | `150ms` | `500ms` |

Ràng buộc: hiến chương, `PlatformSloDefaults` và khối `slos` của 7 manifest nói cùng một con số ([contracts/constitution-amendment.md](./contracts/constitution-amendment.md), bất biến 3).

## 2. Chính sách ngân sách lỗi (`error-budget-policy`)

Chỉ các trường thay đổi (hình dạng đầy đủ: [contracts/error-budget-policy-manifest-shape.md](./contracts/error-budget-policy-manifest-shape.md)):

| Trường | 027 | 029 |
|---|---|---|
| `window` | `calendar-month` | `calendar-week` (thứ Hai 00:00 → Chủ nhật 23:59) |
| `budgets.availability.allowed-bad-ratio` | `0.1%` | `1%` |
| `budgets.error-rate.allowed-bad-ratio` | `0.1%` | `1%` |

Quy tắc: `allowed-bad-ratio` của khả dụng và 5xx = 1 − SLO của chính manifest.

## 3. Mức tiêu hao ngân sách tuần (giá trị tính, không lưu)

| Trường | Nguồn |
|---|---|
| `total` | số span từ `DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` tới nay |
| `bad` | số span xấu theo quy tắc ngân sách |
| `consumed_pct` | `bad / total / allowed × 100`, với `allowed` = `0.01 / 0.01 / 0.05 / 0.01` |

`total = 0` → không có hàng ("không có dữ liệu").

## 4. Cảnh báo ngân sách

| Rule | Mỗi alert là | Phạm vi dữ liệu | Active khi |
|---|---|---|---|
| `error-budget-50/75/100` | (service, budget) | cửa sổ rule 7 ngày, cắt từ đầu tuần | `consumed_pct ≥` mốc |
| `error-budget-frozen` | service | cửa sổ rule 14 ngày | service đang đóng băng (mục 5) |
| `incident-fast-detection` | service | 5 phút gần nhất | 5xx ≥ 1% hoặc vượt ngưỡng độ trễ (gateway chỉ 5xx) |

Vòng đời alert mốc: `active` → giữ qua mỗi lần chạy 5 phút → `recovered` khi không còn vượt mốc. Thường xảy ra lúc thứ Hai 00:00 giờ Việt Nam, khi mức tiêu hao tuần bắt đầu lại.

## 5. Trạng thái cạn ngân sách của service

```text
              alert error-budget-100 active (ghi event "exhausted")
  BÌNH THƯỜNG ─────────────────────────────────────────────▶ CẠN — ƯU TIÊN ĐỘ TIN CẬY
       ▲                                                        │
       │  3 ngày trọn vẹn (UTC+7) liên tiếp đạt SLO,            │ ngày có traffic mà 5xx ≥ 1%
       │  hoặc không có traffic                                 │ hoặc p95 > 5% / p99 > 1% → đếm lại
       └────────────────────────────────────────────────────────┘
```

- Ngày "đạt" = ngày có traffic với `5xx/spans < 0.01`, `p95_bad/spans ≤ 0.05`, `p99_bad/spans ≤ 0.01`, hoặc ngày không có traffic.
- Thứ Hai 00:00 (ngân sách đặt lại) KHÔNG phải một chuyển trạng thái.
- Rule chỉ thấy sự kiện "cạn" trong 14 ngày gần nhất. Đóng băng kéo dài hơn có thể tự mất (giới hạn đã biết).

## 6. Hiển thị trên dashboard (Discover session của 027, sửa giá trị)

| Saved search (id giữ nguyên) | Tiêu đề mới | `time_range` panel |
|---|---|---|
| `slo-error-budget-consumption` | "Ngân sách lỗi — mức tiêu hao tuần này (7 service × 4 ngân sách)" | `now-7d` |
| `slo-error-budget-active-alerts` | "Ngân sách lỗi — cảnh báo đang hoạt động" (tiêu đề panel: "Ngân sách lỗi tuần này — cảnh báo đang hoạt động") | `now-7d` |
| `slo-error-budget-frozen` | "Cạn ngân sách — ưu tiên độ tin cậy" (giữ) | `now-7d` |

Panel text: "`99%`/tuần" thay cho "`99.9%`/tháng". Cách viết chính xác của các tiêu đề "tuần này" được chốt ở task. Riêng mô tả của saved search (`description`) đổi "% ngân sách tháng" thành "% ngân sách tuần".
