# Implementation Plan: Liveness/readiness probe cho mọi service trên Kubernetes

**Branch**: `code/Liveness-readiness-probes-on-every-service` | **Date**: 2026-09-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/019-liveness-readiness-probes/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Mỗi service (parties, products, baskets, orders, identity, gateway, BFF) đã có sẵn hai endpoint sức
khỏe — `/health/live` và `/health/ready` — từ 001-scaffold-service-shells, nhưng chưa có manifest
triển khai Kubernetes nào trong repository khai báo chúng thành `livenessProbe`/`readinessProbe`.
Cách tiếp cận kỹ thuật: thêm một role Ansible dùng chung, render một template Deployment K8s duy
nhất (Jinja2) cho cả 7 service — tham số hóa theo từng service (cổng, có phụ thuộc cơ sở dữ liệu
hay không, độ trễ khởi động) thay vì viết tay khối probe lặp lại 7 lần — cùng với một bộ test quy
ước (convention test) mới xác nhận mỗi manifest render ra đều khai báo đúng cả hai probe, đúng
đường dẫn, và không để liveness phụ thuộc dependency ngoài.

## Technical Context

**Language/Version**: YAML + Jinja2 (template Ansible); Ansible ≥ 2.16. Không cần ngôn ngữ ứng dụng
mới — endpoint `/health/live` và `/health/ready` đã tồn tại và giữ nguyên hợp đồng (FR-008).

**Primary Dependencies**: Ansible collection `kubernetes.core` (module `k8s`) để áp dụng manifest
đã render vào cluster; không thêm công cụ templating mới ngoài Jinja2 sẵn có của Ansible.

**Storage**: N/A — tính năng không tạo, đọc, hay ghi dữ liệu nghiệp vụ.

**Testing**: Dự án xUnit mới `tests/DeploymentManifestConventionTests`, theo đúng khuôn mẫu convention
test đã có (`tests/ContainerConventionTests/DockerfileSharedProjectTests.cs`) — render template với
biến của từng service rồi assert cấu trúc probe. Bổ sung `ansible-lint` và `kubeconform`/`kubeval`
để xác thực schema tĩnh trong pipeline CI. Một kịch bản smoke test thủ công/định kỳ trên cluster
`kind` cục bộ (tài liệu tại `quickstart.md`) để xác nhận hành vi động (loại trừ traffic, tự khởi
động lại) mà test tĩnh không thể khẳng định.

**Target Platform**: Cụm Kubernetes tự vận hành (self-hosted), được cấp phát và cấu hình qua Ansible
— theo đúng "Technology and Infrastructure Constraints" của constitution.

**Project Type**: Hạ tầng dưới dạng mã (infrastructure-as-code) bổ sung vào monorepo hiện có — không
phải một service runtime mới; không có "frontend"/"backend" theo nghĩa ứng dụng cho tính năng này.

**Performance Goals**: Không có mục tiêu thông lượng riêng. Mỗi lần gọi probe phải hoàn tất rõ ràng
trong `timeoutSeconds` đã cấu hình; không được làm tăng tải đáng kể lên cơ sở dữ liệu của service
(readiness probe của service có DB chỉ mở kết nối kiểm tra, không truy vấn nghiệp vụ — hành vi này
đã tồn tại sẵn ở tầng endpoint, không thay đổi).

**Constraints**: KHÔNG được thay đổi hợp đồng của `/health/live`, `/health/ready` (FR-008). KHÔNG
được viết tay khối probe riêng cho từng service — chỉ một template dùng chung, tham số hóa qua biến
(nhất quán với cách constitution xử lý cấu hình observability dùng chung ở `ServiceDefaults`).
Liveness probe KHÔNG được gắn tag phụ thuộc dependency ngoài (FR-003).

