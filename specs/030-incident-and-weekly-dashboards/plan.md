# Implementation Plan: Tách dashboard SLO thành "Xử lý sự cố" và "Ngân sách lỗi tuần"

**Branch**: `feature/030-incident-and-weekly-dashboards` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/030-incident-and-weekly-dashboards/spec.md`

## Summary

Tách dashboard `SLO vận hành hằng ngày — 7 service` (12 panel, trộn 3 loại cửa sổ thời gian) thành hai dashboard mới và bỏ dashboard cũ:

- **`Xử lý sự cố — 7 service`**: mọi panel theo thanh thời gian, mặc định 1 giờ, tự làm mới 1 phút. Chép bảng SLO + ngưỡng + 3 panel đào sâu; bỏ lọc 15 phút của Phát hiện nhanh; thêm 5xx/p95 theo phút, traffic + 401/403 theo phút, lỗi gọi hạ lưu, log lỗi gần nhất.
- **`Ngân sách lỗi tuần — 7 service`**: cố định tuần lịch giờ Việt Nam, có điều khiển chọn tuần (biến `?week_start`); mức tiêu hao, hạn mức còn lại, cảnh báo/cạn (trạng thái hiện tại), error-rate/p95 theo ngày, tiêu hao lũy kế.
- **Hai dashboard link qua lại**, mỗi dashboard một file ndjson, id mới.
- **Test mới** canh gác rule `incident-fast-detection` (đóng sai lệch Nguyên tắc III của 028); test 027 chạy lại xanh.
- **Kiểm chứng có điều kiện**: log Error trong request thật có mang correlation id/trace id không; chỉ sửa `ServiceDefaults` nếu thiếu.
- **Tài liệu**: bỏ mọi tham chiếu tới dashboard cũ; bộ tài liệu 030 theo khuôn 027/028/029.

Quyết định và dữ kiện đo thật: [research.md](./research.md).

## Technical Context

**Language/Version**: ES|QL (Discover session, Lens ES|QL, điều khiển); Lens/Discover cổ điển (KQL); C#/.NET 10 (chỉ test, và `ServiceDefaults` nếu V4 báo thiếu); Markdown; drawio XML; Postman collection v2.1 JSON.

**Primary Dependencies**: Kibana/Elasticsearch 9.4.4 license Basic (đang chạy ở `localhost:5601/9200`); Saved Objects API (import/export); xUnit + System.Text.Json + YamlDotNet (dự án `tests/ServiceManifestSloConventionTests`); newman qua `npx.cmd` (như 028/029).

**Storage**: Không thêm index hay service. Dùng lại alerts-as-data và các data stream OTel. Hiện vật: 2 file ndjson dashboard, 1 index-pattern logs mới (nằm trong export của dashboard Xử lý sự cố).

**Testing**: xUnit trong `tests/ServiceManifestSloConventionTests` (test mới cho rule 028, viết trước, chạy thấy đỏ). Nếu V4 báo thiếu: test trong `shared/ServiceDefaults.UnitTests`. Hành vi trên Kibana thật kiểm theo [quickstart.md](./quickstart.md), không chặn PR.

**Target Platform**: Docker Compose local (nơi duy nhất có Elastic stack). CI Jenkins chạy xUnit.

**Project Type**: Cấu hình quan sát + test + tài liệu trong monorepo microservices. Không có endpoint hay service mới.

**Performance Goals**: Dashboard Xử lý sự cố làm mới mỗi 1 phút với 7 service; truy vấn ES|QL của dashboard tuần quét ≤ 7 ngày (tuần chọn); lũy kế theo ngày nhân dòng tối đa 7 lần trước khi gom.

**Constraints**:
- Không đổi SLO, ngưỡng, tỷ lệ cho phép, chu kỳ hay hành vi rule (FR-017), trừ sửa ghi log có điều kiện (FR-019).
- Không commit: người dùng tự commit.
- Không sửa lịch sử: mục cũ `QA_Debt`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.
- Bộ chọn tuần, Lens ES|QL, link, section, link trace: sai là **dừng và hỏi**, không tự lùi.
- Dọn Elastic chỉ khi người dùng xác nhận lại (FR-018).
- Stack local đang chạy có dữ liệu thật và dashboard cũ: **không xoá dashboard cũ trên Kibana** cho tới khi người dùng xác nhận (chỉ xoá file trong repo).

**Scale/Scope**: 2 dashboard (khoảng 10 + 9 panel), 1 data view mới, 1 file test mới (+ sửa `ServiceDefaults` nếu V4 thiếu), khoảng 45 file tài liệu sửa tại chỗ, bộ tài liệu 030 gồm 3 file, 3 file debt, 3 drawio, 1 folder Postman.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* Hiến chương hiện hành **2.0.0** (spec 029).

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Không thêm phụ thuộc giữa service. | N/A |
| II. Contract-First | 2 contract (`contracts/`) viết trước khi tạo dashboard và test. | PASS |
| III. Test-First | Test `IncidentFastDetectionRuleDefinitionTests` viết trước, chạy thấy ĐỎ. Nếu V4 thiếu thì test `ServiceDefaults` cũng đỏ trước. Dashboard (cấu hình) không có test đơn vị; kiểm theo quickstart. Spec này đóng sai lệch III của 028. | PASS (tách ở tasks.md) |
| IV. Event-Driven | Không đụng. | N/A |
| V. Tenant Isolation | Không đụng. Log lỗi hiển thị message/trace id: dữ liệu quan sát nội bộ, không thêm dữ liệu tenant. | N/A |
| VI. Secure by Default | Không thêm secret. Dùng lại `KIBANA_ENCRYPTION_KEY`. Panel log lỗi hiển thị `message` có thể chứa dữ liệu nhạy cảm; ghi nhận ở QA_Debt, không lọc thêm trong spec này. | PASS (có ghi nhận) |
| VII. Observable by Default | Hoàn thiện khả năng debug từ telemetry: log lỗi kèm trace/correlation id; V4 kiểm chứng log mang định danh tương quan. | PASS |
| VIII. Performance and Resilience Budgets | Không đổi SLO; chỉ đổi nơi hiển thị. | PASS |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible | Quay lui = import lại ndjson cũ từ git. Nếu V4 sửa `ServiceDefaults`: thay đổi nhỏ, hoàn tác bằng git. | PASS |

**Gate**: qua, không có vi phạm. **Re-check sau Phase 1**: data-model và contracts không thêm service, secret, index hay hợp đồng HTTP/event. Kết quả giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/030-incident-and-weekly-dashboards/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── dashboards-contract.md
│   └── incident-fast-detection-rule-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
docs/kibana-quan-sat-he-thong/dashboards/
├── <xử lý sự cố>.ndjson                    # mới (tên file chốt ở /speckit-tasks)
├── <ngân sách tuần>.ndjson                 # mới (tên file chốt ở /speckit-tasks)
├── slo-van-hanh-hang-ngay.ndjson           # XOÁ khỏi repo
└── README.md                               # sửa: import/export hai file, id mới

docs/kibana-quan-sat-he-thong/
├── 00, 06, 07, 08-*.md                     # sửa tại chỗ; 06 đổi/tách cho hai dashboard (tên chốt ở /speckit-tasks)
└── alerts/README.md                        # sửa nếu nhắc dashboard cũ

tests/ServiceManifestSloConventionTests/
└── IncidentFastDetectionRuleDefinitionTests.cs   # mới (test canh gác rule 028)

shared/ServiceDefaults/                     # CHỈ sửa nếu V4 báo thiếu correlation id trong log
shared/ServiceDefaults.UnitTests/           # CHỈ thêm test nếu V4 báo thiếu

postman/ecommerce.postman_collection.v2.json    # thêm folder 30; sửa mô tả nhắc dashboard cũ

docs/PO|QA|architecture/030_*_hai dashboard xử lý sự cố và ngân sách tuần.md   # mới
docs/PO/functional-debt.md, docs/QA/QA_Debt.md, docs/architecture/technical-debt.md   # thêm mục 030
docs/diagrams/030-*.drawio                  # 3 sơ đồ mới (tên chốt ở /speckit-tasks)
```

