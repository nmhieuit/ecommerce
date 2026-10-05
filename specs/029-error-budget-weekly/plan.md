# Implementation Plan: Ngân sách lỗi theo tuần lịch giờ Việt Nam (thay thế tháng lịch của đặc tả 027)

**Branch**: `feature/029-error-budget-weekly` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/029-error-budget-weekly/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Đổi chu kỳ ngân sách lỗi từ tháng lịch sang tuần lịch giờ Việt Nam (thứ Hai 00:00 → Chủ nhật 23:59, UTC+7). Đồng thời nới SLO nền tảng: khả dụng 99%/tuần, 5xx dưới 1%. Cơ chế 027 giữ nguyên; chỉ đổi giá trị:

- **Hiến chương 1.0.0 → 2.0.0**: sửa hai dòng mặc định của Nguyên tắc VIII.
- **7 manifest**:
  - khối `slos`: `99%   # weekly`, `max-5xx-ratio: 1%`;
  - khối `error-budget-policy`: `window: calendar-week`, tỷ lệ khả dụng/5xx `1%`.
- **4 rule Kibana ES|QL của 027**:
  - ranh giới `DATE_TRUNC(1 week, …)`, tỷ lệ `0.01`;
  - cửa sổ rule mốc 7 ngày, rule đóng băng 14 ngày;
  - ngưỡng ngày đạt SLO 5xx `0.01`;
  - chu kỳ giữ 5 phút.
- **Rule `incident-fast-detection` (028)**: ngưỡng 5xx `1%`.
- **Dashboard**: sửa tạm 3 Discover session ngân sách sang "tuần này" / `now-7d`, và panel text sang 99%/tuần. Không tách dashboard (spec B).
- **Test viết trước**: `PlatformSloDefaults`, `ErrorBudgetPolicyTests`, `ErrorBudgetRuleDefinitionTests` (thêm kiểm tra ranh giới tuần, cửa sổ rule, ngưỡng ngày đạt SLO).
- **Tài liệu**: sửa tại chỗ toàn bộ tài liệu 021/027/028; xoá bằng chứng đo cũ khỏi tài liệu vận hành và spec 027/028; tạo bộ tài liệu 029.

Chi tiết quyết định: [research.md](./research.md).

## Technical Context

**Language/Version**: C#/.NET 10 (chỉ test); ES|QL (rule, Discover session); YAML (manifest); Markdown (hiến chương, tài liệu); drawio XML (sơ đồ); Postman collection v2.1 JSON.

**Primary Dependencies**: Kibana/Elasticsearch 9.4.4 license Basic (rule `.es-query`, connector Index, Saved Objects API) như 027; xUnit + YamlDotNet + System.Text.Json (dự án test có sẵn); newman 6.2.2 qua `npx.cmd` (như 028).

**Storage**: Không thêm gì. Dùng lại index `slo-error-budget-events` và alerts-as-data. Elastic đang trống (volume đã xoá).

**Testing**: xUnit trong `tests/ServiceManifestSloConventionTests` (sửa + thêm kiểm tra, viết trước và chạy thấy đỏ). Hành vi trên Kibana thật kiểm theo [quickstart.md](./quickstart.md), không chặn PR. Rule 028 không có test (người dùng chốt).

**Target Platform**: Docker Compose local (nơi duy nhất có Elastic stack). CI Jenkins chạy test xUnit.

**Project Type**: Sửa cấu hình quan sát, manifest, test và tài liệu trong monorepo microservices. Không có code production, service hay endpoint mới.

**Performance Goals**: 4 rule × mỗi 5 phút, quét ≤ 7 ngày (rule mốc) và ≤ 14 ngày (rule đóng băng). Khối lượng quét ít hơn 027 (31/62 ngày).

**Constraints**:
- Không đổi hành vi endpoint (FR-015). Không đổi ngưỡng độ trễ.
- Không commit: người dùng tự commit.
- Không sửa lịch sử: mục cũ `QA_Debt`, `ket-qua/`, `specs/002`.
- Dọn Elastic chỉ khi người dùng xác nhận lại (FR-018).

