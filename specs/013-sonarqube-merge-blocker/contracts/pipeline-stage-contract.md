# Hợp đồng: Stage pipeline CI ↔ Branch protection GitHub ↔ SonarQube

"Giao diện bên ngoài" của tính năng này không phải một HTTP API — mà là tập hợp tên và đường dẫn mà
Jenkins, GitHub branch protection, và SonarQube phải thống nhất với nhau để cổng chất lượng hoạt
động đúng. Bất kỳ thay đổi nào về tên stage, hoặc khi một service mới gia nhập pipeline, PHẢI giữ
nguyên hợp đồng này hoặc cập nhật nó (và cấu hình branch protection) trong cùng một thay đổi.

**Tài liệu này thay thế** `specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md`
(đã bị xoá cùng spec cũ). Nội dung kỹ thuật không đổi vì hạ tầng (`Jenkinsfile`,
`scripts/ci/setup-branch-protection.sh`) chưa được sửa; các file đó hiện vẫn trỏ tới đường dẫn cũ
trong comment của chúng — cập nhật tham chiếu là một việc cần làm ở `tasks.md`.

## 1. Tên required status check (Jenkins → GitHub)

Danh sách "required status checks" của GitHub branch protection tham chiếu đúng các tên check sau,
được đăng bởi năm stage cấp cao nhất của `Jenkinsfile` (research.md Decision 2):

| Stage | Tên check bắt buộc | Khiến PR thất bại khi |
|---|---|---|
| Build | `ci/build` | Build solution/monorepo thất bại |
| Unit test | `ci/unit-tests` | Bất kỳ project `*.Api.UnitTests` (hoặc bộ test unit của `turbo run test`) thất bại |
| Integration test | `ci/integration-tests` | Bất kỳ project `*.Api.IntegrationTests` thất bại (Testcontainers) |
| Contract test | `ci/contract-tests` | Bất kỳ project `*.Api.ContractTests` thất bại (Pact) |
| Cổng chất lượng | `ci/sonarqube-quality-gate` | Kết quả cổng chất lượng SonarQube khác `OK`, hoặc việc chờ cổng hết thời gian (research.md Decision 3) |

**Ai dùng bảng này**: người cấu hình GitHub branch protection (research.md Decision 5, thực hiện
qua `scripts/ci/setup-branch-protection.sh`) phải liệt kê đủ năm tên trên làm required check, kèm
"Do not allow bypassing the above settings" bật (research.md Decision 7 — hiện chưa áp dụng được do
giới hạn gói dịch vụ). Đổi tên một stage trong `Jenkinsfile` mà không cập nhật branch protection sẽ
âm thầm loại stage đó khỏi phạm vi thực thi — đây chính là lỗi mà hợp đồng này tồn tại để ngăn.

## 2. Hợp đồng báo cáo coverage (project test từng service → SonarQube)

Để một test project backend được tính vào số coverage mà cổng chất lượng đánh giá, nó PHẢI:

- Được đặt tên theo quy ước `<Service>.Api.{UnitTests,IntegrationTests,ContractTests}` (quy ước
  hiện có, đã áp dụng cho `baskets`, `bff`, `orders`, `products`; `gateway` và `parties` hiện có
  unit + integration test nhưng chưa có contract test).
- Tham chiếu `coverlet.collector` (đã là package reference toàn repo) và chạy dưới
  `dotnet test --collect:"XPlat Code Coverage"`, sinh ra file Cobertura XML được
  `scripts/ci/merge-coverage.sh` gộp lại và tiêu thụ qua `sonar.cs.cobertura.reportsPaths`.

Để một package frontend được tính vào coverage, nó PHẢI expose một Turbo task `test` hỗ trợ
`--coverage` và sinh ra `lcov.info`, tiêu thụ qua `sonar.javascript.lcov.reportPaths` /
`sonar.typescript.lcov.reportPaths`.

Một service/package không đáp ứng hợp đồng này đơn giản là vắng mặt khỏi phép tính coverage — nó
không làm build thất bại, nhưng mã của nó không được cổng chất lượng bảo vệ.

## 3. Hợp đồng chờ cổng chất lượng (Jenkins ↔ SonarQube)

- Phân tích Sonar cho một PR PHẢI được gắn nhãn theo nhánh/commit của PR đó để PR decoration của
  SonarQube (research.md Decision 8, khi Community Branch Plugin đã cài) gắn đúng vào PR trên
  GitHub.
- Pipeline PHẢI gọi `waitForQualityGate()` sau khi nộp phân tích và PHẢI coi bất kỳ trạng thái khác
  `OK`, cũng như việc hết thời gian chờ webhook của SonarQube, là một stage thất bại — không bao
  giờ mặc định thành công hay bỏ qua.

## 4. Hợp đồng dự phòng audit (GitHub)

Không có endpoint audit tùy biến nào được đưa vào. Cơ chế dự phòng audit (FR-009) là audit log gốc
của tổ chức/repository trên GitHub cho các thay đổi cấu hình branch protection, cộng với việc GitHub
tự chối các nỗ lực merge khi có required check đang thất bại/đang chờ. Nghĩa vụ duy nhất của tính
năng này với hợp đồng này là đảm bảo branch protection thực sự được cấu hình đúng như mục 1 — phần
còn lại thuộc về GitHub.

**Trạng thái hiện tại (2026-09-01, T017)**: nghĩa vụ đó **đã hoàn thành và đã xác minh trên GitHub
thật** — không chỉ bật thành công (T009/T010), mà còn chặn merge thật với zero đường vòng, kể cả cho
chủ repo (PR #3), và tự mở khoá merge ngay khi gate đạt (PR #2, #6, #9). Bản thân cơ chế dự phòng
audit cũng đã xác minh, không cần viết thêm mã nào:

- **Ai đổi cấu hình branch protection, khi nào**: `github.com/settings/security-log` (lọc theo
  `repo:nmhieuit/ecommerce`) ghi lại sự kiện `repo.change_merge_setting` ("Blocked a merge setting on
  the nmhieuit/ecommerce repository") kèm actor, thời gian, và IP — đây là hình thức audit log của
  GitHub cho tài khoản cá nhân (tổ chức/Enterprise có endpoint audit-log riêng, phong phú hơn, nhưng
  không cần thiết ở quy mô repo này).
- **Trạng thái cổng chất lượng tại thời điểm mỗi lần merge thành công**: `GET
  /repos/{owner}/{repo}/commits/{sha}/status` trả về đầy đủ lịch sử 5 required check
  (`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
  `ci/sonarqube-quality-gate`) cho bất kỳ SHA nào đã từng chạy pipeline, kể cả sau khi PR đã merge và
  đóng — đã dùng lệnh này thật hàng chục lần xuyên suốt T010–T015 để xác minh mọi lượt build. Tab
  "Checks" trên giao diện PR cũng hiển thị cùng dữ liệu này cho người không dùng API.

Kết hợp hai nguồn trên trả lời đầy đủ câu hỏi FR-009 đặt ra ("lượt merge nào, cổng chất lượng lúc đó
ra sao") mà không cần một endpoint audit tuỳ biến nào.
