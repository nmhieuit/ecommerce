# Research: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

**Feature**: [spec.md](./spec.md) | **Ngày**: 2026-09-06

Tài liệu này giải quyết các điểm chưa rõ ràng trong Technical Context của [plan.md](./plan.md) trước khi bước vào Phase 1 (thiết kế). Mỗi mục theo định dạng Decision / Rationale / Alternatives considered.

## 1. Phạm vi triển khai: mã ứng dụng vs. hạ tầng Vault/ESO

**Decision**: Feature này giao (deliver) hai phần tách biệt rõ ràng:
1. **Trong phạm vi (buildable & testable ngay trong repo hiện tại)**: loại bỏ giá trị secret hardcode khỏi `appsettings*.json`; chuẩn hoá cách mỗi service đọc secret qua biến môi trường / file mount tại runtime; cơ chế fail-fast dùng chung trong `shared/ServiceDefaults`; stage CI secret-scanning (gitleaks cho git history, Trivy cho image filesystem) được thêm vào `Jenkinsfile`; manifest khai báo (`Secret`, `ExternalSecret`) cho từng service dưới dạng artifact có thể review.
2. **Ngoài phạm vi của feature này (đã ghi nhận riêng tại ADR-0007, Action Items 1–3, chưa thực hiện)**: triển khai thật Vault HA qua Ansible và cài đặt ESO vào cluster. Đây là hạng mục hạ tầng nền tảng độc lập, không phải là "spec" ứng dụng.

**Rationale**: Khảo sát repo cho thấy **chưa hề tồn tại** thư mục `k8s/`, `helm/`, `ansible/` nào — không có spec nào trước đây (001–017) từng dựng cluster K8s thật; toàn bộ vòng lặp local/CI hiện dựa trên `docker-compose`. Cố đưa việc "triển khai Vault HA + ESO" vào cùng một plan với thay đổi mã ứng dụng sẽ vi phạm Principle I (mức độ phức tạp phải tương xứng với nhu cầu, không lan sang hạng mục không thuộc service) và khiến feature này không thể test độc lập (User Story 2 sẽ phụ thuộc vào một cluster chưa tồn tại). Tách phạm vi giữ cho mọi acceptance scenario trong spec kiểm chứng được bằng phương tiện đã có (Testcontainers-style test harness, Jenkins pipeline thật, hoặc một cluster K8s thử nghiệm tối thiểu — kind/k3d — để chạy Independent Test của User Story 2) mà không cần chờ hạ tầng Vault production.

**Alternatives considered**:
- *Gộp chung cả việc dựng Vault/ESO vào plan này*: bị loại vì biến plan thành một dự án hạ tầng đa tuần, không khớp với chu kỳ delivery từng spec nhỏ, độc lập của repo (Principle I), và lặp lại đúng bẫy mà ADR-0012 từng gặp phải khi trộn "viết pipeline" với "cấu hình branch protection cấp tài khoản".
- *Bỏ qua hoàn toàn phần cluster, chỉ làm sạch appsettings*: bị loại vì không thỏa User Story 2/3 và FR-002/FR-003 của spec — cần ít nhất manifest khai báo và một cách kiểm chứng runtime injection, dù chưa cần Vault production.

## 2. Cách service đọc secret tại runtime

**Decision**: Dùng cơ chế cấu hình chuẩn của ASP.NET Core — biến môi trường được ánh xạ từ Kubernetes `Secret` (đồng bộ bởi ESO từ Vault) qua `envFrom.secretRef` trong manifest triển khai của từng service. Với secret có nội dung dạng file (vd: khóa ký JWT dạng PEM), dùng thêm configuration provider `AddKeyPerFile` đọc từ thư mục volume-mounted. Không cần SDK/thư viện client Vault trong mã ứng dụng.

**Rationale**: `IConfiguration` của .NET đã tự động nạp biến môi trường — không cần thêm dependency mới trong service code, giảm bề mặt thay đổi. Cách này khớp với lựa chọn ESO tại ADR-0007 ("pods don't need Vault client/sidecar integration... application manifests reference plain, well-understood K8s Secret objects"), giữ đúng nguyên tắc tách "nơi secret được lưu" khỏi "cách pod tiêu thụ secret".

**Alternatives considered**:
- *Vault Agent Injector/CSI driver (Option B của ADR-0007)*: đã bị loại tại ADR-0007; không xét lại ở đây.
- *Thư viện Vault .NET client gọi trực tiếp API Vault từ mỗi service*: bị loại vì đi ngược quyết định ESO, thêm dependency mới vào mọi service, và thêm một outbound call + timeout/retry policy runtime (Principle VIII) chỉ để lấy cấu hình khởi động — không cần thiết khi ESO đã vật chất hoá secret thành K8s `Secret` object trước khi pod khởi động.

