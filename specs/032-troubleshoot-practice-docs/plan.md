# Implementation Plan: Tài liệu luyện troubleshoot theo nhóm lỗi (hướng dẫn step by step cho lỗi có kịch bản, gợi ý theo triệu chứng cho lỗi bất ngờ)

**Branch**: `feature/032-troubleshoot-practice-docs` | **Date**: 2026-10-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/032-troubleshoot-practice-docs/spec.md`

## Summary

Viết chuỗi tài liệu 09–17 trong `docs/kibana-quan-sat-he-thong/` biến danh mục 8 nhóm lỗi của 031 thành
bài học tự làm được, **không sửa script, catalog, service, dashboard hay rule**.

- **File 09–16**: mỗi nhóm một file (nhóm 2 chung cho B và C) theo khung cố định
  ([contracts/guide-structure-contract.md](./contracts/guide-structure-contract.md)): triệu chứng, nơi
  nhìn, lệnh tiêm, truy vấn đối chiếu, khôi phục, xác nhận khỏi, giới hạn đã biết, "Bạn sẽ thấy", bài tập,
  checklist "đã đạt".
- **File 17**: gợi ý theo TRIỆU CHỨNG, mỗi triệu chứng 3 mức mở dần (1 triệu chứng + cách kiểm, 2 tên nhóm,
  3 đáp án), cùng nghĩa với `-Hint`; kèm các bẫy/nhiễu thật đã gặp.
- **Bằng chứng**: chạy thật cả 8 nhóm bằng `-Inject` trên stack, ghi số đo (ngày, điều kiện, truy vấn)
  vào từng file. Việc chạy stack và dọn Elastic phải hỏi lại người dùng.
- **Test mới** `tests/TroubleshootGuideConventionTests` (xUnit, đọc file trên đĩa như
  `ServiceManifestSloConventionTests`): link nội bộ không hỏng; mỗi nhóm 1–8 có file hướng dẫn; mỗi loại
  A–I có mục trong file 17. Viết TRƯỚC tài liệu (đỏ → xanh).
- **Sửa** `00-tong-quan-lo-trinh.md` và README diễn tập; **đi kèm**: PO/QA/Architect, 3 drawio mới, folder
  Postman `32 - Luyện troubleshoot` (8 subfolder), cập nhật 3 file nợ.

Chi tiết quyết định: [research.md](./research.md).

## Technical Context

**Language/Version**: Markdown (tài liệu tiếng Việt có dấu); C# / .NET 10 (project test, xUnit, cùng
`TargetFramework` với các suite quy ước hiện có); JSON (Postman collection v2.1); drawio (XML).

**Primary Dependencies**: Không thêm package mới (kiểm link/phủ bằng `System.Text.RegularExpressions` và
`System.Text.Json` có sẵn). Để đo thật: Docker Compose + `scripts/incident-drill.ps1` + Kibana/Elasticsearch
của stack local (đã có từ 017–031).

**Storage**: Không có. Số đo ghi thẳng vào tài liệu; không file dữ liệu mới.

**Testing**: Project xUnit mới `tests/TroubleshootGuideConventionTests`, thêm vào `Ecommerce.slnx`, chạy cùng
`dotnet test`. Không dựng container, không build service. Kiểm chứng tài liệu còn lại bằng
[quickstart.md](./quickstart.md), folder Postman 32 và báo cáo QA.

**Target Platform**: Máy dev Windows chạy `docker-compose.local.yml`; test chạy mọi nơi có .NET.

**Project Type**: Tài liệu + test quy ước tài liệu trong monorepo; không có service mới.

**Performance Goals**: Không có. Test chạy trong vài giây (đọc ~11 file văn bản).

**Constraints**:
- Không sửa `scripts/`, `services/`, `shared/`, `docker/`, `deploy/`, `docker-compose*.yml`, `.env.example`,
  dashboard `.ndjson`, rule `.ndjson`, `catalog.json` (FR-011, SC-006).
- Tài liệu viết riêng với `catalog.json`; test KHÔNG kiểm nội dung khớp (chấp nhận lệch, ghi nợ).
- Mọi số đo/truy vấn đã chạy thật; không số suy đoán (FR-006).
- Lỗi do chạy đo tính vào ngân sách lỗi tuần như sự cố thật.

**Scale/Scope**: 9 file tài liệu mới + 2 file sửa + 1 project test (2 lớp test) + 3 tài liệu PO/QA/Architect + 3
drawio + 1 folder Postman (8 subfolder) + cập nhật 3 file nợ.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Không chạm service. | N/A |
| II. Contract-First | 2 hợp đồng (`contracts/`) viết trước tài liệu và test. | PASS |
| III. Test-First | Test quy ước tài liệu viết trước, thấy đỏ rồi mới viết tài liệu cho xanh. Script/catalog của 031 vẫn không có test tự động — xem Complexity Tracking. | PASS (tài liệu); **SAI LỆCH 031 CÒN MỞ** (script) |
| IV, V, IX | Không liên quan. | N/A |
| VI. Secure by Default | Không thêm secret; tài liệu không chép mật khẩu/token thật, chỉ trỏ `.env`/biến. | PASS |
| VII. Observable by Default | Tài liệu dạy đọc đúng telemetry OTel hiện có. | PASS |
| VIII. Performance and Resilience Budgets | Lỗi chạy đo tiêu hao ngân sách tuần như sự cố thật (FR-013). | PASS |
| X. Toggle-Gated, Reversible | Không thêm hành vi chạy; chỉ tài liệu. Khôi phục bằng `-Restore` của 031. | PASS |

**Gate**: qua. Sai lệch Nguyên tắc III của 031 được xử lý ở Complexity Tracking.

**Re-check sau Phase 1**: data-model/contracts không thêm service, secret hay đổi hợp đồng HTTP/event. Giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/032-troubleshoot-practice-docs/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── guide-structure-contract.md
│   └── guide-convention-test-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
tests/TroubleshootGuideConventionTests/        # mới — xUnit, không tham chiếu service nào
├── TroubleshootGuideConventionTests.csproj
├── GuideFixture.cs                            # tìm gốc repo, đọc catalog.json + file 09–17
├── GroupCoverageTests.cs                      # nhóm 1–8 ↔ file 09–16; loại A–I ↔ file 17
└── InternalLinkTests.cs                       # mọi link nội bộ trong file 09–17 có đích thật
Ecommerce.slnx                                 # sửa: thêm project test

docs/kibana-quan-sat-he-thong/
├── 00-tong-quan-lo-trinh.md                   # sửa: lộ trình 01–17, gỡ "Bộ 4 file"/"cả 6 file"
├── 09-dich-ket-noi-sai.md                     # mới (nhóm 1: A)
├── 10-nghen-va-loi-theo-ty-le.md              # mới (nhóm 2: B, C)
├── 11-do-tre-orders.md                        # mới (nhóm 3: D)
├── 12-ha-tang-dung.md                         # mới (nhóm 4: E)
├── 13-xac-thuc-hong.md                        # mới (nhóm 5: F)
├── 14-thieu-tai-nguyen.md                     # mới (nhóm 6: G)
├── 15-mang-dut.md                             # mới (nhóm 7: H)
├── 16-container-chet-hoac-khoi-dong-lai.md    # mới (nhóm 8: I)
└── 17-goi-y-theo-trieu-chung.md               # mới (dạng b)
docs/dien-tap-chaos-engineering/README.md      # sửa: link sang chuỗi mới

postman/ecommerce.postman_collection.v2.json   # sửa: thêm folder "32 - Luyện troubleshoot" (8 subfolder)
docs/PO/032_PO_tài liệu luyện troubleshoot theo nhóm lỗi.md
docs/QA/032_QA_tài liệu luyện troubleshoot theo nhóm lỗi.md
docs/architecture/032_Architect_tài liệu luyện troubleshoot theo nhóm lỗi.md
docs/diagrams/032-troubleshoot-practice-docs-{component,flow-nghiep-vu,sequence}.drawio
docs/QA/QA_Debt.md, docs/architecture/technical-debt.md, docs/PO/functional-debt.md   # sửa: thêm mục 032
```

