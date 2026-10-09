# Implementation Plan: Ngân sách lỗi chỉ đếm span Server

**Branch**: `feature/034-error-budget-server-spans` | **Date**: 2026-10-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/034-error-budget-server-spans/spec.md`

## Summary

Công thức ngân sách hiện đếm mọi span (chỉ loại `/health*`), kể cả `Client` và `Producer`. Trong tuần 05–11/10, `Bff.Api` có 319 span được tính mà 201 là span Client, và 12/21 lỗi 5xx là span Client; một lỗi đi qua Gateway → BFF → Products bị trừ ở cả 3 service và đếm hai lần trong BFF. Thay đổi:

- **Chỉ đếm `kind == "Server"`** trong 4 rule 027 và rule 028 `incident-fast-detection`; riêng `error-budget-frozen` dùng `kind == "Server" OR _index LIKE "*slo-error-budget-events*"` để không làm mất sự kiện cạn (F4).
- **Cùng điều kiện** cho 5 truy vấn ES|QL của dashboard Ngân sách tuần và KQL `kind : Server and not attributes.url.path : /health*` cho 6 panel Lens của dashboard Xử lý sự cố; panel Lỗi gọi hạ lưu (Client) giữ nguyên.
- **Test**: thêm kiểm tra Server vào `ErrorBudgetRuleDefinitionTests` (4 rule) và `IncidentFastDetectionRuleDefinitionTests`; đỏ trước, xanh sau.
- **Dọn trạng thái** cạn/đóng băng giả sau triển khai (hỏi lại trước khi xoá), **Postman folder 34**, **tài liệu và bộ tài liệu 034** theo khuôn 027–033. Lệch Governance của PR #75 chỉ ghi nhận vào `technical-debt.md`.

Quyết định và dữ kiện đo thật: [research.md](./research.md).

## Technical Context

**Language/Version**: ES|QL (rule, Discover session), KQL (Lens), C#/.NET 10 (chỉ test), Markdown, drawio XML, Postman collection v2.1 JSON.

**Primary Dependencies**: Kibana/Elasticsearch 9.4.4 license Basic (đang chạy `localhost:5601/9200`); Dashboards REST API và Saved Objects API (import/export) như 030/033; xUnit + System.Text.Json + YamlDotNet (`tests/ServiceManifestSloConventionTests`); newman qua `npx.cmd`.

**Storage**: Không thêm index hay service. Dùng lại alerts-as-data và `slo-error-budget-events`.

**Testing**: xUnit trong `tests/ServiceManifestSloConventionTests`: sửa `ErrorBudgetRuleDefinitionTests`, `IncidentFastDetectionRuleDefinitionTests`. Test viết trước, đỏ thật rồi mới sửa rule (Nguyên tắc III). Hành vi trên Kibana thật kiểm theo [quickstart.md](./quickstart.md), không chặn PR.

**Target Platform**: Docker Compose local (nơi duy nhất có Elastic stack). CI Jenkins chạy xUnit.

**Project Type**: Cấu hình quan sát + test + tài liệu trong monorepo microservices. Không có endpoint, service hay code production mới.

**Performance Goals**: Thêm một phép so sánh `kind` mỗi span (rẻ hơn điều kiện `LIKE`); rule vẫn quét ≤ 7/14 ngày, chu kỳ 5 phút như 027/029.

**Constraints**:
- Không đổi SLO, tỷ lệ cho phép, ngưỡng, mốc, cửa sổ, chu kỳ ngân sách, loại `/health*`, hiến chương, hành vi endpoint (FR-009, FR-010). Không thêm sàn số request tối thiểu.
- Không commit: người dùng tự xem và commit.
- Không sửa lịch sử: mục cũ `QA_Debt`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.
- V1–V5 sai là **dừng và hỏi**, không tự lùi.
- Không xoá sự kiện, không Disable/Enable rule, không dọn Elastic khi chưa hỏi lại ngay trước lúc làm (FR-008, FR-013).
- Câu hỏi mở ở cuối research.md: hỏi ở `/speckit-tasks`, không tự đặt.

**Scale/Scope**: 5 rule sửa (2 file export), 2 dashboard (5 truy vấn ES|QL + 6 panel Lens), 2 lớp test, 1 folder Postman mới, khoảng 30–50 file tài liệu, bộ tài liệu 034 (3 file, 3 file debt, 3 drawio).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* Hiến chương hiện hành **2.0.0**.

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Không đổi ranh giới hay phụ thuộc giữa service. Ngân sách mỗi service chỉ còn tính request nó nhận, đúng trách nhiệm từng service. | PASS |
| II. Contract-First | Contract `server-span-only-contract.md` viết trước; rule, dashboard và test sửa sau. | PASS |
| III. Test-First | Test Server mới cho 5 rule viết trước và chạy ĐỎ thật vì rule chưa có điều kiện `kind`; sau đó mới sửa export. Dashboard là cấu hình, kiểm theo quickstart. | PASS (tách ở tasks.md) |
| IV. Event-Driven | Không đụng; span `Producer` chỉ bị loại khỏi ngân sách, không đổi cách publish. | N/A |
| V. Tenant Isolation | Không đụng đường dữ liệu tenant. | N/A |
| VI. Secure by Default | Không thêm secret hay dữ liệu hiển thị. | PASS |
| VII. Observable by Default | Làm chính xác tín hiệu: ngân sách phản ánh đúng request service nhận; lỗi hạ lưu vẫn thấy ở panel Lỗi gọi hạ lưu (Client). | PASS |
| VIII. Performance and Resilience Budgets | SLO, tỷ lệ, ngưỡng không đổi; chỉ đổi tập span được đo. Hiến chương không đổi; lệch phiên bản PR #75 ghi nhận như nợ riêng (người dùng chốt). | PASS |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible | Quay lui = import lại ndjson cũ và `git revert` test; không cần redeploy service. | PASS |

**Gate**: qua, không có vi phạm. **Re-check sau Phase 1**: data-model và contract không thêm service, secret, index, khoá manifest hay hợp đồng HTTP/event. Kết quả giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/034-error-budget-server-spans/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── server-span-only-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
tests/ServiceManifestSloConventionTests/
├── ErrorBudgetRuleDefinitionTests.cs               # thêm: Theory 3 rule mốc + Fact rule frozen
└── IncidentFastDetectionRuleDefinitionTests.cs     # thêm: Fact Server cho rule 028

docs/kibana-quan-sat-he-thong/alerts/
├── error-budget-rules.ndjson                       # export lại (4 rule)
└── incident-fast-detection-rule.ndjson             # export lại

docs/kibana-quan-sat-he-thong/dashboards/
├── ngan-sach-loi-tuan.ndjson                       # export lại (5 truy vấn ES|QL)
├── xu-ly-su-co.ndjson                              # export lại (6 panel Lens)
└── README.md                                       # sửa nếu nhắc công thức

docs/kibana-quan-sat-he-thong/06, 07, 08, alerts/README.md   # sửa tại chỗ
specs/033-exclude-health-spans/contracts/budget-exclusion-contract.md   # sửa: ghi chú nối tiếp 034
postman/ecommerce.postman_collection.v2.json        # thêm folder 34 + sửa mô tả

docs/PO|QA|architecture/034_*                       # mới (tên chốt ở tasks)
docs/PO/functional-debt.md, docs/QA/QA_Debt.md, docs/architecture/technical-debt.md   # thêm mục 034
docs/diagrams/034-*.drawio                          # 3 sơ đồ mới (tên chốt ở tasks)
```

