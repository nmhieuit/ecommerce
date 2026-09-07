# Implementation Plan: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

**Branch**: `018-cluster-secret-store` | **Date**: 2026-09-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/018-cluster-secret-store/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Loại bỏ mọi secret hardcode (connection string, khóa, token) khỏi source code, `appsettings*.json`, và Dockerfile của các service backend, đồng thời chuẩn hoá cách mỗi service nhận secret tại runtime qua biến môi trường/file được Kubernetes `Secret` cung cấp (do External Secrets Operator đồng bộ từ Vault, theo ADR-0007). Cách tiếp cận kỹ thuật: (1) dọn sạch giá trị hardcode còn sót trong `appsettings.Development.json`; (2) thêm cơ chế fail-fast validate secret bắt buộc dùng chung trong `shared/ServiceDefaults`; (3) thêm hai stage CI mới (`ci/secret-scan` dùng gitleaks cho git history, `ci/image-secret-scan` dùng Trivy cho container image) vào `Jenkinsfile` theo đúng convention "stage-là-contract" của ADR-0012; (4) tạo manifest khai báo `Secret`/`ExternalSecret` mẫu cho từng service làm hợp đồng cho hạ tầng Vault/ESO (việc triển khai thật Vault/ESO nằm ngoài phạm vi plan này, xem Complexity Tracking).

## Technical Context

**Language/Version**: C# / .NET 10 (service code, `shared/ServiceDefaults`); Groovy (`Jenkinsfile`); Bash/PowerShell (`scripts/ci/*`); YAML (Kubernetes `Secret`/`ExternalSecret` manifest mẫu)