**Structure Decision**: Tài liệu nằm cùng chuỗi 01–08; test là project xUnit riêng cùng kiểu
`ServiceManifestSloConventionTests` (đọc file trên đĩa, không tham chiếu service), nên chạy nhanh và không
cần Docker. Không đụng `scripts/`.

## Complexity Tracking

| Điểm | Vì sao | Phương án đơn giản hơn bị loại vì |
|---|---|---|
| Sai lệch Nguyên tắc III của 031 (script `incident-drill.ps1` + `catalog.json` không test tự động) vẫn còn | Spec 032 chỉ viết tài liệu, không sửa script (FR-011); thời hạn "đến khi spec D hoàn tất" của 031 hết ở đây nhưng test script nằm ngoài phạm vi D | Thêm test script vào 032: trái FR-011 và phạm vi đã chốt. **Người dùng quyết (2026-10-06): mở spec riêng cho test script** — ghi ở `technical-debt.md`, hạn mới là khi spec đó xong (chưa tạo) |
| Tài liệu và `catalog.json` viết riêng (hai nơi gợi ý) | Người dùng chốt: văn phong tài liệu nặng hơn, linh hoạt hơn catalog | Dùng chung nguồn: bị loại theo quyết định người dùng; test chỉ kiểm phủ; rủi ro lệch ghi ở `technical-debt.md` |