**Scale/Scope**: 7 service (5 service nghiệp vụ + gateway + BFF); một template Deployment dùng
chung + một bộ biến tham số cho mỗi service.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không đổi quyền sở hữu dữ liệu; mỗi service vẫn triển khai độc lập qua manifest riêng của nó (render từ template dùng chung, không phải một manifest gộp). | PASS |
| II. Contract-First Integration | Không có hợp đồng HTTP/event mới; hợp đồng `/health/live`, `/health/ready` hiện có (specs/001) được giữ nguyên, chỉ được tham chiếu (FR-008). | PASS |
| III. Test-First Development | `tests/DeploymentManifestConventionTests` được viết trước, thất bại vì manifest/template chưa tồn tại, rồi mới thêm template để test pass — đúng Red-Green-Refactor. | PASS (kế hoạch ở Phase 2/tasks) |
| IV. Event-Driven by Default | Không liên quan — tính năng không thêm giao tiếp giữa service. | N/A |
| V. Tenant Isolation Is a Security Boundary | Không liên quan — probe không đi qua đường dẫn dữ liệu tenant, không nhận token. | N/A |
| VI. Secure by Default | Endpoint probe đã `AllowAnonymous` từ trước (bắt buộc vì probe không có token); manifest không đưa thêm secret nào — thống nhất với 018-cluster-secret-store. | PASS |
| VII. Observable by Default | Đây chính là yêu cầu "Every service MUST expose liveness and readiness probes" của Principle VII — tính năng này là phần còn thiếu để thỏa mãn nó ở tầng triển khai. | PASS (là mục tiêu chính) |
| VIII. Performance and Resilience Budgets | Tham số thời gian probe (FR-007) được đặt để không kích hoạt sai trong ngưỡng SLO khởi động bình thường; không có truy vấn nặng nào được thêm vào readiness check. | PASS |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible Delivery | Thay đổi manifest triển khai không phải "code" ứng dụng theo nghĩa cần feature toggle; khả năng rollback vốn có sẵn từ chính cơ chế của Kubernetes (revert manifest + áp dụng lại, tương đương `kubectl rollout undo`), thỏa mãn tinh thần "rollback không cần thay đổi mã nguồn ứng dụng" của nguyên tắc này. | PASS (lý giải, không phải vi phạm) |

Không có vi phạm nào cần biện minh tại Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/019-liveness-readiness-probes/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
deploy/
└── ansible/
    ├── roles/
    │   └── service_deployment/
    │       ├── defaults/
    │       │   └── main.yml            # giá trị mặc định probe (initialDelaySeconds, periodSeconds, timeoutSeconds, failureThreshold) theo nhóm service
    │       ├── templates/
    │       │   └── deployment.yaml.j2  # template Deployment K8s dùng chung, chứa khối livenessProbe/readinessProbe
    │       └── tasks/
    │           └── main.yml            # render + áp dụng manifest qua module kubernetes.core.k8s
    └── inventories/
        └── services.yml                # biến theo từng service: tên, cổng, có DB hay không, override thời gian probe nếu cần

tests/
└── DeploymentManifestConventionTests/  # dự án xUnit mới, theo khuôn mẫu ContainerConventionTests
    ├── DeploymentManifestConventionTests.csproj
    ├── ProbeTemplateRenderer.cs        # render deployment.yaml.j2 với biến của một service (không cần Ansible runtime)
    └── ProbeDeclarationTests.cs        # assert mọi service có đủ 2 probe, đúng path, liveness không phụ thuộc dependency
```

**Structure Decision**: Thêm một cây `deploy/ansible/` mới ở gốc repo cho hạ tầng triển khai — vị
trí này là hợp lý vì (a) chưa có manifest K8s hay thư mục Ansible nào tồn tại trong repo, và (b)
constitution cố định "Kubernetes, provisioned via Ansible" mà không nói tới một repo hạ tầng tách
biệt, nên đặt cùng monorepo giữ tính năng này liền mạch với mã nguồn service mà nó triển khai. Test
quy ước mới đặt tại `tests/DeploymentManifestConventionTests`, song song với `tests/ContainerConventionTests`
đã có cho Dockerfile, giữ đúng khuôn mẫu "test đọc file cấu hình, assert cấu trúc" mà repo đang dùng
cho các mối quan tâm hạ tầng tương tự.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh — bảng này để trống có chủ đích.
