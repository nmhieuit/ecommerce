# QA: Cổng chất lượng SonarQube (hạ tầng nền của 013)

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec 012 (`012-sonarqube-quality-gate`) dựng hạ tầng SonarQube + Jenkins; spec 013 (`013-sonarqube-merge-blocker`) thêm cơ chế chặn merge lên trên nó. Thư mục `specs/012-…` đã bị xoá và **không có** tài liệu
architecture/development/PO cho 012 — nguồn duy nhất còn lại là [`docs/spec-summary-vi/012-sonarqube-quality-gate.json`](../spec-summary-vi/012-sonarqube-quality-gate.json) (FR-001…010, US1–US3, SC-001…005) cùng mã CI (`Jenkinsfile`, `scripts/ci/`,
`docker-compose.ci.yml`, `sonar-scanner.properties`). Phần chặn merge phía GitHub được QA ở [013](013_QA_cổng%20chất%20lượng%20CI.md).

## Luồng happy-case đã rà soát

1. Pipeline chạy tuần tự build → unit → integration → contract → **cổng chất lượng SonarQube** (FR-001), dừng trước SonarQube nếu chặng trước lỗi (FR-002).
2. SonarQube tính **1 trạng thái cổng duy nhất** cho mỗi lượt phân tích từ 1 quality gate quản lý tập trung (FR-003, FR-009); chỉ số coverage/trùng lặp/code smell hiển thị (FR-006).
3. Không kết nối được hoặc quá hạn chờ SonarQube = **không đạt**, không phải bỏ qua (FR-008); mỗi commit mới chạy lại toàn chuỗi (FR-007).
4. Kết quả từng chặng được lưu để kiểm toán (FR-010).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — bật/tắt SonarQube cục bộ rồi bấm Postman

Công tắc là chính stack CI (`docker-compose.ci.yml`, dữ liệu cũ còn trong volume `ecomerce-ci_sonarqube-data`):

```bash
docker compose -f docker-compose.ci.yml up -d --no-build sonarqube   # BẬT (đợi healthy, ~1 phút)
docker compose -f docker-compose.ci.yml stop sonarqube               # TẮT
```

Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**, dán nội dung file `.ci-secrets/sonarqube-analysis-token` (gitignored) vào biến `sonarToken`,
rồi chạy folder **`12 - Cổng chất lượng SonarQube`** (bước 01 → 07, chỉ đọc).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| SonarQube chạy, token hợp lệ | BẬT SonarQube (lệnh trên) | `12` bước 01, 02 | `status: UP`, `valid: true` | Đúng (SonarQube 26.8, token 44 ký tự còn hợp lệ) |
| FR-009 — 1 quality gate quản lý tập trung | (không cần) | `12` bước 03 | 1 gate, điều kiện trên phần mã mới | Đúng: chỉ có gate built-in **Sonar way** (mặc định, không sửa được): `new_violations > 0`, `new_coverage < 80`, `new_duplicated_lines_density > 3`, `new_security_hotspots_reviewed < 100` |
| FR-003 — 1 trạng thái đạt/không đạt | (không cần) | `12` bước 04 | `OK` hoặc `ERROR` | `OK` cho `master` |
| FR-006 — số đo chất lượng | (không cần) | `12` bước 05 | coverage, trùng lặp, code smell là số | Đúng: coverage **78.8 %**, trùng lặp **7.9 %**, **47** code smell, 6 bug, 1 lỗ hổng, 3342 dòng (toàn project, không phải riêng phần mã mới) |
| US1/US2 — PR được gắn trạng thái cổng | (không cần) | `12` bước 06 | PR có `qualityGateStatus` | Đúng: **10 PR** đã phân tích, cả 10 `OK`; PR mới nhất phân tích **2026-09-01** |
| Nhánh chính, độ mới của phân tích | (không cần) | `12` bước 07 | `master` là nhánh chính, phân tích không cũ | `master` là nhánh chính; **đỏ**: lần phân tích gần nhất **2026-09-01 (~25 ngày trước)** — CI đã ngừng đẩy phân tích (xem QA_Debt) |
| FR-008 — không kết nối được SonarQube thì bị chặn | TẮT SonarQube, chạy `scripts/ci/sonar-begin.sh` với `SONAR_HOST_URL`/`SONAR_TOKEN` | `12` bước 01 (đỏ khi tắt) | Chặng thất bại, không bỏ qua | `sonar-begin.sh` thoát **mã 127**: "Unable to connect to server … http://localhost:9000/api/settings/values" (ném `HttpRequestException` — pipeline dừng ở chặng `sonarqube: begin analysis`, đăng check `ci/sonarqube-quality-gate` = failure). Trên Git Bash Windows phải đặt `MSYS_NO_PATHCONV=1`, nếu không tham số `/k:ecommerce` bị đổi thành đường dẫn |
| FR-001/FR-002 — chuỗi 5 chặng thật sự chạy | `CI_FAST_ITERATION` trong `Jenkinsfile` (`'false'` = chạy thật) | (không có) — đọc `Jenkinsfile`, lịch sử build trong volume `jenkins-data` | Cả 5 chặng chạy | **`CI_FAST_ITERATION = 'true'` đang được commit**: các chặng `sonarqube: begin analysis`, `integration tests`, `contract tests`, `sonarqube quality gate` bị bỏ qua (`skipped due to when conditional` trong log build 09-02 → 09-06); không có stub nào đăng check thay thế — xem QA_Debt |
| FR-004/FR-005 — chặn merge phía GitHub | (cần PAT GitHub mới) | (không có) — xem QA 013 | Không đường vòng | Không kiểm lại được (PAT hết hạn); trạng thái hiện tại xem [013](013_QA_cổng%20chất%20lượng%20CI.md) |
| Dọn dẹp | `docker compose -f docker-compose.ci.yml stop sonarqube jenkins`; xoá thư mục `.sonarqube/` nếu chạy `sonar-begin.sh` | (không có) | Không dữ liệu dư | Đã dừng Jenkins/SonarQube, xoá `.sonarqube/`; không đẩy phân tích mới lên SonarQube |

