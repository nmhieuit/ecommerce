# Contract: Sửa đổi hiến chương 1.0.0 → 2.0.0 (Nguyên tắc VIII)

**Feature**: [../spec.md](../spec.md) | **File**: `.specify/memory/constitution.md`

**Người tiêu thụ**:
- người review PR (Governance: "Amendments require a pull request that states the rationale and the migration impact on existing services");
- `tests/ServiceManifestSloConventionTests/PlatformSloDefaults.cs`, nơi lặp lại các con số này.

## Thay đổi nội dung (chỉ 2 dòng trong danh sách mặc định của Nguyên tắc VIII)

| Trước | Sau |
|---|---|
| `- 99.9% monthly availability.` | `- 99% weekly availability.` |
| `- 5xx responses below 0.1% of requests.` | `- 5xx responses below 1% of requests.` |

Không dòng nào khác trong thân hiến chương được đổi.

## Dòng phiên bản

`**Version**: 2.0.0 | **Ratified**: 2026-08-13 | **Last Amended**: 2026-10-05`

## Sync Impact Report (thay khối comment đầu file)

Bắt buộc có:
- `Version change: 1.0.0 → 2.0.0`.
- Lý do MAJOR: định nghĩa lại mặc định nền tảng của Nguyên tắc VIII theo cách không tương thích ngược. Chu kỳ khả dụng đổi từ tháng sang tuần, mức khả dụng 99.9% → 99%, ngưỡng 5xx 0.1% → 1%.
- Principles modified: `VIII. Performance and Resilience Budgets` (2 dòng mặc định).
- Tác động chuyển đổi lên service hiện có:
  - 7 `service-manifest.yaml` (`slos`, `error-budget-policy`);
  - `PlatformSloDefaults`;
  - 4 rule ngân sách lỗi (027), rule `incident-fast-detection` (028);
  - dashboard Ngân sách lỗi tuần;
  - tài liệu 021/027/028.
- Templates đã rà: `.specify/templates/plan-template.md`, `spec-template.md`, `tasks-template.md`, `checklist-template.md`, cùng file hướng dẫn agent của repo. Mỗi file ghi ✅ đã cập nhật hoặc ✅ không cần đổi.
- Deferred TODOs: không có.

## Bất biến

| # | Bất biến |
|---|---|
| 1 | Nguyên tắc VIII chứa đúng hai dòng "Sau" ở bảng trên, và không còn "monthly availability" hay "below 0.1%". |
| 2 | Phiên bản `2.0.0`, `Last Amended` `2026-10-05`, `Ratified` giữ `2026-08-13`. |
| 3 | Con số trong `PlatformSloDefaults` bằng đúng con số trong hiến chương (99% / 1%). |
| 4 | Không có tham chiếu "99.9%" / "monthly" nào khác trong `.specify/templates/` hay file hướng dẫn agent mâu thuẫn với hiến chương mới. |