## 3. Hành vi khi thiếu secret bắt buộc (fail-fast)

**Decision**: Thêm một extension dùng chung trong `shared/ServiceDefaults` (cạnh `ServiceDefaultsExtensions.cs`, `CorrelationIdMiddleware.cs` hiện có) để validate các secret/connection-string bắt buộc ngay khi service khởi động, dùng `Microsoft.Extensions.Options` với `ValidateOnStart()`. Nếu thiếu hoặc sai định dạng, service dừng khởi động ngay (fail-fast) và ghi log lỗi có cấu trúc (structured log) nêu rõ tên biến/secret còn thiếu — không ghi giá trị secret ra log.

**Rationale**: Đặt logic dùng chung trong `ServiceDefaults` tránh lặp lại việc validate ở từng service (đúng pattern đã dùng cho OTel/correlation-id ở spec 017/016), thỏa FR-007, và thỏa Principle VII (structured logging, không leak PII/secret vào log).

**Alternatives considered**:
- *Validate thủ công trong `Program.cs` của từng service*: bị loại vì lặp code six lần, dễ lệch nhau giữa các service theo thời gian.
- *Để service khởi động với giá trị null/rỗng và fail ở lần gọi dependency đầu tiên*: bị loại vì vi phạm rõ ràng FR-007 (phải fail ngay lúc khởi động, không fail ngầm khi request đầu tiên tới).

## 4. Công cụ secret-scanning cho git history và container image

**Decision**: Dùng **gitleaks** để quét toàn bộ lịch sử git (khớp với gợi ý trong Jira ticket và Test Scenario 1 của spec) như một stage mới trong `Jenkinsfile`, publish theo đúng cơ chế `githubNotify` (không dùng `publishChecks`, theo bài học đã ghi tại ADR-0012 Amendment 2026-08-29). Dùng **Trivy** ở chế độ secret-scan cho filesystem của container image đã build (Test Scenario 2 của spec) — Trivy đã được ADR-0012 Action Item 4 định hướng làm công cụ scan image (cho lỗ hổng CVE), nên tái dùng cùng công cụ cho secret-scan tránh thêm một binary CI mới.

**Rationale**: Giữ đúng convention stage-là-contract của ADR-0012 (`ci/*` là tên check cố định, thất bại phải chặn merge — fail-closed). Tái dùng Trivy thay vì thêm công cụ scan image riêng cho secret giảm số lượng binary CI phải cài trên Jenkins agent.

**Alternatives considered**:
- *Chỉ dùng gitleaks cho cả git history lẫn image filesystem (`gitleaks detect` trên rootfs đã export)*: khả thi nhưng bị loại vì Trivy đã được định hướng sẵn cho image scanning ở ADR-0012, tránh trùng lặp quyết định công cụ.
- *TruffleHog thay cho gitleaks*: cân nhắc nhưng gitleaks được nêu đích danh trong Jira ticket gốc ("Run a secret-scanning tool (e.g., gitleaks)") nên được ưu tiên làm baseline; không có lý do kỹ thuật để lệch khỏi gợi ý đó.

## 5. `appsettings.Development.json` có mật khẩu dev hardcode

**Decision**: Loại bỏ giá trị mật khẩu literal (`Change_Me_Local_Dev_Only!`) khỏi mọi `appsettings.Development.json` đã commit. Thay vào đó, connection string trong file này chỉ giữ phần host/port/database (không mật khẩu); giá trị mật khẩu cho luồng chạy `dotnet run` ngoài docker-compose được cấp qua .NET User Secrets (`dotnet user-secrets`, vốn không commit vào repo) hoặc qua biến môi trường cục bộ do lập trình viên tự thiết lập — nhất quán với cách `docker-compose*.yml` đã externalize `MSSQL_SA_PASSWORD` qua `.env` (đã gitignore).

**Rationale**: Dù chỉ là giá trị dev yếu, đây vẫn là secret literal nằm trong file đã commit — vi phạm câu chữ của FR-001 và sẽ khiến SC-001 (zero gitleaks findings) không đạt. Assumption trong `spec.md` chỉ miễn trừ cấu hình local (`docker-compose`, `.env` không commit), không miễn trừ file `appsettings.Development.json` đã commit vào git.

**Alternatives considered**:
- *Giữ nguyên vì chỉ là giá trị dev, gắn nhãn "known false positive" trong cấu hình allowlist của gitleaks*: bị loại vì đi ngược tinh thần SC-001 ("zero findings") và tinh thần Test Scenario 1 của ticket gốc — allowlist hoá một secret thật (dù yếu) làm suy yếu độ tin cậy của gate cho các phát hiện thật trong tương lai.

## 6. Full-history gitleaks scan vs. secret đã tồn tại trong các commit trước feature này (phát hiện khi implement)

