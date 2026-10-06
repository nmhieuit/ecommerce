# Contract: Danh mục nhóm lỗi `scripts/incident-drill/catalog.json`

**Feature**: [../spec.md](../spec.md) (FR-001, FR-002, FR-008, FR-010–FR-015) | **Người tiêu thụ**:
`scripts/incident-drill.ps1`, tài liệu spec D | Cấu trúc trường: [../data-model.md](../data-model.md) |
Kiểm chứng bằng [../quickstart.md](../quickstart.md) (không có test tự động — xem plan.md, Complexity
Tracking).

## Bất biến

| # | Bất biến |
|---|---|
| 1 | Có đúng 8 nhóm (`id` 1–8) và 9 loại (`code` A–I) theo ánh xạ 1:{A}, 2:{B,C}, 3:{D}, 4:{E}, 5:{F}, 6:{G}, 7:{H}, 8:{I}. |
| 2 | Danh mục KHÔNG chứa lệnh docker, tên biến compose (`${...}`), tên file compose hay tên network. Chi tiết đó chỉ nằm trong bộ chuyển đổi Compose. |
| 3 | `hints.1` và `hints.2` của mọi loại KHÔNG chứa tên service/DB trong `targets`, mã loại (`A`–`I` đứng riêng) hay giá trị tham số. `hints.3` được phép dùng dấu giữ chỗ `{service}`, `{target}`, `{parameters}`. |
| 4 | Đích áp dụng được: A — 7 service; B — chỉ `gateway-api`; C — 7 service; D — chỉ `orders-api`; E — 5 DB (`products-db`, `baskets-db`, `orders-db`, `parties-db`, `identity-db`), KHÔNG có Redis/RabbitMQ; F — products, baskets, orders, parties, bff, gateway (KHÔNG có `identity-api`, không có biến thể dừng identity); G, H, I — 7 service app. |
| 5 | Tham số đã chốt: D `latencyMs = 2000`, `errorRatePctRange = [5, 50]`; C `errorRatePctRange = [5, 50]`; G `cpuLimit = 0.1`, `memoryLimitMb = 256`; B `maxConnectionsPerServer = 1`. |
| 6 | `requiresFlags` của D gồm cả `FAULT_INJECTION` và `LATENCY_INJECTION`; của mọi loại khác chỉ `FAULT_INJECTION`. Không có cờ nào khác. |
| 7 | `masksByRecreatingAll = true` đúng với A, B, C, D, F; `false` với E, G, H, I. |
| 8 | Mỗi loại có `restoreKind` thuộc tập đã định nghĩa; nếu bộ chuyển đổi không hỗ trợ `restoreKind` đó, script dừng khi nạp danh mục, không bắt đầu tiêm. |
| 9 | Thêm một bộ chuyển đổi mới (CD/Kubernetes) KHÔNG đòi sửa danh mục: nó chỉ cần hỗ trợ cùng tập `restoreKind` và cùng tập tham số trừu tượng. |
| 10 | Lược đồ có `version`; nạp danh mục có `version` lạ thì script dừng. |
