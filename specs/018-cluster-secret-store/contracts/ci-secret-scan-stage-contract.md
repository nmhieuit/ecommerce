# Hợp đồng: CI stage secret-scanning

**Feature**: [../spec.md](../spec.md) | Liên quan: FR-004, FR-005, FR-006, SC-001, SC-002, SC-005

Kế thừa trực tiếp convention "stage name is a contract, not a label" của `docs/adr/0012-ci-quality-gate-enforcement.md` §Consequences: đổi tên check dưới đây mà không cập nhật cấu hình liên quan sẽ âm thầm gỡ stage đó khỏi enforcement.

## Check mới được thêm vào `Jenkinsfile`

| Tên check (GitHub status) | Stage trong `Jenkinsfile` | Công cụ | Phạm vi quét | Liên quan |
|---|---|---|---|---|
| `ci/secret-scan` | `secret scan` | gitleaks | Toàn bộ lịch sử git của repository (không giới hạn theo nhánh/PR diff) | FR-004, FR-006, SC-001, SC-005 |
| `ci/image-secret-scan` | `image secret scan` | Trivy (secret-scan mode) | Filesystem của container image vừa build ở stage `build` | FR-005, FR-006, SC-002, SC-005 |

## Quy tắc bắt buộc

1. **Publish qua Status API (`githubNotify`), không dùng `publishChecks`** — đúng bài học đã ghi tại ADR-0012 Amendment 2026-08-29 (personal access token không dùng được với Checks API).
2. **Fail-closed**: nếu gitleaks/Trivy không chạy được (binary lỗi, timeout, agent thiếu công cụ) thì stage PHẢI báo `FAILURE`, không được coi là `SKIPPED`/pass ngầm — nhất quán với hành vi fail-closed đã áp dụng cho `ci/sonarqube-quality-gate`.
3. **Không log giá trị secret ra console Jenkins**: cấu hình gitleaks/Trivy chạy ở chế độ chỉ xuất vị trí phát hiện (file, dòng, rule đã khớp), không xuất chuỗi giá trị thật khớp pattern.
4. **Không có cơ chế bỏ qua theo từng PR**: loại trừ false-positive chỉ được thực hiện qua `.gitleaks.toml` / Trivy ignore-file ở gốc repo, review như mọi thay đổi code khác — không có tham số/flag để một PR tự tắt riêng stage này cho chính nó. Ngoại lệ duy nhất, có chủ đích: `.gitleaks-baseline.json` (research.md Decision 6) chốt lại đúng các phát hiện lịch sử đã biết từ trước khi feature này tồn tại (không thể xoá an toàn khỏi lịch sử git — xem spec.md Assumptions) — đây không phải cơ chế bỏ qua per-PR, mà là một file được review và commit một lần; bất kỳ phát hiện mới nào ngoài baseline vẫn chặn merge như bình thường.
5. **Hai check này là required status check bổ sung**: khi branch protection của repository (đã bật từ ADR-0012 Amendment 2026-08-29) được cập nhật, `ci/secret-scan` và `ci/image-secret-scan` PHẢI được thêm vào danh sách required checks cùng 5 check hiện có (`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`, `ci/sonarqube-quality-gate`).

## Vị trí trong pipeline

`ci/secret-scan` (gitleaks trên git history) không phụ thuộc vào kết quả `build`, có thể chạy song song với stage `build` để giảm thời gian pipeline tổng thể. `ci/image-secret-scan` (Trivy trên image) PHẢI chạy **sau** stage `build` vì cần image đã build xong.

## Kiểm chứng hợp đồng

Theo đúng phương pháp ADR-0012 đã dùng để validate 5 check gốc: chạy hai stage mới trên ít nhất hai PR thật — một PR "sạch" (cả hai check phải pass) và một PR cố tình chèn một secret giả lập/test pattern (cả hai check phải fail với lý do rõ ràng, và nút merge của PR đó bị khoá) — xem [../quickstart.md](../quickstart.md) cho kịch bản chạy cụ thể.
