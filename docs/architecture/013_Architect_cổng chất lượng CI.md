# Kiến trúc: Cổng chất lượng SonarQube làm rào chặn merge

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-22, đặc tả tại
[`specs/013-sonarqube-merge-blocker/`](../../specs/013-sonarqube-merge-blocker/), xây trên nền hạ
tầng đã dựng ở `specs/012-sonarqube-quality-gate` (đã xoá thư mục, nhưng mã pipeline nó tạo ra vẫn
còn và vẫn đang chạy). Quyết định kiến trúc gốc: [ADR-0012](../adr/0012-ci-quality-gate-enforcement.md).

**Trạng thái xác minh**: toàn bộ 17/17 nhiệm vụ trong `tasks.md` đã hoàn thành, xác minh bằng các
Pull Request thật trên `github.com/nmhieuit/ecommerce` — không phải mô tả lý thuyết. Danh sách PR
dùng làm bằng chứng: #2, #3, #6, #7, #8, #9, #10, #12.

## 1. Kiến trúc tổng thể

```
GitHub (public repo)  <──── quét định kỳ (5 phút) ────  Jenkins Multibranch Pipeline
        │                                                        │
        │  required status checks + branch protection            │  5 stage tuần tự
        │  (không đường vòng, mọi vai trò)                        │  (Jenkinsfile)
        │                                                        ▼
        └──── comment decoration ────  SonarQube Community Edition
                                        + Community Branch Plugin
```

Ba hệ thống độc lập được nối với nhau hoàn toàn bằng **cơ chế nền tảng có sẵn** — không có webhook
tuỳ biến, không có service trung gian tự viết:

- **GitHub** là nơi lưu trữ mã nguồn, quản lý Pull Request, và là nơi thực thi quyết định chặn/mở
  merge (branch protection) — đây là điểm mấu chốt: quyết định "được merge hay không" nằm ở phía
  GitHub, không phải ở Jenkins hay bất kỳ script nào tự viết.
- **Jenkins** đóng vai trò điều phối: phát hiện thay đổi, chạy tuần tự 5 bước kiểm tra, báo kết quả
  từng bước ngược lại GitHub.
- **SonarQube** phân tích chất lượng mã nguồn và tính toán kết quả cổng chất lượng; một plugin cộng
  đồng đăng kết quả đó trực tiếp lên PR.

**Vì sao tự host SonarQube Community Edition thay vì dùng SonarCloud** (dịch vụ đám mây): repository
từng ở chế độ private, và SonarCloud tính phí cho repo private trong khi Community Edition tự host
thì miễn phí, nhất quán với việc toàn bộ nền tảng đã tự host mọi thứ khác (Kubernetes qua Ansible).
Xem đầy đủ phân tích đánh đổi tại ADR-0012, mục "Amendment (2026-08-23)".

## 2. Mô tả từng thành phần

### 2.1. Jenkins Multibranch Pipeline job

- Dùng plugin **GitHub Branch Source** để tự phát hiện nhánh và Pull Request — không có webhook tự
  viết nào kích hoạt Jenkins; một `PeriodicFolderTrigger` (chu kỳ 5 phút) chủ động quét GitHub.
