# Implementation Plan: Danh mục 8 nhóm lỗi để luyện troubleshoot (tiêm bằng cấu hình trên Docker Compose, lỗi có chủ đích và lỗi bất ngờ, gợi ý theo mức)

**Branch**: `feature/031-error-group-catalog` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/031-error-group-catalog/spec.md`

## Summary

Mở rộng công cụ diễn tập sự cố của 028 từ 3 loại lỗi lên 8 nhóm (9 loại A–I), **không sửa code của
service nào**, và gỡ phần Kubernetes của 025.

**Danh mục lỗi** `scripts/incident-drill/catalog.json`: dữ liệu thuần, mô tả độc lập với Docker
Compose — nhóm, loại, đích áp dụng được, tham số trừu tượng, triệu chứng, ba mức gợi ý. Không chứa
lệnh docker hay biến compose.

**Bộ chuyển đổi Compose** (hàm trong `scripts/incident-drill.ps1`): nơi duy nhất biết cách tiêm và
khôi phục từng loại trên `docker-compose.local.yml`. Sau này thêm bộ chuyển đổi CD/Kubernetes là thêm
một tập hàm cùng giao diện, không đụng danh mục.

**Lệnh mới của script** (giữ `-Start`, `-Reveal`, `-Load`, chế độ không mù cũ):
- `-Inject -Type <A–I> [-Group <1–8>] [-Target <đích>] [-DurationSeconds <n>]` — dạng (a), có chủ
  đích, tuỳ chọn tự gỡ.
- `-Restore -RunId <id>` — khôi phục chung cho mọi loại.
- `-Hint -RunId <id> -Level 1|2|3` — gợi ý theo mức, ghi `hint-log.json`.
- `-Start` bốc mù từ cả 8 nhóm.

**Gỡ Kubernetes của 025**: sửa/xoá toàn bộ kịch bản kill-pod và hướng dẫn k8s trong specs/025 và tài
liệu liên quan; nhóm 8 (container chết) thay thế. `deploy/ansible/**`, Jenkinsfile, script lint, test
quy ước manifest, 018/019, ADR-0007 giữ nguyên.

**Tài liệu và Postman** theo nếp 027/028: folder Postman `31 - Danh mục nhóm lỗi` (6 subfolder D–I),
PO/QA/Architect, 3 sơ đồ drawio, cập nhật `technical-debt.md`, `QA_Debt.md`, `functional-debt.md`.

Chi tiết quyết định: [research.md](./research.md).

## Technical Context

**Language/Version**: PowerShell 5.1+ (script, bộ chuyển đổi Compose); JSON (danh mục, Postman
collection v2.1, trạng thái từng lần chạy); YAML (file compose override sinh tạm, như 028).

**Primary Dependencies**: Docker Engine + Compose (`docker stop/start/kill/update/network`); newman
qua `npx` (đã có từ 028, `-Load`); Elasticsearch `_query` (đo tốc độ nền cho loại C/D).

**Storage**: Không có storage mới. File cục bộ `.incident-drill/<runId>/` (đã trong `.gitignore`):
`sealed.json`, `hash.txt`, `state.json`, `hint-log.json`, `injected-at.txt`, `injector.log`,
`docker-compose.incident.yml`. Danh mục `scripts/incident-drill/catalog.json` được commit.

**Testing**: Không viết test tự động (người dùng chốt — xem Complexity Tracking). Kiểm chứng bằng
[quickstart.md](./quickstart.md), folder Postman 31 và báo cáo QA `docs/QA/031_QA_*.md`.

**Target Platform**: Docker Compose local (`docker-compose.local.yml`), máy Windows của người vận hành.

**Project Type**: Bổ sung công cụ vận hành, dữ liệu danh mục và tài liệu vào monorepo hiện có; không
có service mới, không đổi code service.

**Performance Goals**: `-Restore` chờ container đích healthy tối đa 10 phút (người dùng chốt). Không
có mục tiêu hiệu năng khác; lệnh không nằm trên đường dữ liệu của người dùng cuối.

**Constraints**:
- Không sửa file nào dưới `services/`, `shared/`.
- Không sửa `docker-compose.local.yml`/`docker-compose.yml`/`.env.example` để tiêm lỗi; cấu hình sai
  chỉ nằm trong file override tạm hoặc trong lệnh docker bên ngoài.
- `CHAOS_ALLOW_FAULT_INJECTION` (mọi nhóm) và `CHAOS_ALLOW_LATENCY_INJECTION` (nhóm 3) mặc định
  `false`; không thêm cờ mới.
- Không sửa `deploy/ansible/**`, `Jenkinsfile`, `scripts/ci/lint-deployment-manifests.sh`,
  `tests/DeploymentManifestConventionTests`, hiến chương.
- Hợp đồng và hành vi `-Start`, `-Reveal`, `-Load`, chế độ không mù của 028 giữ nguyên, trừ phần sửa
  có chủ đích ở [contracts/incident-drill-script-contract.md](./contracts/incident-drill-script-contract.md).

**Scale/Scope**: 1 danh mục JSON (8 nhóm, 9 loại), 4 lệnh mới/mở rộng của 1 script, 1 folder Postman
(6 subfolder), ~37 file tài liệu k8s của 025 rà soát gỡ, 3 tài liệu PO/QA/Architect, 3 sơ đồ.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Không thêm phụ thuộc giữa service; không sửa service; tách DB dừng chỉ qua `docker stop`, không đọc DB. | PASS |
| II. Contract-First | 2 hợp đồng (`contracts/`) viết trước script và danh mục. | PASS |
| III. Test-First | Người dùng chốt không viết test. Sai lệch ghi ở Complexity Tracking, có thời hạn. | **SAI LỆCH CÓ GHI NHẬN** |
| IV. Event-Driven | Không thêm giao tiếp giữa service. | N/A |
| V. Tenant Isolation | Không chạm đường dữ liệu tenant. | N/A |
| VI. Secure by Default | Không thêm secret. File override/sealed không commit (`.gitignore`); connection string sai nội suy `${MSSQL_SA_PASSWORD}` lúc chạy, không ghi mật khẩu ra file. Script từ chối chạy khi cờ tắt. | PASS |
| VII. Observable by Default | Triệu chứng và xác nhận khôi phục dựa trên telemetry OTel và trạng thái container (health). | PASS |
| VIII. Performance and Resilience Budgets | Lỗi diễn tập tiêu hao ngân sách lỗi tuần như sự cố thật (FR-021, spec 029). | PASS |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible | Gated bởi cờ `.env` (mặc định `false`); công cụ SRE lâu dài (như 025/027/028), người sở hữu `owner-devops`. Gỡ lỗi bằng `-Restore`, không đổi code hay redeploy. | PASS (lý giải như 025/027/028) |

Hiến chương nêu nền tảng "Kubernetes, provisioned through Ansible" — spec này KHÔNG đổi nền tảng đó;
chỉ ngừng dùng cluster cho diễn tập chaos. Vì vậy không cần sửa hiến chương.

**Gate**: chỉ có sai lệch Nguyên tắc III, đủ lý do, phương án bị loại và thời hạn ở Complexity
Tracking → gate qua.

**Re-check sau Phase 1**: data-model và contracts không thêm service/secret vào file commit, không đổi
hợp đồng HTTP/event, không sửa code service. Kết quả giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/031-error-group-catalog/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── fault-catalog-contract.md
│   └── incident-drill-script-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
scripts/incident-drill.ps1                    # sửa: -Inject / -Restore / -Hint, bốc thăm 8 nhóm, bộ chuyển đổi Compose
scripts/incident-drill/catalog.json           # mới — danh mục nhóm/loại (không chứa lệnh docker)

postman/ecommerce.postman_collection.v2.json  # sửa: thêm folder "31 - Danh mục nhóm lỗi" (6 subfolder D–I)

specs/025-chaos-pod-kill-latency/             # sửa: gỡ kill-pod/k8s (spec, plan, research, data-model, tasks, quickstart, contracts, checklist)
docs/dien-tap-chaos-engineering/
├── README.md                                 # sửa: gỡ kill-pod, thêm mục danh mục nhóm lỗi + lệnh mới
├── mau-ket-qua.md                            # sửa: gỡ trường/ví dụ kill-pod
├── mau-ban-ghi-su-co.md                      # sửa nhẹ: nhắc loại D–I, -Restore, nhật ký gợi ý (nếu cần)
└── ket-qua/
    ├── 2026-09-12-kill-pod.md                # XOÁ
    ├── 2026-09-14-kill-pod.md                # XOÁ
    └── 2026-09-14-inject-latency.md          # giữ

docs/development/025_*.md                     # sửa: gỡ phần kill-pod
docs/spec-summary-vi/025-*.json               # sửa: gỡ FR/SC kill-pod
docs/diagrams/025-chaos-pod-kill-latency-component.drawio   # sửa: gỡ khối k8s/kill-pod
docs/PO|QA|architecture/025_*.md              # sửa: gỡ kill-pod/k8s
docs/QA/QA_Debt.md, docs/architecture/technical-debt.md     # sửa: gỡ câu/mục kill-pod của 025; thêm mục 031
docs/PO/functional-debt.md                    # sửa: thêm mục 031
```

Tài liệu đi kèm (tách thành task ở `/speckit-tasks`):
- `docs/PO/031_PO_danh mục 8 nhóm lỗi luyện troubleshoot.md`,
  `docs/QA/031_QA_danh mục 8 nhóm lỗi luyện troubleshoot.md`,
  `docs/architecture/031_Architect_danh mục 8 nhóm lỗi luyện troubleshoot.md`;
- 3 sơ đồ drawio `031-*` (component, flow nghiệp vụ, sequence) trong `docs/diagrams/`;
- `docs/spec-summary-vi/031-*.json` nếu nếp 027/028 có.

**Structure Decision**: Không tạo dự án mới. Danh mục nằm cạnh script (`scripts/incident-drill/`),
không vào `docs/` để script không phụ thuộc đường dẫn tài liệu (người dùng chọn phương án này). Bộ
chuyển đổi Compose nằm trong chính `incident-drill.ps1` (người dùng chốt), tách bạch về giao diện: danh
mục chỉ nêu *loại* và *tham số trừu tượng*, các hàm `Invoke-Compose<Inject|Restore>` nêu *cách làm*.

## Điểm dừng bắt buộc khi triển khai

- [research.md](./research.md) mục "Điểm phải xác minh" (V1–V8) phải được kiểm chứng trên stack thật ở
  task đầu tiên. **V4 (memory — đã chốt 256m) hoặc V9 (header qua gateway) cho kết quả không như dự kiến thì dừng
  và hỏi lại người dùng**, không tự đổi phương án (V4: có thể OOM-kill; V9: header có thể không tới orders). V3 đã được hỏi và giải quyết: bỏ biến thể dừng identity-api.
- Các đề xuất chờ duyệt đã được người dùng duyệt ở `/speckit-tasks` (2026-10-05): quy tắc bốc thăm mù,
  route nhóm D qua gateway có token (kèm điểm xác minh V9), phong cách gợi ý. Văn bản 27 đoạn gợi ý
  còn phải được người dùng duyệt ở task T036 trước khi coi xong.
- Xoá Elastic sau triển khai: **hỏi lại người dùng trước khi xoá** (đã chốt ở phiên rà soát nợ).
- Không commit: người dùng tự commit (đã chốt).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Nguyên tắc III (Test-First): không có test viết trước cho `incident-drill.ps1` mở rộng, `catalog.json` và folder Postman 31. | Người dùng chốt không viết test tự động cho script mở rộng: không file nào dưới `services/`/`shared/` bị sửa; phần mới là công cụ vận hành, dữ liệu và tài liệu, được kiểm chứng bằng chính buổi diễn tập thật theo `quickstart.md`, folder Postman 31 và báo cáo QA. **Thời hạn**: tới khi spec D (tài liệu troubleshoot chi tiết) hoàn tất; ghi vào `docs/architecture/technical-debt.md`. | Viết test (Pester cho script; kiểm lược đồ `catalog.json`) — người dùng không chọn. Rủi ro đã biết: danh mục và bộ chuyển đổi có thể lệch nhau (một loại có trong danh mục nhưng thiếu thao tác), hoặc thao tác docker đổi hành vi theo phiên bản Docker, mà không có build nào bắt. |
