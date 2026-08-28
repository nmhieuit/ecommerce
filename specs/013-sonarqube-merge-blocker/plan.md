# Kế hoạch triển khai: Cổng chất lượng SonarQube chặn merge Pull Request

**Nhánh**: `013-sonarqube-merge-blocker` | **Ngày**: 2026-08-27 | **Đặc tả**: [spec.md](./spec.md)

**Đầu vào**: Đặc tả tính năng từ `specs/013-sonarqube-merge-blocker/spec.md`

**Ghi chú**: File này do lệnh `/speckit-plan` tạo ra; định nghĩa của lệnh mô tả quy trình thực thi bên dưới.

## Tóm tắt

Pipeline Jenkins năm bước (build → unit test → integration test → contract test → cổng chất lượng
SonarQube), file `sonar-project.properties`, môi trường Jenkins + SonarQube cục bộ
(`docker-compose.ci.yml`) và script `scripts/ci/setup-branch-protection.sh` **đã tồn tại sẵn trong
repo** từ một nỗ lực triển khai trước (ghi lại tại [ADR-0012](../../docs/adr/0012-ci-quality-gate-enforcement.md)
và [docs/github-jenkins-sonarqube-setup.md](../../docs/github-jenkins-sonarqube-setup.md)). Kế
hoạch này **không xây mới** hạ tầng đó — nó đối chiếu hạ tầng hiện có với 9 yêu cầu chức năng của
`spec.md` (013), cập nhật các tham chiếu đường dẫn từ `specs/012-...` (đã bị xoá) sang
`specs/013-...`, và giải quyết dứt điểm hai khoảng trống mà nỗ lực trước để ngỏ:

1. **Bảo vệ nhánh (branch protection) hiện không thể bật được** — `setup-branch-protection.sh` đã
   xác nhận bằng thực nghiệm rằng GitHub từ chối (HTTP 403) bật branch protection trên một
   repository **private** ở gói miễn phí, kể cả với token quản trị. Đây là **rào cản chặn thẳng
   FR-003** (yêu cầu cốt lõi P1 "không có đường vòng"), không phải một chi tiết kỹ thuật phụ.
2. **Chỉ số chất lượng chưa hiển thị trên PR (FR-005)** — SonarQube Community Edition không có
   tính năng PR decoration chính thức; ADR-0012 để ngỏ quyết định chọn plugin cộng đồng hay nâng
   cấp phiên bản trả phí.

Cả hai được giải quyết thành quyết định cụ thể ở `research.md` (Decision 7, 8) thay vì để ngỏ.

## Bối cảnh kỹ thuật

**Ngôn ngữ/Phiên bản**: Groovy (Jenkins declarative `Jenkinsfile`, đã có sẵn) điều phối các bộ công
cụ đã tồn tại — C#/.NET 10 (`Ecommerce.slnx`) và TypeScript (pnpm/Turborepo `frontend/`). Không có
ngôn ngữ ứng dụng mới nào được đưa vào bởi tính năng này.

**Thành phần phụ thuộc chính**: Jenkins plugin GitHub Branch Source, GitHub Checks, SonarQube
Scanner (cả ba đã được cài đặt và xác nhận hoạt động ở môi trường Jenkins cục bộ theo
`docs/github-jenkins-sonarqube-setup.md`); `dotnet-sonarscanner`; `coverlet.collector` (đã có sẵn
trong toàn repo cho coverage backend); `@vitest/coverage-v8` (coverage frontend); GitHub CLI (`gh`)
cho `scripts/ci/setup-branch-protection.sh`. Cần bổ sung: SonarQube Community Branch Plugin
(research.md Decision 8) để đóng khoảng trống FR-005.

**Lưu trữ**: Không áp dụng — tính năng này không thêm dữ liệu nghiệp vụ mới. Coverage/kết quả phân
tích là artefact tạm thời của CI (Cobertura XML, `lcov.info`), được SonarQube và Jenkins tự quản lý
lịch sử lưu trữ của chính chúng.