- Chiến lược phát hiện PR: `OriginPullRequestDiscoveryTrait` với `strategyId=2` ("The current pull
  request revision" — build đúng commit đầu/head của PR).

### 2.2. `Jenkinsfile` — 5 stage tuần tự (tại thời điểm spec 013)

*(Cập nhật: sau khi spec `018-cluster-secret-store` và `019-liveness-readiness-probes` merge,
`Jenkinsfile` hiện có 9 stage — thêm `secret scan`/`image secret scan` (2 check GitHub mới,
`ci/secret-scan`/`ci/image-secret-scan`, LUÔN chạy, không bị `CI_FAST_ITERATION` bỏ qua) và
`deployment manifest lint` (không publish check GitHub riêng). Bảng dưới đây vẫn đúng cho 5 stage gốc
của spec này; bảng đầy đủ 9 stage xem
[07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md § 7](../onboarding/07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md#7-thất-bại-chặn-gì--phần-cần-xác-nhận-không-suy-đoán).)*

| Stage | Tên check GitHub | Thất bại khi nào |
|---|---|---|
| Build | `ci/build` | `dotnet build` hoặc build frontend (pnpm/Turbo) lỗi |
| Unit test | `ci/unit-tests` | Bất kỳ project `*.Api.UnitTests` thất bại |
| Integration test | `ci/integration-tests` | Bất kỳ project `*.Api.IntegrationTests` thất bại (Testcontainers — SQL Server, Redis, RabbitMQ thật) |
| Contract test | `ci/contract-tests` | Bất kỳ project `*.Api.ContractTests` thất bại (Pact) |
| Cổng chất lượng | `ci/sonarqube-quality-gate` | `waitForQualityGate()` trả về khác `OK`, hoặc hết `timeout(15 phút)` |

Mỗi stage gọi `checkStarted`/`checkPassed`/`checkFailed` — các hàm này dùng bước **`githubNotify`**
(Status API), không phân biệt gì với cách required-check của GitHub khớp theo **tên context**.

Stage cuối gọi `dotnet sonarscanner end` rồi `waitForQualityGate(abortPipeline: false)` bên trong
`timeout(15 phút)`. Điểm quan trọng: `dotnet sonarscanner end` chỉ báo thành công khi **tải phân
tích lên** xong — kết quả cổng chất lượng được SonarQube tính **bất đồng bộ** sau đó và báo lại qua
webhook. Nếu chỉ dựa vào exit code của scanner, một cổng chất lượng thất bại vẫn có thể trông như
thành công. `waitForQualityGate()` là cơ chế đúng để biến *kết quả cổng*, chứ không phải *việc tải
lên*, thành điều kiện chặn.

### 2.3. GitHub branch protection

- Nhánh `master` yêu cầu đủ 5 check trên là "required status checks".
- Tuỳ chọn **"Do not allow bypassing the above settings"** được bật — đây là công tắc duy nhất loại
  bỏ đường vòng cho mọi vai trò, kể cả chủ sở hữu repository. Không có mã tự viết nào thực thi việc
  chặn này; toàn bộ nằm trong cơ chế gốc của GitHub.

### 2.4. SonarQube Community Edition + Community Branch Plugin

- Quality Gate mặc định "Sonar way" với 4 điều kiện trên **mã mới** (new code) của mỗi PR:
  `new_violations > 0` (bất kỳ vấn đề mới nào cũng đủ để fail), `new_coverage < 80`,
  `new_duplicated_lines_density > 3`, `new_security_hotspots_reviewed < 100`.
- SonarQube Community Edition **không có** tính năng PR decoration chính thức (đó là tính năng trả
  phí ở bản Developer Edition). Plugin cộng đồng
  [Community Branch Plugin (mc1arke)](https://github.com/mc1arke/sonarqube-community-branch-plugin)
  được cài để lấp khoảng trống này, dùng chung Personal Access Token đã cấu hình cho Jenkins — không
  cần cấp thêm quyền nào khác.
- Plugin này **xoá comment decoration cũ và đăng comment mới** ở mỗi lượt phân tích (không sửa tại
  chỗ, không giữ đồng thời nhiều comment) — nghĩa là tại mọi thời điểm PR chỉ có đúng một comment
  decoration, luôn phản ánh commit mới nhất. Đã xác minh trực tiếp: số liệu coverage ước tính đổi
  thật giữa hai lần phân tích liên tiếp trên cùng PR (79.30% → 79.40%), chứng minh đây là phân tích
  mới chứ không phải bản sao/cache.

### 2.5. Hạ tầng cục bộ: `docker-compose.ci.yml` + `docker/ci/jenkins.Dockerfile`

Hai container trên một mạng Docker riêng (`ci-backbone`): `jenkins` (build từ Dockerfile riêng) và
`sonarqube` (image `sonarqube:community`). Mỗi container có volume riêng để dữ liệu sống sót qua
`docker compose down` (không kèm `-v`). Groovy init script (`docker/ci/jenkins-init/`) tự khôi phục
cấu hình kết nối SonarQube server mỗi lần Jenkins khởi động lại.

## 3. Cơ chế audit (FR-009)

Không có endpoint audit tuỳ biến nào được xây dựng. Hai cơ chế sẵn có của GitHub trả lời đầy đủ câu
hỏi "ai đổi cấu hình chặn merge, và cổng chất lượng ra sao tại thời điểm mỗi lần merge":

- **Ai đổi cấu hình, khi nào**: `github.com/settings/security-log` (lọc theo
  `repo:nmhieuit/ecommerce`) ghi lại sự kiện `repo.change_merge_setting`, kèm actor, thời gian, IP.
- **Trạng thái cổng chất lượng tại thời điểm mỗi lần merge**: `GET
  /repos/{owner}/{repo}/commits/{sha}/status` trả về đầy đủ lịch sử 5 required check cho bất kỳ SHA
  nào, kể cả sau khi PR đã merge và đóng. Tab "Checks" trên giao diện PR hiển thị cùng dữ liệu này.

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/013-quality-gate-component.drawio`](../diagrams/013-quality-gate-component.drawio)
- Sơ đồ trình tự (đầy đủ luồng, gồm cả nhánh thất bại → sửa → tự động chạy lại):
  [`docs/diagrams/013-quality-gate-sequence.drawio`](../diagrams/013-quality-gate-sequence.drawio)

Các lỗi hạ tầng thật đã gặp/sửa (strategyId, Checks API 403, private-repo branch protection, 6 vấn đề
Docker/Testcontainers trong CI), và giới hạn phạm vi (instance chỉ là dev, tiền đề private-repo không
còn đúng 100%): xem [technical-debt.md](technical-debt.md).