**Structure Decision**: Không tạo dự án hay thư mục mới ngoài `specs/030-*` và các file theo khuôn có sẵn.

### Phạm vi sửa tài liệu trỏ tới dashboard cũ (FR-014)

Lấy từ lệnh tìm `e2e06ff5|slo-van-hanh-hang-ngay|SLO vận hành hằng ngày` trên nhánh hiện tại:
- **021**: `specs/021-declare-service-slos/{plan,research,data-model,quickstart,tasks}.md`, `contracts/continuous-measurement-contract.md`, `docs/QA|architecture|development/021_*`, 2 drawio 021.
- **025/026** (phát hiện khi lập plan, ngoài danh sách 021/027/028 trong mô tả ban đầu; "mọi tài liệu đang trỏ tới" bao gồm chúng): `specs/025-*/{quickstart,research,tasks}.md`, `specs/026-*/quickstart.md`, `docs/architecture/025_*`, drawio 025.
- **027/028/029**: `specs/027|028|029-*/…`, `docs/QA|architecture/027_*`, `docs/QA/028_*`, `docs/QA/029_*`, drawio 027/028/029, contract `fast-detection-rule-contract.md` và `error-budget-alert-rules-contract.md`.
- **Chung**: `docs/kibana-quan-sat-he-thong/{00,06,07,08}-*.md`, `dashboards/README.md`, `docs/superpowers/{plans,specs}/2026-09-08-dashboard-slo-*.md`, `docs/dien-tap-chaos-engineering/README.md`, mô tả trong collection Postman.
- **Không sửa**: `docs/dien-tap-chaos-engineering/ket-qua/` (kết quả diễn tập), mục cũ `docs/QA/QA_Debt.md`, `specs/002-gateway-bff-routing/`.

Lệnh tìm phải được chạy lại ở task cuối (SC-005); kết quả còn lại phải thuộc danh sách "không sửa".

## Điểm dừng bắt buộc khi triển khai

- V1–V8 trong [research.md](./research.md): sai thì **dừng và hỏi người dùng**, không tự lùi.
- Trước khi xoá dashboard cũ trên Kibana đang chạy và trước khi dọn Elastic: **hỏi người dùng**.
- Các tên chưa chốt sẽ hỏi ở `/speckit-tasks`, không tự đặt (xem cuối research.md).

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh. Bảng này để trống có chủ đích.
