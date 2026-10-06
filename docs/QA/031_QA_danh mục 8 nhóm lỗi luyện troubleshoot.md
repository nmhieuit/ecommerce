# QA: Danh mục 8 nhóm lỗi để luyện troubleshoot

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — danh mục độc lập với nơi tiêm**: `scripts/incident-drill/catalog.json` (8 nhóm, 9 loại A–I,
   không có lệnh docker); bộ chuyển đổi Compose nằm trong `scripts/incident-drill.ps1`.
2. **US2 — dạng (a), có chủ đích**: `-Inject -Type|-Group [-Target] [-DurationSeconds]` tiêm một loại vào
   một đích hợp lệ; `-Restore -RunId` khôi phục mọi loại; từ chối khi cờ tắt, khi loại không áp dụng
   được cho đích, khi còn lần chạy chưa khôi phục.
3. **US3 — dạng (b), bất ngờ**: `-Start` bốc nhóm đều 1/8, chỉ in `runId` và mã băm; `-Reveal` kiểm băm
   và in trạng thái, nhật ký gợi ý. Chế độ không mù của 028 (`-Start -Service -FaultType`) và `-Load`
   giữ nguyên.
4. **US4 — gợi ý theo mức**: `-Hint -RunId -Level 1|2|3`, ghi `hint-log.json`.
5. **US5 — gỡ Kubernetes của 025**: tài liệu 025 không còn kill-pod/kubectl; `deploy/ansible`, CI không đổi.
6. **US6 — Postman và tài liệu**: folder Postman `31`, tài liệu PO/QA/Architect, 3 sơ đồ.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ trong `.env`, chạy `incident-drill.ps1`, bấm Postman folder 31

**Dựng stack**: `docker compose -f docker-compose.local.yml up -d --wait`. Tải nền ở một terminal riêng:
`./scripts/incident-drill.ps1 -Load` (cần cho loại D vì lấy token từ file do `-Load` sinh).