**Kiểm thử**: Tính năng được xác nhận bằng cách chạy chính pipeline trước các PR thật (xem
`quickstart.md`), không phải bằng một bộ unit test mới — không có mã ứng dụng nào để unit test ở
đây. Các bộ `*.Api.UnitTests` / `*.Api.IntegrationTests` / `*.Api.ContractTests` (.NET, xUnit, từ
specs 009–011) và bộ Vitest của frontend chính là các test mà 5 stage của pipeline thực thi.

**Nền tảng mục tiêu**: Jenkins controller/agent cục bộ (Docker Desktop, đã dựng theo
`docker-compose.ci.yml`) hiện tại; GitHub.com (`github.com/nmhieuit/ecommerce`, private, nhánh mặc
định `master`) là nơi lưu trữ PR.

**Loại dự án**: Kết nối hạ tầng CI/CD trong monorepo hiện có — không có service mới, không có tính
năng frontend mới, không thay đổi bề mặt API.

**Mục tiêu hiệu năng**: Không phải tính năng về hiệu năng runtime; các ngân sách hiệu năng ở
Nguyên tắc VIII của hiến pháp không bị ảnh hưởng. Thời gian chạy toàn bộ pipeline được giới hạn bởi
`timeout(90 phút)` (toàn pipeline) và `timeout(15 phút)` (chờ cổng chất lượng) đã cấu hình sẵn
trong `Jenkinsfile`.

**Ràng buộc**:
- Không xây dựng công cụ IaC mới (Terraform...) chỉ để quản lý cấu hình branch protection của một
  repository duy nhất (research.md Decision 5, kế thừa từ nỗ lực trước).
- Cổng chất lượng phải fail-closed khi SonarQube không phản hồi (FR-008 của spec cũ, nay là hành vi
  đã triển khai trong `Jenkinsfile` — xem stage "sonarqube quality gate").
- **Ràng buộc mới phát sinh**: branch protection không thể áp dụng được trên repository private ở
  gói GitHub miễn phí — đây là ràng buộc về chi phí/tài khoản, không phải kỹ thuật thuần tuý, và
  nằm ngoài khả năng một phiên làm việc tự động có thể tự quyết định hay chi trả.

**Quy mô/Phạm vi**: Sáu service hiện có (`baskets`, `bff`, `gateway`, `orders`, `parties`,
`products`) cộng với frontend monorepo, phân tích chung trong một Sonar project (`sonar.projectKey
=ecommerce`); phạm vi giới hạn ở 5 stage nêu trong SCRUM-22 — stage quét lỗ hổng container image
mà hiến pháp yêu cầu thêm vẫn nằm ngoài phạm vi (xem Complexity Tracking, kế thừa từ ADR-0012 Action
Item 4).

## Kiểm tra hiến pháp (Constitution Check)

*GATE: Phải đạt trước Phase 0 research. Kiểm tra lại sau Phase 1 design.*

- **Nguyên tắc III (Test-First, KHÔNG THƯƠNG LƯỢNG)** — "Cổng chất lượng SonarQube là cơ quan có
  thẩm quyền về coverage và PHẢI đạt trước khi merge." Tính năng này tồn tại để hiện thực hoá đúng
  câu đó. **ĐẠT** (đã triển khai trong `Jenkinsfile`).
- **Nguyên tắc VI (Secure by Default)** — deny-by-default áp dụng tự nhiên cho việc kiểm soát merge:
  không có đường vòng cho bất kỳ vai trò nào (FR-003/FR-004). **ĐẠT VỀ MẶT THIẾT KẾ, NHƯNG CHƯA ĐẠT
  VỀ MẶT VẬN HÀNH** — xem mục "Development Workflow and Quality Gates" ngay dưới đây.
- **Development Workflow and Quality Gates** — hiến pháp yêu cầu "build → unit → integration →
  contract → SonarQube → quét lỗ hổng container image... không ngoại lệ, không đường vòng, không
  miễn trừ." Pipeline hiện có triển khai đúng thứ tự 5 stage đầu; stage thứ sáu (quét lỗ hổng) chưa
  được triển khai (kế thừa từ ADR-0012, xem Complexity Tracking). Quan trọng hơn: **điều khoản
  "không đường vòng" hiện chưa được thực thi thật trên GitHub**, vì branch protection — cơ chế duy
  nhất biến 5 check này thành merge blocker thật sự — không bật được trên repo private ở gói miễn
  phí (xác nhận bằng thực nghiệm, xem `scripts/ci/setup-branch-protection.sh`). **CONDITIONAL —
  xem Complexity Tracking**, đây là vi phạm có thật, được ghi nhận và giới hạn thời gian, không phải
  một khoảng trống bị bỏ qua âm thầm.
