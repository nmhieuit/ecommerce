# Implementation Plan: Diễn tập sự cố thật và phản ứng on-call (tiêm lỗi mù, phát hiện, xử lý, xác nhận khôi phục bằng telemetry)

**Branch**: `claude/scrum-36-backlog-export-ac1561` | **Date**: 2026-10-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/028-incident-oncall-drill/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Biến quy trình phản ứng sự cố từ "mô tả" thành "diễn tập dưới điều kiện thật", **không sửa code của
service nào**.

**Script `scripts/incident-drill.ps1`**:
- Chỉ chạy khi `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true`.
- Bốc ngẫu nhiên một service trong 7, một loại hỏng hóc áp dụng được, tham số của nó, và một thời
  điểm trong 0–30 phút. Các loại hỏng hóc:
  - đích kết nối sai;
  - cạn pool;
  - 5xx của 027 qua header.
- Niêm phong lựa chọn vào file bị gitignore, chỉ in mã băm SHA-256.
- Tới giờ, tạo lại cả 7 container; chỉ container đích nhận cấu hình sai qua một file compose override
  tạm.

**Tải nền**: newman chạy folder Postman 00 + 26, cộng folder mới 28 (parties, identity), để cả 7
service có traffic.

**Phát hiện**: một rule Kibana ES|QL mới `incident-fast-detection` (5 phút) bắn cho service nào vượt
SLO trong 5 phút gần nhất. Trạng thái của rule hiện ở bảng mới trên dashboard Xử lý sự cố.

**Phản ứng**: người vận hành triage theo quy trình viết trong `docs/dien-tap-chaos-engineering/`, gồm:
- Kibana Case tạo tay: severity SEV1–3, comment mỗi mốc và mỗi 30 phút, baseline "alert → merge";
- giảm thiểu bằng merge PR phòng ngừa rồi build lại từ master;
- xác nhận giải quyết khi đạt SLO liên tục 15 phút;
- reveal lựa chọn niêm phong;
- ghi bản ghi sự cố theo mẫu mới.

Chi tiết quyết định: [research.md](./research.md).

## Technical Context

**Language/Version**: PowerShell 5.1+ (script diễn tập); ES|QL (rule, panel Kibana); YAML (file
compose override sinh tạm); JSON (Postman collection v2.1).

**Primary Dependencies**: Docker Compose; Kibana/Elasticsearch 9.4.4 (Alerting, Cases — license Basic,
xác minh V1); newman qua `npx` (phụ thuộc mới, hỏi người dùng trước khi tải).

**Storage**: Không có storage mới. File cục bộ `.incident-drill/<runId>/` (không commit);
alerts-as-data `.alerts-stack.alerts-default`; Kibana Cases.

**Testing**: Không viết test tự động (người dùng chốt — xem Complexity Tracking). Kiểm chứng bằng
[quickstart.md](./quickstart.md) và báo cáo QA `docs/QA/028_QA_*.md`.

**Target Platform**: Docker Compose local (`docker-compose.local.yml`), máy Windows của người vận hành.

**Project Type**: Bổ sung công cụ vận hành, cấu hình quan sát và tài liệu vào monorepo hiện có; không có
service mới, không đổi code service.

**Performance Goals**: Rule 5 phút chỉ quét 5 phút dữ liệu. Cảnh báo bắn trong ≤ 5 phút + một chu kỳ
rule kể từ khi service vượt SLO (SC-002).

**Constraints**:
- Không sửa file nào dưới `services/`, `shared/`.
- Không sửa compose/`.env.example` để tiêm lỗi; cấu hình sai chỉ nằm trong file override tạm.
- `CHAOS_ALLOW_FAULT_INJECTION` mặc định `false`.
- 4 rule 027 và `error-budget-rules.ndjson` giữ nguyên.

**Scale/Scope**: 1 script, 1 rule, 1 panel, 1 folder Postman, 1 mẫu bản ghi, 1 mục quy trình triage,
1 tài liệu Kibana; 7 service là đích tiêm.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Không thêm phụ thuộc giữa service; không sửa service. | PASS |
| II. Contract-First | 3 hợp đồng (`contracts/`) viết trước khi làm script, rule, mẫu bản ghi. | PASS |
| III. Test-First | Người dùng chốt không viết test. Sai lệch được ghi ở Complexity Tracking, có thời hạn. | **SAI LỆCH CÓ GHI NHẬN** |
| IV. Event-Driven | Không thêm giao tiếp giữa service. | N/A |
| V. Tenant Isolation | Không chạm đường dữ liệu tenant. Request header 5xx bị middleware 027 chặn trước logic nghiệp vụ. | N/A |
| VI. Secure by Default | Không thêm secret. File override và file niêm phong không commit (`.gitignore`). Script từ chối chạy khi cờ tắt. Connection string sai được sinh lại từ biến `.env` lúc chạy, không ghi mật khẩu vào file commit. | PASS |
| VII. Observable by Default | Phát hiện và xác nhận khôi phục đều dựa trên telemetry OTel có sẵn. | PASS |
| VIII. Performance and Resilience Budgets | Diễn tập tiêu hao ngân sách lỗi như sự cố thật (FR-014) và áp dụng chính sách 027. | PASS |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible | Diễn tập gated bởi `CHAOS_ALLOW_FAULT_INJECTION`, là công cụ SRE lâu dài (người dùng chốt, giống 025/027), người sở hữu `owner-devops`. Gỡ lỗi chỉ cần chạy lại compose không kèm override; rule tắt/bật trên UI. | PASS (lý giải như 025/027) |