**Primary Dependencies**: ASP.NET Core configuration system (biến môi trường, `AddKeyPerFile`), `Microsoft.Extensions.Options` (`ValidateOnStart()`), gitleaks CLI, Trivy CLI (secret-scan mode). Không thêm NuGet package Vault client nào vào service code (xem research.md #2).

**Storage**: N/A trong repo — giá trị secret thật sống trong Vault (ngoài phạm vi plan này); repo chỉ chứa manifest khai báo tham chiếu tên secret, không chứa giá trị.

**Testing**: xUnit unit test cho logic validate/fail-fast mới trong `ServiceDefaults`; integration test xác nhận service dừng khởi động rõ ràng khi thiếu biến môi trường bắt buộc (dùng test harness hiện có từ spec 010); chạy stage `ci/secret-scan`/`ci/image-secret-scan` thật trên một PR thử để xác nhận chặn merge khi có secret giả lập, theo đúng cách ADR-0012 đã validate 5 stage hiện có.

**Target Platform**: Linux container trên self-hosted Kubernetes (theo constitution); không ảnh hưởng luồng local dev qua `docker-compose` (đã externalize secret qua `.env` từ trước).

**Project Type**: Thay đổi trong monorepo hiện có — sửa `shared/ServiceDefaults`, `appsettings.Development.json` của 6 service (parties, products, baskets, orders, identity, gateway/bff), `Jenkinsfile`; thêm manifest mẫu mới dưới `deploy/k8s/` (thư mục mới, chưa tồn tại trong repo).

**Performance Goals**: Không có yêu cầu hiệu năng runtime mới — bước validate cấu hình khi khởi động chỉ đọc `IConfiguration` đã nạp sẵn (không có I/O mạng bổ sung), tác động tới thời gian khởi động là không đáng kể.

**Constraints**: CI stage mới PHẢI fail-closed (secret-scanner lỗi/không chạy được = stage fail, không phải pass ngầm), nhất quán với nguyên tắc fail-closed đã áp dụng cho `ci/sonarqube-quality-gate` (ADR-0012); giá trị secret không bao giờ được in ra Jenkins console log.

**Scale/Scope**: 6 service backend + 1 shared component (`ServiceDefaults`) + pipeline CI dùng chung; quét toàn bộ lịch sử git của repository (không giới hạn theo nhánh).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Đánh giá | Kết quả |
|---|---|---|
| I. Service Autonomy and Bounded Context | Thay đổi tập trung ở `shared/ServiceDefaults` (đã là pattern chuẩn cho cross-cutting concern, xem spec 016/017); mỗi service vẫn tự khai báo secret nào nó cần, tự triển khai độc lập. | PASS |
| II. Contract-First Integration | Không phải API/event contract, nhưng feature định nghĩa "hợp đồng" tương đương: shape cấu hình sau khi dọn secret, và cấu trúc manifest `Secret`/`ExternalSecret` — xem `contracts/`. | PASS |
| III. Test-First Development | Logic fail-fast mới và hành vi CI gate mới PHẢI có test/PR thất bại trước khi implement, theo TDD chuẩn của repo. | PASS (áp dụng ở giai đoạn implement) |
| IV. Event-Driven by Default | Không có giao tiếp liên service mới. | N/A |
| V. Tenant Isolation Is a Security Boundary | Không đổi cách resolve tenant; chính sách per-tenant Vault (dynamic credential theo tenant) là mở rộng tương lai của ADR-0007, ngoài phạm vi feature này. | N/A (ghi nhận là giả định, không phải vi phạm) |
| VI. Secure by Default | Đây chính là principle mà feature này hiện thực hoá trực tiếp (secret không hardcode, inject runtime, deny-by-default khi thiếu secret). | PASS — mục tiêu cốt lõi |
| VII. Observable by Default | Lỗi fail-fast khi thiếu secret PHẢI là structured log kèm tên service, không log giá trị secret. | PASS (ràng buộc thiết kế, xem research.md #3) |
| VIII. Performance and Resilience Budgets | Không có outbound call runtime mới từ service (ESO/Vault vận hành ở tầng operator, ngoài app); không cần thêm timeout/retry policy trong app code. | PASS |
| IX. Frontend Discipline | Không có secret nào do frontend SPA tiêu thụ trong phạm vi feature này (frontend chỉ gọi BFF). | N/A |
| X. Toggle-Gated, Reversible Delivery | Xem Complexity Tracking — thay đổi CI-pipeline/startup-config không có ngữ nghĩa "toggle theo request" có ý nghĩa; rollback qua revert Jenkinsfile/config, theo đúng tiền lệ ADR-0012. | Deviation đã ghi nhận, xem Complexity Tracking |

Không có vi phạm nào chặn Phase 0 (một deviation duy nhất, đã biện minh và ghi nhận tại Complexity Tracking bên dưới).

## Project Structure

### Documentation (this feature)

```text
specs/018-cluster-secret-store/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── service-configuration-contract.md
│   ├── external-secret-manifest-contract.md
│   └── ci-secret-scan-stage-contract.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
└── ServiceDefaults/
    ├── ServiceDefaultsExtensions.cs      # + đăng ký validate-secret-on-start dùng chung
    ├── CorrelationIdMiddleware.cs        # (không đổi, tham chiếu để giữ pattern nhất quán)
    └── RequiredSecretsValidation.cs      # MỚI — IValidateOptions + ValidateOnStart() fail-fast

services/
├── parties/src/Parties.Api/appsettings.Development.json    # dọn secret hardcode
├── products/src/Products.Api/appsettings.Development.json  # dọn secret hardcode
├── baskets/src/Baskets.Api/appsettings.Development.json    # dọn secret hardcode
├── orders/src/Orders.Api/appsettings.Development.json      # dọn secret hardcode
├── identity/src/Identity.Api/appsettings.Development.json  # dọn secret hardcode
├── gateway/src/Gateway.Api/appsettings.Development.json    # rà soát (đã sạch theo khảo sát Phase 0)
└── bff/src/Bff.Api/appsettings.Development.json            # rà soát (đã sạch theo khảo sát Phase 0)

deploy/k8s/                                    # MỚI — chưa tồn tại trong repo trước feature này
├── README.md                                  # giải thích quan hệ với ADR-0007 (chưa provision)
└── <service>/
    ├── secret.example.yaml                    # K8s Secret placeholder (không chứa giá trị thật)
    └── external-secret.yaml                   # ExternalSecret trỏ tới Vault path của service

Jenkinsfile                                    # + stage 'secret scan' (ci/secret-scan)
                                                # + stage 'image secret scan' (ci/image-secret-scan)

.gitleaks.toml                                 # MỚI — cấu hình gitleaks (rule/allowlist tối thiểu)

tests/
└── (unit + integration test mới cho RequiredSecretsValidation, đặt cạnh test hiện có của từng service
    hoặc trong shared/ServiceDefaults nếu có test project riêng — xác định cụ thể ở tasks.md)
```

**Structure Decision**: Đây là thay đổi trong monorepo backend nhiều service đã có (không phải dự án mới). Không dùng cấu trúc Option 1/2/3 mẫu của template vì repo đã có cấu trúc `services/<name>/src|tests` + `shared/<Component>` cố định. Điểm mới duy nhất về cấu trúc thư mục là `deploy/k8s/` (manifest khai báo, không phải hạ tầng thật) và file cấu hình `.gitleaks.toml` ở gốc repo.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Principle X (Toggle-Gated, Reversible Delivery) không áp dụng theo nghĩa runtime feature-flag cho thay đổi này | Thay đổi gồm: (a) một CI gate (stage Jenkins mới) và (b) hành vi fail-fast lúc khởi động service khi thiếu cấu hình bắt buộc. Cả hai không phải hành vi phục vụ *request* của người dùng cuối nên không có "bật/tắt theo % traffic hay theo tenant" có ý nghĩa — CI gate hoặc chặn merge hoặc không, không có trạng thái trung gian; fail-fast khi thiếu secret bắt buộc không thể "tắt" mà vẫn giữ đúng FR-007 (nếu tắt được thì đó chính là hành vi cần loại bỏ). | Một feature toggle runtime (vd: đọc từ config/ database) cho "có validate secret lúc khởi động hay không" tự nó lại là một secret-configuration surface mới cần bảo vệ, và cho service khởi động ở trạng thái không xác định khi toggle tắt — mâu thuẫn trực tiếp với FR-007. Rollback thực hiện qua `git revert` trên `Jenkinsfile`/`ServiceDefaults`, đúng tiền lệ đã chấp nhận cho `ci/sonarqube-quality-gate` (ADR-0012), vốn cũng không có toggle runtime. |