- **Governance** ("các quyết định kiến trúc quan trọng PHẢI được ghi lại dưới dạng ADR") — cách
  tiếp cận Multibranch Pipeline + required status checks đã được ghi tại ADR-0012. **ĐẠT.**
- Các nguyên tắc còn lại (Service Autonomy, Contract-First, Event-Driven, Tenant Isolation,
  Observable by Default, Performance Budgets, Frontend Discipline, Toggle-Gated Delivery) không
  liên quan tới một thay đổi thuần về kết nối CI, không có mã ứng dụng mới. **KHÔNG ÁP DỤNG.**

*Kiểm tra lại sau Phase 1 (data-model.md, contracts/, quickstart.md): thiết kế không phát sinh vi
phạm mới — mọi quyết định vẫn nằm trong các cơ chế gốc của GitHub/Jenkins/SonarQube, không thêm
service, kho dữ liệu, hay mã thực thi tùy biến nào.* **ĐẠT**, với hai mục tồn đọng đã được ghi
nhận tường minh ở Complexity Tracking (không bị bỏ sót).

## Cấu trúc dự án

### Tài liệu (tính năng này)

```text
specs/013-sonarqube-merge-blocker/
├── plan.md              # File này (đầu ra lệnh /speckit-plan)
├── research.md          # Đầu ra Phase 0 (/speckit-plan)
├── data-model.md         # Đầu ra Phase 1 (/speckit-plan)
├── quickstart.md         # Đầu ra Phase 1 (/speckit-plan)
├── contracts/             # Đầu ra Phase 1 (/speckit-plan)
│   └── pipeline-stage-contract.md
└── tasks.md              # Đầu ra Phase 2 (/speckit-tasks — KHÔNG được tạo bởi /speckit-plan)
```

### Mã nguồn (gốc repository) — đã tồn tại, được tái sử dụng bởi tính năng này

```text
Jenkinsfile                       # ĐÃ CÓ — pipeline khai báo 5 stage (research.md Decision 1-3)
                                     # CẦN SỬA: comment đầu file trỏ tới
                                     # "specs/012-sonarqube-quality-gate/contracts/..." đã bị xoá —
                                     # cập nhật sang specs/013-sonarqube-merge-blocker (việc cho tasks.md)
sonar-project.properties          # ĐÃ CÓ — một Sonar project cho cả backend + frontend (Decision 4)
docker-compose.ci.yml             # ĐÃ CÓ — Jenkins LTS + SonarQube Community cục bộ, dùng để
                                     # phát triển/kiểm thử wiring, KHÔNG phải bản production
docker/ci/jenkins-init/           # ĐÃ CÓ — Groovy init script giữ cấu hình SonarQube server qua
                                     # các lần `docker compose down -v`

scripts/ci/
├── setup-branch-protection.sh    # ĐÃ CÓ — áp branch protection; hiện BỊ CHẶN bởi giới hạn gói
                                     # GitHub private/free (xem Bối cảnh kỹ thuật, research.md
                                     # Decision 7). CẦN SỬA: tham chiếu đường dẫn contract specs/012
                                     # → specs/013 trong comment.
├── sonar-begin.sh                 # ĐÃ CÓ — dịch sonar-project.properties thành tham số dòng lệnh
├── run-dotnet-tests.sh             # ĐÃ CÓ — phân loại *Tests.csproj theo tầng (unit/integration/contract)
└── merge-coverage.sh               # ĐÃ CÓ — gộp báo cáo Cobertura sau mỗi tầng test

docs/adr/0012-ci-quality-gate-enforcement.md   # ĐÃ CÓ — ADR ghi quyết định kiến trúc + bản amendment
                                                  # chọn SonarQube self-hosted; CẦN SỬA: thêm mục ghi
                                                  # quyết định branch-protection-blocked (Decision 7)
docs/github-jenkins-sonarqube-setup.md          # ĐÃ CÓ — hướng dẫn nối dây thủ công còn lại (token,
                                                   # webhook) dành cho người vận hành thật
```