**Scale/Scope**:
- Cấu hình và test: 1 hiến chương, 7 manifest, 3 file test (+ `PlatformSloDefaults`), 2 file export rule, 1 file export dashboard.
- Tài liệu: khoảng 45 file tài liệu của 021/027/028; bộ tài liệu 029 gồm 3 file 029, 3 file debt, 3 drawio, 1 folder Postman.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Đánh giá theo hiến chương **hiện hành 1.0.0** và theo bản **2.0.0** sau sửa đổi.

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Mỗi service tự mang chính sách trong manifest; không thêm phụ thuộc giữa service. | PASS |
| II. Contract-First | 3 contract (`contracts/`) viết trước khi sửa hiến chương, manifest, rule, test. | PASS |
| III. Test-First | Sửa `PlatformSloDefaults` và 2 lớp test, thêm kiểm tra tuần, chạy thấy ĐỎ trước khi sửa manifest/rule. Rule 028 không có test: đây là sai lệch đã ghi nhận của 028, spec này không mở rộng nó. | PASS (tách ở tasks.md) |
| IV. Event-Driven | Không thêm giao tiếp giữa service. | N/A |
| V. Tenant Isolation | Không chạm đường dữ liệu tenant. | N/A |
| VI. Secure by Default | Không thêm secret. Dùng lại `KIBANA_ENCRYPTION_KEY` (Vùng 2). Cờ tiêm lỗi mặc định tắt. | PASS |
| VII. Observable by Default | Dùng telemetry OTel có sẵn; không đổi instrumentation. | PASS |
| VIII. Performance and Resilience Budgets | **Thay đổi chính chủ đích.** Theo 1.0.0, SLO 99%/1% là lệch mặc định. Sửa đổi 2.0.0 đổi chính mặc định đó, đi cùng PR với lý do và tác động chuyển đổi (Governance). Sau sửa đổi, 7 manifest tuân mặc định, không cần ngoại lệ. | PASS với điều kiện: PR chứa sửa đổi hiến chương và được người duy trì phê duyệt |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible | Rule bật/tắt trên UI không cần redeploy. Quay lui = import lại ndjson cũ và hoàn tác manifest/hiến chương bằng git. Tiêm lỗi dùng lại cờ `Chaos:AllowFaultInjection` của 027. | PASS |
| Governance — Amendments | "Amendments require a pull request that states the rationale and the migration impact... plus approval from the platform maintainers." Sync Impact Report ghi lý do và tác động ([contracts/constitution-amendment.md](./contracts/constitution-amendment.md)). Người dùng tự commit/mở PR và phê duyệt; plan không tự phê duyệt. | PASS với cùng điều kiện ở Nguyên tắc VIII |

**Gate**: qua. Không có vi phạm cần biện minh. Điều kiện duy nhất là sửa đổi hiến chương phải nằm trong cùng PR (người dùng chốt).

**Re-check sau Phase 1**: data-model và contracts không thêm service, secret, index hay hợp đồng HTTP/event mới. Sửa đổi hiến chương đã có contract riêng với bất biến kiểm được (bất biến 3 ràng `PlatformSloDefaults` với hiến chương). Kết quả giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/029-error-budget-weekly/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── constitution-amendment.md
│   ├── error-budget-policy-manifest-shape.md
│   └── error-budget-alert-rules-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
.specify/memory/constitution.md                      # sửa: 2 dòng Nguyên tắc VIII, 2.0.0, Sync Impact Report