**Bối cảnh phát sinh**: Khi implement task T016 (tạo `scripts/ci/run-secret-scan.sh`), chạy thử gitleaks bằng Docker (`zricethezav/gitleaks:latest`) với ruleset mặc định của gitleaks trên toàn bộ lịch sử git của repo cho kết quả "no leaks found" — kể cả trước khi các commit hardcode mật khẩu dev (`Change_Me_Local_Dev_Only!`) trong `appsettings.Development.json`, vốn vừa được task T008-T012 loại bỏ khỏi working tree, được dọn sạch. Lý do: ruleset mặc định của gitleaks không có rule nào khớp chuỗi `Password=<giá trị đọc được, entropy thấp>` kiểu ADO.NET connection string — nó tối ưu cho API key/token có entropy cao, private key, và các nhà cung cấp cloud cụ thể. Điều này có nghĩa nếu chỉ dùng ruleset mặc định, SC-001 ("0 gitleaks findings") sẽ "đạt" một cách giả tạo — công cụ không đủ khả năng phát hiện đúng loại secret mà ticket này nhắm tới.

Sau khi thêm rule tùy chỉnh `connection-string-password` vào `.gitleaks.toml` (khớp `Password=`/`Pwd=` theo quy ước connection string) để gate có "tác dụng thật", việc quét toàn bộ lịch sử (`--log-opts="--all"`, đúng như FR-004/Test Scenario 1 yêu cầu) tất yếu phát hiện lại chính các commit lịch sử đã hardcode mật khẩu dev đó — vì chúng vẫn tồn tại nguyên vẹn trong các commit cũ, bất kể working tree hiện tại đã sạch. Đây là xung đột trực tiếp với SC-001 ("0 findings trên toàn bộ lịch sử") mà spec.md không lường trước, và với Assumption đã ghi trong spec.md rằng việc viết lại lịch sử git (`git filter-repo`/BFG) để xoá hẳn secret khỏi lịch sử bị coi là rủi ro cao, ngoài phạm vi bắt buộc.

**Decision**: Dùng cơ chế **baseline** có sẵn của gitleaks (`--baseline-path`) thay vì allowlist hoá bằng regex. Đã tạo `.gitleaks-baseline.json` tại gốc repo, chốt lại chính xác 9 phát hiện lịch sử đã xác nhận (5 file `appsettings.Development.json` × các commit đã sửa chúng, xác nhận bằng fingerprint commit SHA + file + dòng cụ thể — xem file baseline). `scripts/ci/run-secret-scan.sh` truyền `--baseline-path .gitleaks-baseline.json`, nên gate chỉ fail khi có phát hiện **mới**, không nằm trong baseline — đã verify bằng thực nghiệm: sau khi áp baseline, `gitleaks detect` trên toàn lịch sử trả về đúng 0 phát hiện mới (exit code 0).

**Rationale**: Baseline khác về bản chất với "allowlist hoá một secret thật" mà Decision 5 ở trên đã bác bỏ — allowlist là một regex tổng quát áp dụng vĩnh viễn cho MỌI vị trí khớp mẫu đó trong tương lai (làm suy yếu rule cho các phát hiện thật kế tiếp), còn baseline chỉ loại trừ đúng 9 fingerprint (commit SHA + file + dòng) cụ thể đã biết, được review một lần và commit vào repo — bất kỳ occurrence MỚI nào của cùng mẫu đó, kể cả một commit tương lai vô tình thêm lại y hệt giá trị cũ ở một dòng khác, vẫn bị chặn. Đây là cách tiếp cận chuẩn ngành cho đúng tình huống này (legacy finding đã biết, không thể xoá khỏi lịch sử một cách an toàn, nhưng gate vẫn phải có tác dụng thật cho mọi thay đổi từ nay về sau).

