# Implementation Plan: Loại span health khỏi công thức ngân sách lỗi

**Branch**: `feature/033-exclude-health-spans` | **Date**: 2026-10-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/033-exclude-health-spans/spec.md`

## Summary

Công thức ngân sách hiện tính mọi span Server nên health check của Docker (`curl` mỗi 5 giây) chiếm toàn bộ mẫu số và một span chậm lúc khởi động nguội làm ngân sách vọt 14–18% dù chưa có request nghiệp vụ. Thay đổi:

- **Loại span có đường dẫn bắt đầu bằng `/health`** khỏi 4 rule 027 (`error-budget-50/75/100`, `error-budget-frozen`), rule 028 `incident-fast-detection`, các panel ES|QL của dashboard `Ngân sách lỗi tuần` và 6 panel Lens đọc traces của dashboard `Xử lý sự cố`. Điều kiện dùng `COALESCE(attributes.url.path, "")` (bắt buộc, F3).
- **Khai báo `excluded-path-prefixes: [/health]`** trong `error-budget-policy` của cả 7 manifest; test đối chiếu manifest ↔ rule.
- **Chỉ báo health lỗi**: một rule cảnh báo mới (≥ 50% span health của service trả 5xx trong 5 phút) và một panel trên dashboard Xử lý sự cố; không đụng ngân sách.
- **Postman**: đổi đường dẫn tiêm lỗi/độ trễ của 26 request (folder 25, 27, 29a, 30a) sang đường dẫn không phải health. `scripts/incident-drill.ps1` không đổi.
- **Tài liệu và bộ tài liệu 033** theo khuôn 027–030.

Quyết định và dữ kiện đo thật: [research.md](./research.md).

## Technical Context

**Language/Version**: ES|QL (rule, Discover session), KQL (Lens), YAML (manifest), C#/.NET 10 (chỉ test), Markdown, drawio XML, Postman collection v2.1 JSON.

**Primary Dependencies**: Kibana/Elasticsearch 9.4.4 license Basic (đang chạy `localhost:5601/9200`); Dashboards REST API (`PUT /api/dashboards/{id}`) và Saved Objects API (import/export) như 030; xUnit + System.Text.Json + YamlDotNet (`tests/ServiceManifestSloConventionTests`); newman 6.2.2 qua `npx.cmd`.

**Storage**: Không thêm index hay service. Dùng lại alerts-as-data và `slo-error-budget-events`.

**Testing**: xUnit trong `tests/ServiceManifestSloConventionTests`: sửa `ServiceManifestModel`, `ErrorBudgetPolicyTests`, `ErrorBudgetRuleDefinitionTests`, `IncidentFastDetectionRuleDefinitionTests`; thêm lớp test cho rule health lỗi. Test viết trước, đỏ thật rồi mới sửa (D8). Hành vi trên Kibana thật kiểm theo [quickstart.md](./quickstart.md), không chặn PR.

**Target Platform**: Docker Compose local (nơi duy nhất có Elastic stack). CI Jenkins chạy xUnit.

**Project Type**: Cấu hình quan sát + manifest + test + tài liệu trong monorepo microservices. Không có endpoint, service hay code production mới.

**Performance Goals**: Điều kiện loại thêm một phép `COALESCE`/`LIKE` mỗi span; rule vẫn quét ≤ 7/14 ngày, chu kỳ 5 phút như 027/029. Rule health lỗi quét 5 phút mỗi lần chạy.

**Constraints**:
- Không đổi SLO, tỷ lệ cho phép, ngưỡng, mốc, cửa sổ, chu kỳ ngân sách, hiến chương, hành vi endpoint (FR-013).
- Không commit: người dùng tự commit.
- Không sửa lịch sử: mục cũ `QA_Debt`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.
- V1–V5 sai là **dừng và hỏi**, không tự lùi.
- Không xoá object Kibana đang chạy và không dọn Elastic khi chưa hỏi lại ngay trước lúc làm (FR-015).
- Tên rule/tag/chu kỳ/panel/đường dẫn tiêm lỗi/tên file chưa chốt: hỏi ở `/speckit-tasks`, không tự đặt.

**Scale/Scope**: 7 manifest, 5 rule sửa + 1 rule mới (1 file export mới), 2 dashboard (7 panel ES|QL ngân sách, 6 panel Lens, 1 panel mới), khoảng 4 file test, 26 request Postman, khoảng 40–60 file tài liệu, bộ tài liệu 033 (3 file, 3 file debt, 3 drawio, 1 folder Postman).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* Hiến chương hiện hành **2.0.0**.

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Mỗi service tự khai báo tiền tố loại trong manifest của mình; không thêm phụ thuộc giữa service. | PASS |
| II. Contract-First | 2 contract (`contracts/`) viết trước; contract manifest 029 cập nhật trước khi sửa 7 manifest, rule và test. | PASS |
| III. Test-First | Test (manifest, điều kiện loại ở 5 rule, rule health lỗi mới) viết trước và chạy ĐỎ thật vì rule/manifest chưa có điều kiện/khoá; không cần đổi giá trị giả. Dashboard là cấu hình, kiểm theo quickstart. | PASS (tách ở tasks.md) |
| IV. Event-Driven | Không đụng. | N/A |
| V. Tenant Isolation | Không đụng đường dữ liệu tenant. | N/A |
| VI. Secure by Default | Không thêm secret; không mở thêm dữ liệu hiển thị (panel health chỉ có tên service và số đếm). | PASS |
| VII. Observable by Default | Giữ và làm rõ tín hiệu: ngân sách chỉ tính request nghiệp vụ, health lỗi có chỉ báo riêng (rule + panel) nên service không sẵn sàng vẫn nhìn thấy. | PASS |
| VIII. Performance and Resilience Budgets | SLO và tỷ lệ không đổi; chỉ đổi tập span được đo (loại health check không phải trải nghiệm người dùng). Hiến chương không đổi (người dùng chốt). | PASS |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible | Quay lui = import lại ndjson cũ và `git revert` manifest/test; không cần redeploy service. | PASS |

**Gate**: qua, không có vi phạm. **Re-check sau Phase 1**: data-model và contracts không thêm service, secret, index hay hợp đồng HTTP/event; manifest thêm một khoá nhưng không đổi SLO. Kết quả giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/033-exclude-health-spans/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── budget-exclusion-contract.md
│   └── health-failure-rule-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
services/*/src/*.Api/service-manifest.yaml          # sửa (7 file): thêm excluded-path-prefixes: [/health]

specs/029-error-budget-weekly/contracts/error-budget-policy-manifest-shape.md   # sửa: khoá mới

tests/ServiceManifestSloConventionTests/
├── ServiceManifestModel.cs                         # sửa: ExcludedPathPrefixes
├── ErrorBudgetPolicyTests.cs                       # sửa/thêm: 7 manifest giống hệt nhau
├── ErrorBudgetRuleDefinitionTests.cs               # thêm: điều kiện loại ở 4 rule 027
├── IncidentFastDetectionRuleDefinitionTests.cs     # thêm: điều kiện loại ở rule 028
└── <HealthFailureRuleDefinitionTests>.cs           # mới (tên chốt ở tasks): rule health lỗi

docs/kibana-quan-sat-he-thong/alerts/
├── error-budget-rules.ndjson                       # export lại (4 rule)
├── incident-fast-detection-rule.ndjson             # export lại
└── <health-failure-rule>.ndjson                    # mới (tên chốt ở tasks)

docs/kibana-quan-sat-he-thong/dashboards/
├── ngan-sach-loi-tuan.ndjson                       # export lại (truy vấn ES|QL có điều kiện loại)
├── xu-ly-su-co.ndjson                              # export lại (6 Lens + panel health lỗi mới)
└── README.md                                       # sửa nếu nhắc công thức

docs/kibana-quan-sat-he-thong/06, 07, 08, 12-*.md, alerts/README.md   # sửa tại chỗ
postman/ecommerce.postman_collection.v2.json        # sửa 26 request (25, 27, 29a, 30a) + thêm folder 33

docs/PO|QA|architecture/033_*                       # mới (tên chốt ở tasks)
docs/PO/functional-debt.md, docs/QA/QA_Debt.md, docs/architecture/technical-debt.md   # thêm mục 033
docs/diagrams/033-*.drawio                          # 3 sơ đồ mới (tên chốt ở tasks)
```