**Postman**: import
[`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn
environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token)` rồi folder
**`31 - Danh mục nhóm lỗi: kiểm chứng thủ công 6 nhóm D–I`** theo từng subfolder: tiêm bằng lệnh nêu ở mô
tả subfolder → chạy request `01 Gây triệu chứng` → `-Restore` → chạy request `02 Kiểm khôi phục`. Với newman
dùng `-e .incident-drill/load/environment-with-token.json`.

**Công tắc cấu hình**:
- **Bật**: `CHAOS_ALLOW_FAULT_INJECTION=true` trong `.env` (mọi nhóm); nhóm D thêm
  `CHAOS_ALLOW_LATENCY_INJECTION=true` (cả hai mặc định tắt).
- **Khôi phục**: `./scripts/incident-drill.ps1 -Restore -RunId <id>`. Sau đó đặt hai cờ về `false`/xoá
  dòng trong `.env` và chạy lại stack không kèm override.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | Đã quan sát (2026-10-05/06) |
|---|---|---|---|---|
| Từ chối khi cờ tắt | Bỏ `CHAOS_ALLOW_FAULT_INJECTION` | (không có) — `-Inject -Type G -Target products-api`, `-Start` | `exit 1`, không đổi gì | Từ chối cả hai, không có thư mục lần chạy mới |
| Từ chối khi thiếu cờ latency | `CHAOS_ALLOW_LATENCY_INJECTION=false` | (không có) — `-Inject -Type D` | Nêu rõ cờ nào thiếu | Từ chối, nêu `CHAOS_ALLOW_LATENCY_INJECTION` |
| Từ chối loại sai đích | Cờ bật | (không có) — `-Type B -Target orders-api`, `-Type D -Target products-api`, `-Type F -Target identity-api`, `-Type E -Target orders-api`, `-Group 3 -Type A` | Từ chối kèm lý do, không đổi gì | Cả 5 bị từ chối, nêu danh sách đích hợp lệ |
| Chỉ một lần chạy mở | Cờ bật; đang có lần chạy `injected` | (không có) — `-Inject` lần hai | Từ chối, chỉ ra `runId` đang mở | Từ chối, nêu `runId` |
| Nhóm D — độ trễ | Hai cờ bật; `-Inject -Type D -Target orders-api -DurationSeconds 300` | Subfolder `D` (01, 02) | Request 01: 404 chậm ≥ 1,9 s; 02: 404 nhanh | 44/216 span ≥ 1,9 s, p95 2006 ms; tự khôi phục sau 300 s |
| Nhóm E — DB dừng | `-Inject -Type E -Target orders-db` | Subfolder `E` | 01: 503; 02: 200 sau khôi phục | 503 trong ≤ 6 s; ready lại sau ≈ 44–68 s |
| Nhóm F — Authority sai | `-Inject -Type F -Target gateway-api` | Subfolder `F` | 01: 401/502/504; 02: 200 | 01 đúng; 02 đúng sau ≈ 2 phút chờ token |
| Nhóm G — thiếu tài nguyên | `-Inject -Type G -Target products-api` | Subfolder `G` | 200, chậm hơn nền | 200; p95 ≈ 102 ms, p99 ≈ 198 ms (nhẹ) |
| Nhóm H — mạng đứt | `-Inject -Type H -Target baskets-api` | Subfolder `H` | 01: 502/504; 02: 200/404 | 504 sau ≈ 3 s; khôi phục 1–3 s |
| Nhóm I — container chết | `-Inject -Type I -Target parties-api` | Subfolder `I` | 01: 502/504; 02: 404 | 504 sau ≈ 3 s; khôi phục ≈ 11–13 s |
| Loại A, B, C qua `-Inject` | `-Inject -Type A -Target orders-api` / `B gateway-api` / `C products-api -DurationSeconds 150` | (không có) — telemetry Kibana | A: lỗi; B: gateway; C: 5xx ≈ tỷ lệ niêm phong | A: 504/502; B: chưa thấy triệu chứng; C: 31,9% so với 31% |
| Bốc thăm mù phủ các nhóm | Cờ bật; 24 lần `-Start` rồi `-Restore` khi còn `pending` | (không có) | Phủ ≥ 6/8 nhóm; đầu ra chỉ `runId` + băm | Phủ 8/8 nhóm; đầu ra đúng 3 dòng |
| Bài mù trọn vẹn + gợi ý | `-Start` (giữ lần có độ trễ ≤ 100 s), `-Hint` mức 1–3, `-Restore`, `-Reveal` | (không có) | Mức 1–2 không lộ đích; `hint-log.json` đủ dòng; băm khớp | Đúng; bỏ cách mức vẫn ghi |
| Tương thích 028 | `-Start -Service gateway-api -FaultType B -DelaySeconds 0`; `-Reveal` lần chạy không có `state.json` | (không có) | Hành vi cũ không đổi | Đúng; in "CHẾ ĐỘ KHÔNG MÙ" |
| Gỡ Kubernetes của 025 | (không có) | (không có) — `rg`, `git diff` | Không còn kill-pod/kubectl; ansible/CI không đổi | `rg` rỗng (trừ chỗ trỏ sang 031); `git diff` trống |
| Dọn dẹp | Hai cờ về `false`, `-Restore` mọi lần chạy, chạy lại stack | (không có) | Mọi container healthy | Healthy |

### Tự động

Không có test tự động. Đây là sai lệch Nguyên tắc III do người dùng chốt, hạn tới khi spec D (tài liệu
troubleshoot chi tiết) hoàn tất; xem
[`specs/031-error-group-catalog/plan.md`](../../specs/031-error-group-catalog/plan.md), mục Complexity
Tracking. Danh mục, script và folder Postman chỉ được kiểm chứng bằng bảng thủ công ở trên.

## Kết luận

**PASS kèm ghi chú.** Cả 9 loại tiêm và khôi phục được bằng lệnh, từ chối đúng khi cờ tắt hoặc sai đích,
bốc mù phủ đủ 8 nhóm, gợi ý ba mức không lộ đáp án ở mức 1–2, hành vi 028 không đổi, và phần Kubernetes
của 025 đã gỡ mà hạ tầng Ansible không đổi.

Ghi chú: (1) loại B không cho triệu chứng dưới tải nền một tiến trình; (2) nhóm G chỉ tạo triệu chứng nhẹ;
(3) biến thể dừng identity-api đã bỏ vì không có triệu chứng; (4) không che đích với E, G, H, I; (5) không
có test tự động.

Chi tiết: [QA_Debt.md](QA_Debt.md) mục 031.