**Structure Decision**: Không tạo dự án hay thư mục mới ngoài `specs/034-*` và các file theo khuôn có sẵn. Manifest của 7 service không đổi (D4; có thể đổi nếu người dùng chọn khai báo ở manifest khi hỏi ở tasks).

### Phạm vi sửa tài liệu (FR-011)

Danh sách dựng ở `/speckit-tasks` bằng LỆNH-TÌM các mô tả công thức ngân sách ("mọi span", "mọi span Server", điều kiện loại `/health*`) và chạy lại ở task cuối (SC-006). Dự kiến: `docs/kibana-quan-sat-he-thong/06|07|08`, `alerts/README.md`, `dashboards/README.md`; `docs/PO|QA|architecture/027–030, 033_*`; contract rule 027/028/033; sơ đồ 027–030, 033; mô tả Postman. **Không sửa**: mục cũ `docs/QA/QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.

## Điểm dừng bắt buộc khi triển khai

- V1–V5 trong [research.md](./research.md): sai thì **dừng và hỏi người dùng**.
- Trước khi xoá sự kiện, Disable/Enable rule, ghi đè object Kibana đang chạy ngoài việc import lại file export, và trước khi dọn Elastic: **hỏi người dùng**.
- Các câu hỏi mở (cuối research.md): hỏi ở `/speckit-tasks`, không tự đặt.

## Điều chỉnh so với spec (ghi lại, không đổi ý định)

- Spec ghi `incident-fast-detection` "hiện chưa có test". Thực tế đã có `IncidentFastDetectionRuleDefinitionTests` (8 test); cái chưa có là kiểm tra điều kiện Server. Việc làm: thêm test vào lớp đó (research D5).
- Spec nói "mỗi rule chỉ đếm Server"; rule frozen cần ngoại lệ sự kiện (research F4/D1) để giữ nguyên hành vi "ngày không traffic = đạt" và việc đọc sự kiện cạn.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh. Bảng này để trống có chủ đích.