**Structure Decision**: Không tạo dự án hay thư mục mới ngoài `specs/033-*` và các file theo khuôn có sẵn.

### Phạm vi sửa tài liệu (FR-012)

Danh sách dựng bằng lệnh tìm đã chạy lúc plan (`/health/(live|ready)` cùng `Chaos|tiêm`, và các mô tả công thức "mọi span"): `docs/QA/025|027|029|030_*`, `docs/architecture/025|027|028|029|030_*`, `docs/development/025_*`, `docs/PO/027–030_*`, `docs/kibana-quan-sat-he-thong/06|07|08|12-*`, `alerts/README.md`, `dashboards/README.md`, `specs/025–030` (quickstart/research/tasks/contract liên quan), sơ đồ `025`/`027`/`028`/`029`/`030` nhắc `/health/live`, mô tả Postman. Danh sách chính xác (LỆNH-TÌM) dựng ở `/speckit-tasks`, chạy lại ở task cuối (SC-006). **Không sửa**: mục cũ `docs/QA/QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.

## Điểm dừng bắt buộc khi triển khai

- V1–V5 trong [research.md](./research.md): sai thì **dừng và hỏi người dùng**.
- Trước khi xoá/ghi đè object Kibana đang chạy ngoài việc import lại file export, và trước khi dọn Elastic: **hỏi người dùng**.
- Các tên chưa chốt (cuối research.md): hỏi ở `/speckit-tasks`, không tự đặt.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh. Bảng này để trống có chủ đích.
