# Data Model: Danh mục 8 nhóm lỗi để luyện troubleshoot

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Không có cơ sở dữ liệu hay storage mới. Mô hình dưới đây là cấu trúc của danh mục (commit) và các file
trạng thái từng lần chạy (không commit, dưới `.incident-drill/`).

## 1. Danh mục — `scripts/incident-drill/catalog.json`

```text
Catalog
├── version: integer                  # bắt đầu 1
├── groups[]: FaultGroup
└── types[]: FaultType
```

### FaultGroup

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `id` | integer | 1–8, duy nhất |
| `name` | string | tên nhóm (tiếng Việt có dấu), ví dụ "Độ trễ" |
| `description` | string | một câu mô tả nhóm, không chứa lệnh docker |

### FaultType

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `code` | string | `A`–`I`, duy nhất |
| `groupId` | integer | tham chiếu `FaultGroup.id` |
| `name` | string | tên loại |
| `targetKind` | enum | `app-service` \| `database` \| `gateway-only` \| `orders-only` |
| `targets` | string[] | danh sách đích áp dụng được (tên service hoặc DB) |
| `parameters` | object | tham số trừu tượng, ví dụ `{ "latencyMs": 2000, "errorRatePctRange": [5, 50] }`, `{ "cpuLimit": 0.1, "memoryLimitMb": 256 }` |
| `requiresFlags` | string[] | `["FAULT_INJECTION"]` hoặc `["FAULT_INJECTION", "LATENCY_INJECTION"]` (tên logic; script ánh xạ sang biến `.env`) |
| `masksByRecreatingAll` | boolean | `true` với A, B, C, D, F (giữ nhiễu che của 028) |
| `restoreKind` | enum | `recreate-without-override` \| `stop-sending` \| `resume-component` \| `reattach-network` \| `recreate-target` |
| `symptoms` | string | triệu chứng quan sát được (dùng cho tài liệu và mức 1) |
| `hints` | object | `{ "1": string, "2": string, "3": string }` — mức 1, 2 không chứa tên service/tham số/mã loại; mức 3 có thể dùng dấu giữ chỗ `{service}`, `{target}`, `{parameters}` |

**Quy tắc kiểm** (script khi nạp danh mục): mỗi `code` có đúng một `restoreKind` được bộ chuyển đổi hỗ trợ;
`hints.1` và `hints.2` không chứa chuỗi trùng tên service trong `targets` hoặc mã loại; không trường nào
chứa `docker` hay `${`.

**Ánh xạ nhóm → loại**: 1:{A}; 2:{B,C}; 3:{D}; 4:{E}; 5:{F}; 6:{G}; 7:{H}; 8:{I}.

## 2. Lần chạy — `.incident-drill/<runId>/`

`runId` = `yyyyMMdd-HHmmss` giờ Việt Nam (như 028).

### sealed.json (băm SHA-256)

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `runId` | string | |
| `blind` | boolean | `true` chỉ với `-Start` bốc thăm mù hoàn toàn |
| `mode` | enum | `blind` \| `scripted` (dạng a, `-Inject`) \| `legacy-nonblind` (`-Start -Service/-FaultType` của 028) |
| `group` | integer | 1–8 (lần chạy 028 cũ không có trường này) |
| `faultType` | string | `A`–`I` (028 cũ: `A`/`B`/`C`) |
| `target` | string | đích thật được tiêm (service hoặc DB) |
| `service` | string | service app bị ảnh hưởng chính (E: service chủ DB) |
| `faultDetail` | object | ví dụ `{ "downstream": "OrdersApi" }` (A với BFF) |
| `delaySeconds` | integer | 0–1800 |
| `durationSeconds` | integer? | chỉ với dạng (a) có `-DurationSeconds` |
| `errorRatePct` | integer? | 5–50 khi `faultType` ∈ {C, D} |
| `latencyMs` | integer? | 2000 khi `faultType = D` |
| `plannedInjectAt` | string | ISO-8601 +07:00 |

### state.json (KHÔNG băm)

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `status` | enum | `pending` → `injected` → `restored`; `failed` khi tiêm/khôi phục thất bại |
| `injectorPid` | integer? | PID tiến trình nền, để `-Restore` hủy khi còn `pending` |
| `injectedAt` | string? | ISO-8601 +07:00 |
| `restoredAt` | string? | ISO-8601 +07:00 |
| `failure` | string? | lý do khi `failed` |

**Chuyển trạng thái**:

```text
        -Start/-Inject
              │
          pending ──(tiêm xong)──▶ injected ──(-Restore xong, healthy ≤ 10 phút)──▶ restored
              │                         │
              └──(-Restore sớm)─────────┴──────────────────────────────────────▶ restored
        (lỗi ở bất kỳ bước)──▶ failed   (vẫn khôi phục được bằng -Restore)
```

Điều kiện từ chối bắt đầu mới: tồn tại bất kỳ `state.json` có `status ∈ {pending, injected, failed}`.

### hint-log.json

Mảng `[ { "at": "<ISO-8601 +07:00>", "level": 1|2|3 } ]`, ghi thêm sau mỗi lần `-Hint`.

### Các file khác

`hash.txt`, `injected-at.txt`, `restored-at.txt`, `stop.flag`, `injector.log`,
`docker-compose.incident.yml` — xem [research.md](./research.md) Quyết định 5.

## 3. Quan hệ

- `FaultType.groupId` → `FaultGroup.id` (n–1).
- `sealed.json.faultType` → `FaultType.code` (tra danh mục lúc `-Hint`, `-Restore`).
- Một `runId` có đúng một `sealed.json`, một `state.json`, 0..n dòng `hint-log.json`.
- Danh mục không biết đến Docker; bộ chuyển đổi Compose không lưu dữ liệu, chỉ đọc `sealed.json` và danh mục.