**Quyết định cấu trúc**: Đây vẫn là kết nối hạ tầng CI, không phải thành phần ứng dụng mới. Không có
thư mục `src/`, `backend/`, hay `frontend/src/` nào bị thay đổi. Toàn bộ artefact liệt kê ở trên đã
tồn tại trong repository từ trước; công việc của tính năng 013 là (a) đối chiếu chúng với các yêu
cầu chức năng mới trong `spec.md`, (b) cập nhật các tham chiếu đường dẫn `specs/012-...` đã lỗi thời,
và (c) đóng hai khoảng trống nêu ở Tóm tắt — không viết lại pipeline từ đầu.

## Theo dõi độ phức tạp (Complexity Tracking)

> **Chỉ điền khi Constitution Check có vi phạm cần biện minh**

| Vi phạm | Vì sao cần thiết | Vì sao phương án đơn giản hơn bị bác bỏ |
|-----------|------------|---------------------------------------|
| Hiến pháp yêu cầu chuỗi cổng PR gồm build → unit → integration → contract → SonarQube → **quét lỗ hổng bảo mật container image**; tính năng này dừng lại trước bước quét lỗ hổng. | Tiêu chí chấp nhận và kịch bản kiểm thử gốc của SCRUM-22 chỉ giới hạn ở cổng SonarQube; gộp thêm một stage quét lỗ hổng chưa được đặc tả (công cụ, ngưỡng mức độ nghiêm trọng, tích hợp registry) vào ticket này sẽ mở rộng phạm vi vượt quá những gì được yêu cầu. | Việc hoãn stage quét lỗ hổng sang một ticket riêng, được đặc tả rõ ràng, được ưu tiên hơn là đoán các yêu cầu ở đây. Đây là sai lệch có giới hạn thời gian, đã được ghi nhận từ ADR-0012 Action Item 4 (mã ticket `SCRUM-TBD` — chưa có ticket thật). |
| Điều khoản "không có đường vòng" (FR-003) hiện **chưa được thực thi thật** trên GitHub: `scripts/ci/setup-branch-protection.sh` xác nhận bằng thực nghiệm rằng GitHub từ chối (HTTP 403 "Upgrade to GitHub Pro") việc bật branch protection — kể cả repository ruleset thay thế — trên repository **private ở gói miễn phí**, bất kể quyền quản trị của token. | Không có cách nào khác trong phạm vi mã nguồn/CI để đạt được "không đường vòng, kể cả cho admin" trên một repo private miễn phí: GitHub không cung cấp cơ chế thay thế nào ở gói này. | Xây một bot kiểm tra merge tuỳ biến (GitHub App/Action) để tự thực thi quy tắc bị bác bỏ từ ADR-0012 (Option C) vì kém tin cậy hơn tính năng gốc của nền tảng và tự nó lại là một điểm hỏng mới. Phương án còn lại — nâng cấp lên GitHub Pro (~$4/tháng cho tài khoản cá nhân) hoặc chuyển repo sang công khai — là quyết định về chi phí/tài khoản mà một phiên làm việc tự động không được tự ý thực hiện (thay đổi cài đặt tài khoản, mua dịch vụ). **Khuyến nghị** (research.md Decision 7): nâng cấp lên GitHub Pro, giữ repo private, vì chi phí thấp và không đánh đổi việc công khai mã nguồn — chủ repository cần tự xác nhận và thực hiện bước này. **Cập nhật 2026-08-27**: chủ repository đã chọn ngược lại khuyến nghị này — chuyển repo sang **công khai** thay vì trả phí GitHub Pro (xác nhận qua trình duyệt: repo hiển thị "Public", `Settings → Branches` không còn bị chặn). Vi phạm này coi như đã được giải quyết bằng phương án B đã cân nhắc ở research.md Decision 7, không phải phương án được khuyến nghị ban đầu; xem ghi chú cập nhật ở đó về hệ quả đối với giả định "repo private" của ADR-0012. |
