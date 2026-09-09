# Implementation Plan: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

**Branch**: `code/Declare-per-service-SLOs-in-manifest` | **Date**: 2026-09-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/021-declare-service-slos/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Rà soát thực tế cho thấy phần lớn nội dung của tính năng này đã tồn tại nhưng chưa được bảo vệ hay
chính thức hoá: cả 7 `service-manifest.yaml` (parties, products, baskets, orders, identity, gateway,
bff) đã khai báo đủ 4 chỉ tiêu SLO khớp đúng bộ mặc định theo phân loại của constitution Principle
VIII, và một dashboard Kibana đo liên tục 3 chỉ số SLO từ dữ liệu OTel thật (`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`,
export tại `dashboards/slo-van-hanh-hang-ngay.ndjson`) đã được dựng và xác minh khớp dữ liệu thô.
Cả hai đều làm **thủ công, không có gì bảo vệ khỏi trôi dạt (drift)**: không có test nào đọc
`service-manifest.yaml`, không có gì ngăn một service mới thiếu SLO hoặc một giá trị bị sửa sai lệch
âm thầm; dashboard là một tài liệu vận hành rời rạc, chưa được xem là một phần chính thức của tính
năng này với hợp đồng rõ ràng.

Cách tiếp cận: (1) thêm một dự án xUnit quy ước mới (`tests/ServiceManifestSloConventionTests`),
theo đúng khuôn mẫu `DeploymentManifestConventionTests`/`CrossServiceIsolation.Tests`, đọc trực tiếp
cả 7 file `service-manifest.yaml` và assert đủ 4 chỉ tiêu SLO, khớp mặc định theo phân loại hoặc có
lý do ngoại lệ; (2) thêm một trường có cấu trúc `slos.justification` vào schema (máy đọc được, không
chỉ comment) làm cơ chế phòng ngừa cho một ngoại lệ tương lai theo FR-003; (3) chính thức hoá dashboard
Kibana đã có làm cơ chế đo lường liên tục của User Story 3, ghi lại hợp đồng (bất biến) mà nó phải
thỏa và một `quickstart.md` lặp lại đúng 3 kịch bản kiểm thử của Jira SCRUM-29 — không xây lại
dashboard hay hệ thống telemetry mới.

**Cập nhật sau khi triển khai (Phase 3–4, xem tasks.md T007–T008)**: Hai điều chỉnh so với dự tính
ban đầu, cả hai đều do chạy test thật phát hiện, không phải do đổi phạm vi:
1. `services/identity/src/Identity.Api/service-manifest.yaml` thiếu hẳn `service.classification` —
   một lỗ hổng thật ngoài dự kiến của research.md Quyết định 0 (đã sửa: thêm
   `classification: internal-service-api`, khớp đúng 4 giá trị SLO hiện có).
2. Giả định ban đầu rằng "`bff` có một ngoại lệ SLO cần chuyển từ comment sang trường có cấu trúc"
   là **sai**: `bff` khớp đúng mặc định của CHÍNH phân loại `client-facing-bff` của nó — hai hồ sơ mặc
   định của constitution (client-facing-bff, internal-service-api) song song, không cái nào là ngoại
   lệ của cái kia. `SloDefaultComplianceTests` xác nhận 7/7 service khớp mặc định của chính phân loại
   của mình; KHÔNG sửa `bff` để tránh đưa dữ liệu giả vào manifest — chi tiết tại research.md Quyết
   định 0/2 (đã cập nhật).

## Technical Context