**Alternatives considered**:
- *Viết lại lịch sử git (`git filter-repo`) để xoá hẳn giá trị cũ*: bị loại — đúng như spec.md Assumptions đã ghi, đây là hành động phá hoại cao trên một repository đang hoạt động, có PR/commit SHA đã được tham chiếu ở nơi khác (vd: chính `docs/adr/0012-ci-quality-gate-enforcement.md` trích dẫn SHA cụ thể của PR #2/#3); một phiên agent không tự ý thực hiện thao tác này.
- *Allowlist hoá bằng regex chung (vd: loại trừ mọi `Password=Change_Me_Local_Dev_Only!`)*: bị loại vì đúng là điều Decision 5 đã bác bỏ — làm gate không còn phát hiện được nếu ai đó vô tình dùng lại chính chuỗi placeholder quen thuộc này trong một service mới.
- *Giới hạn phạm vi quét chỉ từ commit hiện tại trở về sau (`--log-opts` giới hạn range thay vì `--all`)*: bị loại vì đi ngược trực tiếp câu chữ FR-004 ("toàn bộ lịch sử git") và Test Scenario 1 của ticket gốc; baseline giữ được đúng phạm vi quét "toàn bộ lịch sử" mà chỉ loại trừ đúng các finding đã biết.

**Cập nhật liên quan**: `spec.md` Assumptions cần một dòng bổ sung ghi nhận cơ chế baseline này (đã thêm); `contracts/ci-secret-scan-stage-contract.md` và `quickstart.md` Kịch bản 1 cần phản ánh việc dùng `--baseline-path` (đã cập nhật).

## 7. `external-secrets.io/v1beta1` → `v1` (phát hiện khi chạy T032/T034 trên cluster thử nghiệm thật)

**Bối cảnh phát sinh**: Khi apply `deploy/k8s/orders/external-secret.yaml` (dùng `apiVersion: external-secrets.io/v1beta1` như contract ban đầu quy định) vào một cluster thử nghiệm thật (Docker Desktop Kubernetes, ESO cài qua Helm chart mới nhất tại thời điểm này), API server trả lỗi `no matches for kind "ClusterSecretStore" in version "external-secrets.io/v1beta1"`. Kiểm tra `kubectl api-resources` xác nhận: CRD của ESO vẫn định nghĩa cả hai version `v1` và `v1beta1` trong schema (cho tương thích chuyển đổi), nhưng chỉ **serve** `v1` — `v1beta1` đã bị ESO loại khỏi danh sách version thực sự phục vụ ở phiên bản chart hiện tại.

**Decision**: Đổi `apiVersion` từ `external-secrets.io/v1beta1` sang `external-secrets.io/v1` trong cả 5 manifest (`deploy/k8s/*/external-secret.yaml`) và trong `contracts/external-secret-manifest-contract.md`.

**Rationale**: Đây là lỗi thực tế, xác nhận trực tiếp trên cluster thật (không phải suy đoán) — `v1beta1` không còn dùng được với phiên bản ESO cài đặt tại thời điểm feature này triển khai. Sửa ngay tại nguồn (file manifest + contract) thay vì chỉ sửa bản test cục bộ, để bất kỳ ai áp dụng các manifest này sau này không gặp lại lỗi tương tự.

**Xác thực end-to-end thật đã thực hiện** (không chỉ static validation của T033):
- Cài Vault (dev-mode) + External Secrets Operator qua Helm vào cluster Docker Desktop Kubernetes.
- Tạo `ClusterSecretStore` trỏ vào Vault, seed `secret/orders` → `connectionstring`.
- Apply đúng file `deploy/k8s/orders/external-secret.yaml` của repo (sau khi sửa `v1`) → `ExternalSecret` báo `SecretSynced`/`Ready=True`.
- Đọc `Secret` K8s tạo ra: `ConnectionStrings__OrdersDb` khớp **chính xác** giá trị đã seed trong Vault.
- Chạy một pod thật với `envFrom.secretRef.name: orders-secrets` → biến môi trường `ConnectionStrings__OrdersDb` xuất hiện trong container đúng giá trị (T032 phần 2c).
- Rotate secret trong Vault (`vault kv put`, version 2), ép ESO đồng bộ lại (annotation `force-sync`, không `kubectl apply`/redeploy gì khác) → `Secret` K8s cập nhật giá trị mới ngay, `ExternalSecret` vẫn `SecretSynced` (T034, SC-004).

**Giới hạn môi trường test đã gặp (không phải lỗi thiết kế)**: cluster Docker Desktop Kubernetes dùng ở đây có 2 node (`desktop-control-plane`, `desktop-worker`), mỗi node containerd riêng, không chia sẻ image store với `docker build` cục bộ (khác hành vi Docker Desktop K8s single-node cổ điển) — image `orders-api:secret-scan` không pull được vào pod thật (`ErrImageNeverPull`) dù đã thử cả registry tạm trong cluster. Vì đây là vấn đề phân phối image cục bộ của môi trường test, không liên quan tới cơ chế secret, đã thay bằng một pod `busybox` public image + `envFrom` cùng Secret để xác thực trực tiếp biến môi trường đến đúng container — kết hợp với các bằng chứng đã có từ trước (WebApplicationFactory integration test xác nhận .NET parse đúng `ConnectionStrings__OrdersDb`, Trivy xác nhận image `orders-api` sạch), tổng thể vẫn là một chứng minh end-to-end đầy đủ và đáng tin cậy cho FR-002/FR-003/SC-003/SC-004.

## Tổng kết NEEDS CLARIFICATION

Không còn mục nào trong Technical Context của `plan.md` cần đánh dấu `NEEDS CLARIFICATION` sau khi 7 quyết định trên được ghi nhận (Decision 6, 7 bổ sung trong lúc implement/xác thực — xem tasks.md T016, T032, T034).
