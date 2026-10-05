# Data Model: Chính sách ngân sách lỗi và ngưỡng cảnh báo

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Tính năng không thêm bảng hay dữ liệu nghiệp vụ nào. Các "thực thể" dưới đây là cấu hình (manifest,
rule Kibana) và dữ liệu vận hành (index Elasticsearch).

## 1. Chính sách ngân sách lỗi (`error-budget-policy` trong `service-manifest.yaml`)

Một khối cho mỗi service trong 7 service, đặt ngay sau khối `slos`. Hình dạng chính xác và giá trị
bắt buộc: [contracts/error-budget-policy-manifest-shape.md](./contracts/error-budget-policy-manifest-shape.md).

| Trường | Ý nghĩa | Ràng buộc |
|---|---|---|
| `window` | Cửa sổ tính ngân sách | `calendar-week` (FR-003) |
| `timezone` | Múi giờ của ranh giới ngày/tuần | `UTC+07:00` (giờ Việt Nam) |
| `budgets` | 4 ngân sách | Đủ đúng 4 khoá: `availability`, `error-rate`, `latency-p95`, `latency-p99` (FR-002) |
| `budgets.<x>.bad-request` | Quy tắc request "xấu" | `http-5xx` / `slower-than-slo-p95` / `slower-than-slo-p99` |
| `budgets.<x>.allowed-bad-ratio` | Tỷ lệ xấu cho phép | `1%`, `1%`, `5%`, `1%` |
| `alert-thresholds` | Các mốc cảnh báo | Đúng `[50%, 75%, 100%]` (FR-006) |
| `exhausted-when` | Định nghĩa "cạn" | `any-budget-at-100%` (FR-004) |
| `on-exhausted.who` / `.stops` / `.does` | Hệ quả khi cạn | Không rỗng (FR-009, SC-004) |
| `recovery.consecutive-days-meeting-slo` | Điều kiện hồi phục | `3` (FR-010) |
| `recovery.no-traffic-day-counts-as-met` | Ngày không traffic | `true` |
| `recovery.budget-reset-clears-freeze` | Đặt lại đầu tuần có gỡ đóng băng không | `false` |

Ngưỡng độ trễ dùng cho `latency-p95`/`latency-p99` **không** lặp lại trong khối này — luôn lấy từ
`slos.latency` của cùng manifest (FR-013), tránh hai nguồn sự thật.

## 2. Mức tiêu hao ngân sách (giá trị tính, không lưu)

Tính bởi truy vấn ES|QL dùng chung giữa rule và dashboard; không ghi xuống đâu.

| Trường | Nguồn |
|---|---|
| `service` | `resource.attributes.service.name` |
| `budget` | một trong 4 khoá ngân sách |
| `total` | số span từ đầu tuần (UTC+7) tới nay |
| `bad` | số span xấu theo quy tắc của ngân sách |
| `consumed_pct` | `bad / total / allowed-bad-ratio × 100` |

`total = 0` → không có hàng (trạng thái "không có dữ liệu", FR-012).

## 3. Cảnh báo ngân sách (alert của Kibana)

| Rule | Mỗi alert là | Active khi |
|---|---|---|
| `error-budget-50` / `-75` / `-100` | một cặp (service, budget) | `consumed_pct ≥` mốc của rule |
| `error-budget-frozen` | một service | service đang ở trạng thái cạn ngân sách (mục 5) |

Vòng đời alert: `active` (vượt mốc) → giữ `active` qua mỗi lần chạy 5 phút chừng nào còn vượt →
`recovered` khi không còn vượt (thường là lúc sang tuần mới). Lưu trong alerts-as-data
`.alerts-stack.alerts-default`; mọi rule mang tag `slo-error-budget`.

## 4. Sự kiện cạn ngân sách (index `slo-error-budget-events`)

Ghi bởi action Index connector của rule `error-budget-100`, chỉ khi một alert chuyển sang `active`.
Chỉ ghi thêm (append-only), không sửa, không xoá.

| Trường | Kiểu | Ví dụ |
|---|---|---|
| `@timestamp` | date | thời điểm alert chuyển active |
| `service` | keyword | `Orders.Api` |
| `budget` | keyword | `error-rate` |
| `event` | keyword | `exhausted` |

## 5. Trạng thái cạn ngân sách của service (giá trị tính bởi rule `error-budget-frozen`)

```text
            alert error-budget-100 active (ghi event "exhausted")
  BÌNH THƯỜNG ─────────────────────────────────────────────▶ CẠN — ƯU TIÊN ĐỘ TIN CẬY
       ▲                                                        │
       │  3 ngày trọn vẹn (UTC+7) liên tiếp sau                 │ ngày có traffic không đạt SLO
       │  MAX(ngày cạn, ngày xấu gần nhất), mỗi ngày            │ → đếm lại từ đầu
       └───── đạt đủ 4 chỉ tiêu hoặc không có traffic ◀─────────┘
```

- Ngày "đạt" = ngày có traffic và cả 4 tỷ lệ xấu trong ngày ≤ tỷ lệ cho phép, **hoặc** ngày không có
  traffic.
- Sang tuần mới KHÔNG phải một chuyển trạng thái (FR-010).

## 6. Cấu hình tiêm lỗi 5xx (`Chaos:AllowFaultInjection`)

| Trường | Kiểu | Mặc định | Nguồn |
|---|---|---|---|
| `Chaos:AllowFaultInjection` | bool | `false` | `appsettings.json` / biến compose `CHAOS_ALLOW_FAULT_INJECTION` |
| Header `X-Chaos-Fault` | string | không gửi | từng request; chỉ giá trị `5xx` có hiệu lực |

Chi tiết bất biến: [contracts/chaos-fault-injection-contract.md](./contracts/chaos-fault-injection-contract.md).