**Language/Version**: C#/.NET 10 cho dự án test quy ước mới (xUnit) — không có ngôn ngữ ứng dụng mới.
YAML cho schema `slos.justification` mới (không có instance nào dùng tới hiện tại — xem "Cập nhật sau
khi triển khai" ở Summary) và cho việc bổ sung `service.classification` còn thiếu ở `identity`.

**Primary Dependencies**: `YamlDotNet` để parse `service-manifest.yaml` — cùng thư viện
`tests/DeploymentManifestConventionTests` đã dùng cho manifest triển khai, giữ nhất quán cách đọc
YAML cấu hình trong repo thay vì thêm một parser khác.

**Storage**: N/A cho bản thân tính năng — dữ liệu đo lường liên tục (US3) đọc từ Elasticsearch đã
được nạp bởi pipeline OTel có sẵn (017-otel-servicedefaults-elastic), không tạo, không ghi dữ liệu
nghiệp vụ mới.

**Testing**: Dự án xUnit mới `tests/ServiceManifestSloConventionTests`, theo khuôn mẫu convention-test
đã có trong repo (`tests/DeploymentManifestConventionTests`, `tests/CrossServiceIsolation.Tests`) —
đọc file trên đĩa, không cần cluster hay Elastic thật để chạy trong CI. Phần đo lường liên tục (US3)
được xác thực bằng kịch bản thủ công/định kỳ tại `quickstart.md`, đối chiếu dashboard đã import với
dữ liệu Elasticsearch thật — không chặn mọi PR (cùng logic với cách 019 tách smoke test động sang
kịch bản riêng).

**Target Platform**: Elastic stack (Elasticsearch + Kibana) đã triển khai theo 017-otel-servicedefaults-elastic;
dự án test quy ước mới chạy trong pipeline CI hiện có (Jenkins), không cần hạ tầng mới.

**Project Type**: Bổ sung một dự án test quy ước + tài liệu hợp đồng vào monorepo hiện có — không
phải service runtime mới, không có "frontend"/"backend" theo nghĩa ứng dụng cho tính năng này.

**Performance Goals**: Không có mục tiêu hiệu năng riêng cho bản thân tính năng — bản thân nội dung
của tính năng chính là các con số ngân sách hiệu năng (Principle VIII), không phải một ràng buộc lên
việc thực thi tính năng đó.

**Constraints**: KHÔNG được thay đổi hành vi phản hồi của bất kỳ endpoint nào hiện có (FR-008).
KHÔNG được sửa giá trị SLO đã đúng của bất kỳ service nào để "khớp" một giả định — chỉ sửa khi test
phát hiện một lỗ hổng thật (như `identity` thiếu `classification`), không bao giờ thêm dữ liệu giả
(như một `slos.justification` không có căn cứ) chỉ để làm test pass. KHÔNG xây lại dashboard hay
pipeline telemetry đã có — chỉ tham chiếu và chính thức hoá.

**Scale/Scope**: 7 `service-manifest.yaml` (đọc bởi test mới, sửa 1 file); 1 dashboard Kibana đã có
(tham chiếu, không xây lại); 1 dự án test quy ước mới.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không đổi quyền sở hữu dữ liệu hay ranh giới service; mỗi service vẫn tự khai báo SLO của chính nó trong manifest riêng. | PASS |
| II. Contract-First Integration | Không có hợp đồng HTTP/event mới; hợp đồng đầu ra mới duy nhất là hình dạng YAML của khối `slos` (contracts/service-manifest-slo-shape.md), được viết trước khi sửa manifest `bff`. | PASS |
| III. Test-First Development | `tests/ServiceManifestSloConventionTests` được viết trước; chạy thật phát hiện `identity` thiếu `service.classification` (FAIL → sửa manifest → PASS, đúng Red-Green). Giả định ban đầu về một FAIL ở `bff` không thành hiện thực (`bff` vốn đã đúng) — Test-First vẫn được tôn trọng vì test được viết và chạy trước khi kết luận bất kỳ điều gì cần sửa, kể cả kết luận "không cần sửa". | PASS (đã thực hiện — xem tasks.md T007/T008) |
| IV. Event-Driven by Default | Không liên quan — tính năng không thêm giao tiếp giữa service. | N/A |
| V. Tenant Isolation Is a Security Boundary | Không liên quan — không có đường dẫn dữ liệu tenant nào bị chạm tới. | N/A |
| VI. Secure by Default | Không liên quan — không có endpoint mới, không có secret mới; dashboard Kibana đọc dữ liệu telemetry đã có, qua quyền truy cập Kibana hiện có. | N/A |
| VII. Observable by Default | Đây chính là "MUST be measured continuously from the telemetry required by Principle VII" — tính năng này là phần đo lường (US3) dựa trực tiếp trên dữ liệu OTel mà Principle VII đã yêu cầu, không thêm instrumentation mới. | PASS (là mục tiêu chính) |
| VIII. Performance and Resilience Budgets | Đây chính là nội dung cốt lõi của Principle VIII ("Every service MUST declare its SLOs... and those SLOs MUST be measured continuously"); tính năng này là phần còn thiếu để nguyên tắc này được bảo vệ tự động thay vì chỉ đúng tại một thời điểm kiểm tra thủ công. | PASS (là mục tiêu chính) |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible Delivery | Thêm một test quy ước và chính thức hoá tài liệu/dashboard không phải "code" ứng dụng có hành vi runtime cần feature toggle; rollback là revert commit (xoá test/tài liệu), không ảnh hưởng release đang chạy — thỏa tinh thần "rollback không cần thay đổi mã ứng dụng". | PASS (lý giải, không phải vi phạm) |

Không có vi phạm nào cần biện minh tại Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/021-declare-service-slos/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
services/
└── identity/src/Identity.Api/service-manifest.yaml   # sửa: bổ sung service.classification còn thiếu (phát hiện thật bởi T007, không dự tính ban đầu)

tests/
└── ServiceManifestSloConventionTests/       # dự án xUnit mới, theo khuôn mẫu DeploymentManifestConventionTests
    ├── ServiceManifestSloConventionTests.csproj
    ├── ServiceManifestFixture.cs             # tìm & parse cả 7 services/*/src/*/service-manifest.yaml
    ├── PlatformSloDefaults.cs                # 2 hồ sơ mặc định (client-facing-bff, internal-service-api) từ constitution Principle VIII
    ├── SloDeclarationTests.cs                # FR-001: đủ 4 chỉ tiêu, không placeholder
    └── SloDefaultComplianceTests.cs          # FR-002/FR-003: khớp mặc định hoặc có slos.justification

docs/kibana-quan-sat-he-thong/
├── 06-dashboard-slo-van-hanh-hang-ngay.md    # đã có — không sửa, chỉ tham chiếu làm nguồn US3
└── dashboards/slo-van-hanh-hang-ngay.ndjson  # đã có — artifact được feature này chính thức hoá làm cơ chế đo liên tục
```

**Structure Decision**: Không tạo cây thư mục hạ tầng mới — tính năng chỉ thêm một dự án test quy
ước cạnh các dự án cùng loại đã có (`tests/DeploymentManifestConventionTests`,
`tests/CrossServiceIsolation.Tests`), sửa đúng 1 file YAML hiện có (`bff`), và chính thức hoá (không
di chuyển, không viết lại) artifact dashboard đã tồn tại dưới `docs/kibana-quan-sat-he-thong/`. Giữ
nguyên vị trí tài liệu Kibana hiện có vì nó đã là nơi duy nhất trong repo ghi lại hạ tầng quan sát
(017-otel-servicedefaults-elastic) — tạo một cây tài liệu song song sẽ vi phạm chính tinh thần
Principle I ("phức tạp phải tương xứng với miền", không nhân bản một cách không cần thiết).

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh — bảng này để trống có chủ đích.
