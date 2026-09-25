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

### Tự động — lớp kiểm tra 1

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| SC-001 — inventory đủ đúng 7 service, mỗi service có `container_port` + `depends_on_database` | [`ServiceInventoryTests.cs:24`](../../tests/DeploymentManifestConventionTests/ServiceInventoryTests.cs#L24) · [`:52`](../../tests/DeploymentManifestConventionTests/ServiceInventoryTests.cs#L52) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~ServiceInventoryTests` |
| FR-001/002/003/005/006/008 — đủ 2 probe, đúng path/cổng, liveness không trỏ nhầm readiness, ngưỡng dương, ngưỡng readiness nhóm DB > nhóm stateless | [`ProbeDeclarationTests.cs:24`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L24) · [`:43`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L43) · [`:66`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L66) · [`:85`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L85) · [`:106`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs#L106) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~ProbeDeclarationTests` |
| FR-006/007/US3 — liveness threshold/period dương, initial delay 1–15 s | [`LivenessRestartTests.cs:28`](../../tests/DeploymentManifestConventionTests/LivenessRestartTests.cs#L28) · [`:59`](../../tests/DeploymentManifestConventionTests/LivenessRestartTests.cs#L59) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~LivenessRestartTests` |
| FR-009/US2 — `maxUnavailable: 0` | [`RolloutStrategyTests.cs:29`](../../tests/DeploymentManifestConventionTests/RolloutStrategyTests.cs#L29) | `dotnet test tests/DeploymentManifestConventionTests --filter FullyQualifiedName~RolloutStrategyTests` |

**Kết quả lượt QA này (2026-09-24)**: `DeploymentManifestConventionTests` **58/58 PASS** (khớp "26 task, 58/58" của tài liệu) — không đổi sau khi dịch comment.

### Thủ công — lớp kiểm tra 2 (lần đầu chạy thật, qua Docker)

Máy QA không cài `ansible-lint`/`kubeconform` nên chạy qua Docker (`pipelinecomponents/ansible-lint`, `ghcr.io/yannh/kubeconform`; mount thư mục tại `/code`, `MSYS_NO_PATHCONV=1`).

| Bước | Lệnh (trong `deploy/ansible`) | Kỳ vọng | **Đã quan sát** |
|---|---|---|---|
| `ansible-lint` | `ansible-lint roles/service_deployment deploy.yml` | 0 vi phạm | **7 vi phạm, exit 2** (6 `var-naming[no-role-prefix]`, 1 `name[template]`) — script CI dùng `set -eu` nên dừng ngay tại đây |
| Render từng service | `ansible-playbook deploy.yml --check --diff --limit orders` | Render manifest `orders` | `skipping: no hosts matched`, **exit 0** (`--limit` lọc theo host, còn play chạy `hosts: localhost` + `loop:`) |
| Render cả vòng lặp | `ansible-playbook deploy.yml --check --diff` (bỏ `--limit`) | Render 7 manifest | Render đúng `parties` (diff khớp `deployment.yaml.j2`) rồi **fail**: task `kubernetes.core.k8s` không tìm thấy `.rendered/parties.deployment.yaml` (`template` ở `--check` không ghi file) |
| `kubeconform` trên file thiếu | `kubeconform -strict .rendered/orders.deployment.yaml` | Valid | `no such file or directory`, exit 1 (hệ quả của dòng `--limit`) |
| US2/US3 động trên cluster `kind` (quickstart Bước 3–5) | (không dựng lại) | — | Dùng bằng chứng gốc ở `technical-debt.md` mục 019 (gateway ready ngay; orders ready fail `503` nhưng liveness không fail, 0 restart ~90 s; rolling restart giữ pod cũ `1/1`) |

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán với nhau và với mã; lớp kiểm tra 1 (58 test C#) xanh; cấu trúc probe (ngưỡng tái dùng số liệu vận hành, `maxUnavailable: 0`, liveness tách readiness)
đúng tài liệu và đã có bằng chứng `kind` thật từ phiên gốc. Ghi chú — 3 vấn đề ở lớp kiểm tra 2, lần đầu chạy được thật: (1) `ansible-lint` báo 7 vi phạm nên CI stage
sẽ đỏ ngay dòng đầu; (2) `--limit "$service"` trong script không khớp host nào, exit 0 "xanh giả" rồi `kubeconform` fail vì thiếu file (chẩn đoán sai hướng); (3) `--check` tự mâu thuẫn với chuỗi
`template` → `k8s` của role. Không ảnh hưởng hành vi runtime. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 019.