### Tự động

Không có test C#/TS cho spec này (thuần cấu hình CI). Kiểm tra tĩnh đã làm bằng cách đọc file:

| Cần xác nhận | File (bấm để mở) | Kết quả |
|---|---|---|
| Thứ tự chặng, `waitForQualityGate` có timeout 15 phút và mọi trạng thái khác `OK` đều thành lỗi (FR-008) | [`Jenkinsfile`](../../Jenkinsfile) | Đúng như tài liệu (chặng `sonarqube quality gate` đóng-khi-lỗi) |
| Cấu hình scanner là 1 nguồn duy nhất, dịch sang tham số `/d:` | [`sonar-scanner.properties`](../../sonar-scanner.properties), [`scripts/ci/sonar-begin.sh`](../../scripts/ci/sonar-begin.sh) | Đúng; project `ecommerce` (`Ecommerce Platform`) |
| Java agent của Community Branch Plugin ở cả `web` và `ce`, mount `docker.sock` | [`docker-compose.ci.yml`](../../docker-compose.ci.yml) | Đúng — SonarQube khởi động healthy với dữ liệu cũ |
| Jenkins tự nối SonarQube bằng init script | [`docker/ci/jenkins-init/10-sonarqube-server.groovy`](../../docker/ci/jenkins-init/10-sonarqube-server.groovy) | Đúng (Jenkins healthy; cần đăng nhập mới xem được UI — anonymous trả `403`) |

## Kết luận

**PASS kèm ghi chú nghiêm trọng (về cấu hình CI, không phải về SonarQube).** SonarQube cục bộ chạy đúng thiết kế: 1 gate tập trung, trạng thái `OK`/`ERROR` duy nhất, số đo và trạng thái từng PR truy vấn được, và mất kết nối thì chặng bị lỗi (fail closed). Ghi chú: (1) `Jenkinsfile` đang commit
`CI_FAST_ITERATION = 'true'` nên 4 chặng (SonarQube, integration, contract, deployment lint) bị bỏ qua và không có check thay thế — ngay cả khi CI chạy, cổng chất lượng không đo gì; (2) phân tích mới nhất của `master` là 2026-09-01 (CI ngừng); (3) không có tài liệu architecture/development/PO cho 012,
nguồn chỉ còn bản tóm tắt; (4) `sonar-begin.sh` hỏng trên Git Bash Windows nếu thiếu `MSYS_NO_PATHCONV=1`. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 012.
