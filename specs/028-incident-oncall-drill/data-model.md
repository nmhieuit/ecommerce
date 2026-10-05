# Data Model: Diễn tập sự cố thật và phản ứng on-call

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Tính năng không thêm bảng hay dữ liệu nghiệp vụ nào. Các "thực thể" dưới đây gồm:
- file cục bộ của script (không commit);
- rule và Case của Kibana;
- tài liệu Markdown trong repo.

## 1. Lựa chọn niêm phong (`.incident-drill/<runId>/sealed.json`, không commit)

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `runId` | chuỗi | `yyyyMMdd-HHmmss` theo giờ Việt Nam, lúc chạy `-Start` |
| `service` | chuỗi | một trong `gateway-api`, `bff-api`, `products-api`, `baskets-api`, `orders-api`, `parties-api`, `identity-api` |
| `faultType` | chuỗi | `A-wrong-target` / `B-pool-exhaustion` (chỉ gateway) / `C-027-5xx`; phải áp dụng được cho `service` ([research.md](./research.md) Quyết định 3) |
| `faultDetail` | object | tham số cụ thể; ví dụ với BFF loại A là tên downstream bị trỏ sai |
| `delaySeconds` | số nguyên | 0–1800 |
| `errorRatePct` | số nguyên hoặc `null` | 5–50 khi `faultType = C-027-5xx`, ngược lại `null` |
| `blind` | bool | `true` khi bốc thăm; `false` khi chạy chế độ xác minh `-Service/-FaultType` |
| `plannedInjectAt` | thời điểm ISO 8601 (+07:00) | = lúc chạy `-Start` + `delaySeconds` |

Mã băm SHA-256 của file được in ra lúc `-Start` và dán vào Kibana Case.

File phụ, ghi sau khi tiêm, cùng thư mục, cũng không commit:
- `injected-at.txt`: thời điểm tiêm thực tế;
- `docker-compose.incident.yml`: override chỉ cho service đích; `-Reveal` xoá file này.

Vòng đời: `sealed` (sau `-Start`) → `injected` (tiến trình nền đã tạo lại 7 container) → `revealed`
(sau `-Reveal`, chỉ được chạy khi bản ghi đã có mốc giải quyết).

## 2. Cảnh báo phát hiện nhanh (rule Kibana `incident-fast-detection`)

| Thuộc tính | Giá trị |
|---|---|
| Loại | `.es-query`, ES|QL, `groupBy: row` |
| Chu kỳ / cửa sổ | 5 phút / 5 phút |
| Mỗi alert là | một service |
| Active khi | trong 5 phút gần nhất: `err_pct ≥ 1` hoặc `p95 >` ngưỡng p95 của service hoặc `p99 >` ngưỡng p99 của service |
| Tag | `incident-fast-detection` |
| Action | không có |

Hợp đồng chi tiết: [contracts/fast-detection-rule-contract.md](./contracts/fast-detection-rule-contract.md).

## 3. Sự cố (Kibana Case, tạo tay)

| Trường | Ghi chú |
|---|---|
| Tiêu đề | `Sự cố diễn tập <runId> — <service đang có cảnh báo>` |
| Severity | `SEV1` / `SEV2` / `SEV3` theo spec FR-007 |
| Mô tả | mã băm niêm phong |
| Comment | một comment tại mỗi mốc, cộng cập nhật định kỳ mỗi 30 phút khi sự cố còn mở |
| Baseline | comment `baseline: alert→merge = <phút>` |
| Trạng thái | `open` → `in-progress` → `closed` (chỉ đóng sau mốc giải quyết) |

## 4. Bản ghi sự cố (`docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-su-co-<runId>.md`)

Trường bắt buộc và thứ tự mốc: [contracts/incident-record-contract.md](./contracts/incident-record-contract.md).

Quan hệ:
- một bản ghi ↔ một `runId` ↔ một Kibana Case;
- bản ghi trỏ tới PR giảm thiểu đã merge.