**Gate**: chỉ có sai lệch Nguyên tắc III. Sai lệch này có đủ lý do, phương án bị loại và thời hạn ở
Complexity Tracking, nên gate qua.

**Re-check sau Phase 1**: data-model và contracts không thêm service, không thêm secret vào file commit,
không đổi hợp đồng HTTP/event, không sửa code service. Kết quả giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/028-incident-oncall-drill/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── incident-drill-script-contract.md
│   ├── fast-detection-rule-contract.md
│   └── incident-record-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
scripts/incident-drill.ps1                          # mới — -Start / -Reveal, tiến trình nền tiêm lỗi
.gitignore                                          # sửa: thêm .incident-drill/

postman/ecommerce.postman_collection.v2.json        # sửa: thêm folder "28 - Diễn tập sự cố: tải nền bổ sung (parties, identity)"

docs/kibana-quan-sat-he-thong/
├── 08-phat-hien-nhanh-va-xu-ly-su-co.md            # mới — dựng/vận hành rule, panel, Kibana Case
├── 00-tong-quan-lo-trinh.md                        # sửa: thêm mục 08
├── alerts/incident-fast-detection-rule.ndjson      # mới — export rule
├── alerts/README.md                                # sửa: cách import rule mới
└── dashboards/xu-ly-su-co.ndjson        # sửa — thêm bảng phát hiện nhanh

docs/dien-tap-chaos-engineering/
├── README.md                                       # sửa: mục "Diễn tập sự cố on-call (SCRUM-36)" — quy trình triage, severity
└── mau-ban-ghi-su-co.md                            # mới — mẫu bản ghi sự cố
```

Tài liệu đi kèm như spec 027 đã làm, sẽ được tách thành task ở `/speckit-tasks`:
- `docs/PO/028_PO_*.md`, `docs/QA/028_QA_*.md`;
- cập nhật `docs/QA/QA_Debt.md`, `docs/architecture/technical-debt.md`, `docs/PO/functional-debt.md`;
- sơ đồ drawio `028-*` (component, flow nghiệp vụ, sequence).

**Structure Decision**: Không tạo dự án mới, không sửa code service. Script đặt cạnh các script vận
hành khác trong `scripts/`; rule và tài liệu Kibana nối tiếp file `07` của 027; quy trình và bản ghi
sự cố dùng chung thư mục diễn tập chaos của 025 (người dùng chốt).

## Điểm dừng bắt buộc khi triển khai

- [research.md](./research.md) mục "Điểm phải xác minh" (V1–V4, V6) phải được kiểm chứng trên stack thật ở
  task đầu tiên. V1 hoặc V2 sai thì **dừng và hỏi lại người dùng**, không tự đổi phương án.
- Các đề xuất trong research.md đã được người dùng duyệt ở phiên `/speckit-tasks` (2026-10-01); loại D bị loại.
- Người dùng đã đồng ý trước việc tải `newman` qua `npx`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Nguyên tắc III (Test-First): không có test viết trước cho script `incident-drill.ps1`, rule `incident-fast-detection` (khớp ngưỡng manifest) và mẫu bản ghi sự cố. | Người dùng chốt: "đây là diễn tập, không có code mới nên không cần test" — không file nào dưới `services/`/`shared/` bị sửa; phần mới là công cụ vận hành, cấu hình Kibana và tài liệu, được kiểm chứng bằng chính buổi diễn tập thật theo `quickstart.md` và báo cáo QA. **Thời hạn**: tới khi SCRUM-37 (postmortem và ticket follow-up) hoàn tất; ghi vào `docs/architecture/technical-debt.md`. | Viết test (Pester cho script; test kiểu `ErrorBudgetRuleDefinitionTests` cho rule) — người dùng không chọn. Rủi ro đã biết: ngưỡng trong rule có thể trôi dạt khỏi manifest mà không bị build bắt. |
