# QA: Liveness/Readiness Probe cho mọi service trên Kubernetes

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. `deploy/ansible/inventories/services.yml` liệt kê 7 service, mỗi service khai `container_port` và `depends_on_database` (5 service DB = `true`, gateway/bff = `false`).
2. `roles/service_deployment/defaults/main.yml` chia 2 nhóm ngưỡng (`db_backed`/`stateless`); `db_backed.readiness` tái dùng đúng số liệu `x-service-healthcheck` của `docker-compose.yml`.
3. `deployment.yaml.j2` render `livenessProbe`/`readinessProbe` trỏ `/health/live`/`/health/ready` (FR-008) + `strategy.rollingUpdate.maxUnavailable: 0` tường minh.
4. **Lớp kiểm tra 1** (`tests/DeploymentManifestConventionTests`) tự render Jinja2 bằng C#, không gọi `ansible-playbook`.
5. **Lớp kiểm tra 2** (`scripts/ci/lint-deployment-manifests.sh`: `ansible-lint` + render thật + `kubeconform`) — phiên triển khai gốc chưa từng chạy được (Ansible không chạy native trên Windows).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — bật/tắt phụ thuộc rồi bấm Postman; phần Kubernetes dùng cụm Docker Desktop

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api parties-api identity-api` (kéo theo DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**, chạy folder
**`19 - Liveness / Readiness của mọi service`** (14 request, không cần token: liveness + readiness của 5 service có DB và 2 service stateless).

**Công tắc** (hạ tầng, không sửa mã): `docker compose -f docker-compose.local.yml stop orders-db` / `start orders-db` (DB của orders); `stop bff-api` / `start bff-api`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| FR-001/FR-002 — mọi service có 2 probe, mặc định khoẻ | Mặc định (DB và service đều bật) | `19` bước 01 → 14 | Liveness `200`, readiness `200` (nhóm DB có check `self-database` Healthy) | **14/14 xanh** |
| FR-003/US2 — DB tắt: liveness vẫn `200`, readiness `503` | `stop orders-db`, chờ ~12 giây | `19` bước 05 (orders readiness) và các bước còn lại | Chỉ readiness của orders lỗi; service khác không đổi; **không** restart | Readiness orders **`503`** (`self-database` Unhealthy, phản hồi mất ~5.3 giây), liveness `200`; 13/14 xanh (chỉ bước đó đỏ); `orders-api` `running`, `restarts=0` |
| Phục hồi khi DB bật lại | `start orders-db` | `19` bước 05 | Tự trở lại `200`, không thao tác thủ công | `200` sau **28 giây** (thời gian SQL Server khởi động); folder 14/14 xanh lại |
| Nhóm stateless — không phụ thuộc downstream | `stop bff-api` | (gọi `gateway` readiness) | Gateway readiness vẫn `200` | Gateway liveness `200`, readiness **`200`**; `bff-api` bật lại, ready sau 2 giây |
| US2 — Kubernetes: pod bị loại khỏi Service khi DB tắt, không bị restart *(ngoại lệ: dùng `kubectl`; đã nạp image thật vào worker, Deployment `baskets` theo mẫu `deployment.yaml.j2` — probe `initialDelaySeconds: 10`, readiness `failureThreshold: 20`, `maxUnavailable: 0`)* | `docker compose … stop baskets-db` trong khi pod đang chạy | (không có) | Pod `0/1` (NotReady), không restart, Service không còn endpoint | Lần đầu Ready sau **14 giây**. Sau **70 giây** pod vẫn `1/1` và còn trong endpoints (chưa đủ 20 lần lỗi × 5 giây); sau **130 giây**: `0/1 Running`, **`Restarts 0`**, `ENDPOINTS` rỗng, sự kiện `Readiness probe failed: … 503` (×25), liveness của pod vẫn `{"status":"Healthy"}` |
| K8s — phục hồi | `start baskets-db` | (không có) | Pod tự Ready lại | Ready lại sau **31 giây**, vẫn 0 restart |
| K8s — US2 rolling restart không gián đoạn | `kubectl rollout restart deploy/baskets`, thăm dò `/health/ready` qua Service mỗi 0.2 giây | (không có) | `maxUnavailable: 0`: luôn còn pod Ready | Rollout xong sau **13 giây**; **148/148 thăm dò `OK`, 0 lỗi** |
| Lớp kiểm tra 2 — `ansible-lint` *(ngoại lệ: công cụ chạy qua Docker, `MSYS_NO_PATHCONV=1`)* | (không có) — `ansible-lint roles/service_deployment deploy.yml` trong `deploy/ansible` | (không có) | 0 vi phạm | Còn vi phạm (`name[template]` ở `tasks/main.yml:34` và các `var-naming[no-role-prefix]`) — không đổi so với lượt trước, xem QA_Debt |
| Lớp kiểm tra 2 — render/`kubeconform` *(ngoại lệ)* | (không có) | (không có) | Render 7 manifest rồi validate | Không đổi: `--limit` không khớp host (exit 0 "xanh giả"), `--check` không ghi file — xem QA_Debt |
| Dọn dẹp | `start` DB/`bff-api`; xoá namespace `qa019`, image trong worker, ngắt worker khỏi mạng compose | (không có) | Không dữ liệu dư | Đã dọn (`kubectl get ns` không còn `qa019`); stack về mặc định |

### Tự động — lớp kiểm tra 1

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| SC-001 — inventory đủ đúng 7 service, mỗi service có `container_port` + `depends_on_database` | [`ServiceInventoryTests.cs:24`](../../tests/DeploymentManifestConventionTests/ServiceInventoryTests.cs#L24) · [`:52`](../../tests/DeploymentManifestConventionTests/ServiceInventoryTests.cs#L52) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~ServiceInventoryTests` |
| FR-001/002/003/005/006/008 — đủ 2 probe, đúng path/cổng, liveness không trỏ nhầm readiness, ngưỡng dương, ngưỡng readiness nhóm DB > nhóm stateless | [`ProbeDeclarationTests.cs:24`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L24) · [`:43`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L43) · [`:66`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L66) · [`:85`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L85) · [`:106`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L106) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~ProbeDeclarationTests` |
| FR-006/007/US3 — liveness threshold/period dương, initial delay 1–15 s | [`LivenessRestartTests.cs:28`](../../tests/DeploymentManifestConventionTests/LivenessRestartTests.cs#L28) · [`:59`](../../tests/DeploymentManifestConventionTests/LivenessRestartTests.cs#L59) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~LivenessRestartTests` |
| FR-009/US2 — `maxUnavailable: 0` | [`RolloutStrategyTests.cs:29`](../../tests/DeploymentManifestConventionTests/RolloutStrategyTests.cs#L29) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~RolloutStrategyTests` |

**Kết quả lượt QA này (2026-09-26)**: `DeploymentManifestConventionTests` **58/58 PASS** (khớp "26 task, 58/58" của tài liệu) — không đổi sau khi dịch comment.

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán với nhau và với mã; lớp kiểm tra 1 (58 test C#) xanh; trên hệ thống thật cấu trúc probe hoạt động đúng thiết kế: liveness tách khỏi DB (DB tắt vẫn `200`, 0 restart), readiness `503` rồi tự phục hồi, gateway/bff stateless không phụ thuộc downstream,
và trên Kubernetes thật pod bị loại khỏi Service (không restart) khi DB tắt và rolling restart không mất request (148/148). Ghi chú: (1) ngưỡng readiness nhóm DB (20 lần × 5 giây) khiến pod chỉ bị loại khỏi Service sau ~100 giây — DB gián đoạn ngắn hơn vẫn nhận traffic và trả lỗi; (2) lớp kiểm tra 2 (`ansible-lint` còn vi phạm, `--limit` "xanh giả",
`--check` mâu thuẫn) chưa đổi. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 019.