services/*/src/*.Api/service-manifest.yaml           # sửa (7 file): slos 99%/1%, error-budget-policy calendar-week + 1%

tests/ServiceManifestSloConventionTests/
├── PlatformSloDefaults.cs                           # sửa: 99% / 1%
├── SloDefaultComplianceTests.cs                     # sửa comment con số
├── ErrorBudgetPolicyTests.cs                        # sửa: calendar-week, 1%
├── ErrorBudgetRuleDefinitionTests.cs                # sửa tỷ lệ + thêm bất biến 11–13
└── ServiceManifestModel.cs                          # sửa comment ví dụ (0.1%)

docs/kibana-quan-sat-he-thong/
├── alerts/error-budget-rules.ndjson                 # export lại (4 rule + connector)
├── alerts/incident-fast-detection-rule.ndjson       # export lại (ngưỡng 1%)
├── dashboards/slo-van-hanh-hang-ngay.ndjson         # export lại (3 Discover session + panel text)
├── alerts/README.md, 06-*.md, 07-*.md, 08-*.md      # sửa tại chỗ; xoá bằng chứng đo cũ (07/08/README)
└── 00-tong-quan-lo-trinh.md                         # sửa nếu nhắc tháng

postman/ecommerce.postman_collection.v2.json         # thêm folder 29; sửa mô tả 27/28 nếu nêu tháng/0.1%

docs/PO|QA|architecture/029_*_ngân sách lỗi theo tuần lịch.md       # mới
docs/PO/functional-debt.md, docs/QA/QA_Debt.md, docs/architecture/technical-debt.md   # thêm mục 029; sửa con số ở mục 021/027/028 (trừ mục cũ QA_Debt)
docs/diagrams/029-error-budget-weekly-{component,flow-nghiep-vu,sequence}.drawio      # mới
```

**Structure Decision**: Không tạo dự án hay thư mục mới ngoài `specs/029-*` và các file tài liệu theo khuôn có sẵn. Mọi sửa đổi nằm đúng chỗ của hiện vật 021/027/028.

### Phạm vi sửa tài liệu 021/027/028 (sửa tại chỗ, FR-016)

Danh sách lấy từ lệnh tìm `calendar-month|ngân sách (lỗi )?tháng|1 month|monthly availability|# monthly|99\.9 ?%|0\.1 ?%|0\.001|err_pct >= 0\.1` trên `695bccb`:
- **021**: `specs/021-declare-service-slos/{spec,research,data-model,tasks}.md`, `contracts/service-manifest-slo-shape.md`, `docs/QA/021_QA_*.md`, `docs/spec-summary-vi/021-declare-service-slos.json`.
- **027**: `specs/027-error-budget-alerting/*`, `docs/PO|QA|architecture/027_*`, 2 drawio 027 có nhắc tháng (rà cả 3).
- **028**: `specs/028-incident-oncall-drill/{spec,research,tasks}.md`, `contracts/{fast-detection-rule,incident-record}-contract.md`, `docs/PO|architecture/028_*`, `docs/diagrams/028-incident-oncall-drill-component.drawio`.
- **Chung**:
  - `docs/kibana-quan-sat-he-thong/06|07|08-*.md`, `alerts/README.md`;
  - `docs/superpowers/{plans,specs}/2026-09-08-dashboard-slo-*.md`;
  - `docs/PO/functional-debt.md`, `docs/architecture/technical-debt.md`;
  - mô tả trong collection Postman.
- **Không sửa**: mục cũ trong `docs/QA/QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/tasks.md`.

Lệnh tìm phải được chạy lại ở task cuối (SC-006). Mọi kết quả còn lại phải thuộc danh sách "không sửa" hoặc phần lịch sử phiên bản của hiến chương.

## Điểm dừng bắt buộc khi triển khai

- [research.md](./research.md) V1 (đầu tuần = thứ Hai giờ Việt Nam) và V2 (cửa sổ 7 ngày không hụt đầu tuần) phải được kiểm chứng trên Kibana 9.4.4 thật ở task đầu tiên cần stack. Sai thì **dừng và hỏi người dùng**.
- Trước khi dọn Elastic: **hỏi người dùng**.
- Các tên chưa được chốt sẽ hỏi ở phiên `/speckit-tasks`, không tự đặt:
  - tên folder Postman 029;
  - tiêu đề chính xác của 3 panel "tuần này";
  - đường dẫn/tên 3 file drawio 029.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh. Bảng này để trống có chủ đích.
