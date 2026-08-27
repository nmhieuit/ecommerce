# Quickstart: Xác nhận cổng chất lượng SonarQube chặn merge

Tài liệu này xác nhận tính năng theo đúng các kịch bản chấp nhận trong `spec.md`. Khác với một
tính năng mới hoàn toàn, phần lớn hạ tầng bên dưới **đã tồn tại** — quickstart này vừa là hướng dẫn
xác nhận, vừa là danh sách những gì còn thiếu trước khi các kịch bản dưới đây có thể chạy được trên
GitHub thật (xem "Điều kiện tiên quyết còn thiếu").

## Điều kiện tiên quyết

**Đã sẵn sàng**:
- `Jenkinsfile`, `sonar-project.properties`, `docker-compose.ci.yml` tại gốc repo.
- Môi trường Jenkins + SonarQube cục bộ (`docker compose -f docker-compose.ci.yml up -d`), ba
  plugin Jenkins bắt buộc (`github-branch-source`, `github-checks`, `sonar`) đã cài và xác nhận
  hoạt động.
- `dotnet` 10 SDK và `pnpm` (qua `corepack`) — công cụ hiện có của repo.

**Còn thiếu (chặn các kịch bản 1–5 chạy trên GitHub thật — xem `docs/github-jenkins-sonarqube-setup.md`)**:
1. Hoàn tất setup wizard của Jenkins + tạo user quản trị (yêu cầu nhập mật khẩu ban đầu vào trình
   duyệt — việc của người vận hành thật).
2. Đăng nhập lần đầu SonarQube, tạo project `nmhieuit_ecommerce`, sinh token phân tích.
3. Kết nối Jenkins ↔ SonarQube (credential + server connection + webhook) và Jenkins ↔ GitHub
   (Personal Access Token + Multibranch Pipeline job).
4. **Nâng cấp gói GitHub lên Pro** cho `nmhieuit/ecommerce` (research.md Decision 7) — bắt buộc
   trước khi bước 5 dưới đây có thể chạy, vì GitHub từ chối bật branch protection trên repo private
   ở gói miễn phí.
5. Sau khi có ít nhất một lượt chạy pipeline thật (để GitHub "biết" về năm check), chạy
   `scripts/ci/setup-branch-protection.sh nmhieuit/ecommerce master` để bật branch protection với
   `enforce_admins: true`.
6. Cài đặt SonarQube Community Branch Plugin (research.md Decision 8) để chỉ số hiển thị trực tiếp
   trên PR (Kịch bản 3 dưới đây).

Các bước 1–3 và 6 không cần quyền tài khoản đặc biệt ngoài quyền quản trị Jenkins/SonarQube cục bộ.
Bước 4 là quyết định chi phí/tài khoản dành cho chủ repository. Bước 5 cần quyền admin trên
repository GitHub.

## Kịch bản 1 — Toàn bộ chuỗi chạy đúng thứ tự (User Story 1, Kịch bản chấp nhận 1)

1. Mở một PR với một thay đổi nhỏ, chắc chắn đạt.
2. Trong tab check của PR, xác nhận cả năm check xuất hiện và chuyển
   `pending → success` theo đúng thứ tự: `ci/build`, `ci/unit-tests`, `ci/integration-tests`,
   `ci/contract-tests`, `ci/sonarqube-quality-gate`.
3. **Kỳ vọng**: nút merge chỉ khả dụng sau khi `ci/sonarqube-quality-gate` thành công.

## Kịch bản 2 — Cổng thất bại chặn merge, không có đường vòng (User Story 1, Kịch bản chấp nhận 2)

1. Mở một PR cố tình xoá coverage của một đoạn mã trước đó đã được test (hoặc thêm một code smell/
   trùng lặp vượt ngưỡng), theo Test Scenario 1 của SCRUM-22.
2. Xác nhận pipeline vẫn chạy qua build → unit → integration → contract, tất cả đạt, và
   `ci/sonarqube-quality-gate` báo thất bại.
3. **Kỳ vọng**: nút merge của PR bị vô hiệu hoá/chặn; không vai trò nào (kể cả admin repo) có tùy
   chọn "merge bất chấp" — **chỉ xác minh được sau khi hoàn tất bước 4/5 ở "Điều kiện tiên quyết"**.
4. Riêng biệt, mở một PR có một unit test cố tình hỏng.
5. **Kỳ vọng**: `ci/unit-tests` thất bại, `ci/integration-tests`/`ci/contract-tests`/
   `ci/sonarqube-quality-gate` không chạy, và PR bị chặn với lý do nêu rõ là `ci/unit-tests`
   (FR-002, FR-006).

## Kịch bản 3 — Cổng đạt thì chỉ số hiển thị trên PR (User Story 2)

1. Từ PR đạt ở Kịch bản 1, mở trang check/PR của nó.
2. **Kỳ vọng**: % coverage, % trùng lặp, và số code smell mới hiển thị trực tiếp trên PR (qua PR
   decoration của Community Branch Plugin, research.md Decision 8), không cần vào SonarQube.
3. Push thêm một commit vào cùng PR.
4. **Kỳ vọng**: chỉ số hiển thị cập nhật theo commit mới, không phải commit trước đó (FR-005,
   FR-007).

## Kịch bản 4 — Sửa và đánh giá lại (User Story 3, Test Scenario 3 của SCRUM-22)

1. Từ PR đang bị chặn ở Kịch bản 2, push một commit khôi phục coverage vượt ngưỡng.
2. **Kỳ vọng**: toàn bộ chuỗi năm stage tự chạy lại; ngay khi `ci/sonarqube-quality-gate` thành
   công, việc chặn merge được gỡ bỏ mà không cần thao tác quản trị thủ công nào khác (FR-007,
   FR-008, SC-004).

## Kịch bản 5 — SonarQube không phản hồi thì fail-closed (Edge case)

1. Tạm thời trỏ URL SonarQube của pipeline tới một địa chỉ không tồn tại (chỉ để test; khôi phục
   sau), hoặc dừng SonarQube server ở môi trường không phải production.
2. Mở/cập nhật một PR và để pipeline chạy tới stage cổng chất lượng.
3. **Kỳ vọng**: `waitForQualityGate()` hết thời gian chờ (15 phút) và `ci/sonarqube-quality-gate`
   báo thất bại (không phải thành công, không phải bị bỏ qua) một khi `timeout()` đã cấu hình hết
   hạn.

## Danh sách kiểm tra tiêu chí thành công

- [ ] SC-001: mọi PR nhắm tới nhánh được bảo vệ tự động kích hoạt đủ năm stage mà không cần thao
      tác thủ công.
- [ ] SC-002: không PR nào merge vào nhánh được bảo vệ khi cổng chất lượng thất bại, ở mọi vai trò
      — **phụ thuộc vào việc hoàn tất bước 4/5 ở Điều kiện tiên quyết**.
- [ ] SC-003: coverage/duplication/code smell hiển thị trên mọi PR mà không cần rời trang PR —
      **phụ thuộc vào bước 6 ở Điều kiện tiên quyết**.
- [ ] SC-004: một PR chỉ bị chặn do regression chất lượng trở nên merge được sau đúng một lần chạy
      lại pipeline sau khi sửa, không cần can thiệp thủ công.
