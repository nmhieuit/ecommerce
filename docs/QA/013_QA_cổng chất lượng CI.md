# QA: Cổng chất lượng SonarQube làm rào chặn merge

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Khác mọi spec trước: đây là cấu hình CI/CD thật (Jenkins + SonarQube + GitHub branch protection), đã được xác minh
bằng PR thật lúc viết feature (2026-08-27 → 09-01, `tasks.md` T001–T018). QA ở đây kiểm tra cơ chế đó **có còn hoạt động thật hôm nay hay không**,
bằng API công khai của GitHub (Postman, không cần token) và bằng Jenkins/SonarQube cục bộ dựng lại từ volume cũ. Hạ tầng SonarQube nền được QA ở [012](012_QA_cổng%20chất%20lượng%20SonarQube.md).

## Luồng happy-case đã rà soát (theo thiết kế, có bằng chứng PR thật lúc viết feature)

1. Mọi PR nhắm `master` tự kích hoạt các stage tuần tự `ci/build` → `ci/unit-tests` → `ci/integration-tests` → `ci/contract-tests` → `ci/sonarqube-quality-gate` (sau spec 018 thêm `ci/secret-scan`, `ci/image-secret-scan` — tổng 7 check bắt buộc).
2. Cổng chất lượng thất bại thì PR bị chặn merge, không có đường vòng cho bất kỳ vai trò nào (`enforce_admins: true`).
3. Chỉ số chất lượng (coverage/duplication/code smell) hiển thị ngay trên PR qua comment của SonarQube Community Branch Plugin.
4. Sửa xong, push lại thì cả chuỗi tự chạy lại và PR tự mở khoá.
5. Audit "ai đổi cấu hình chặn merge, cổng chất lượng ra sao lúc merge" trả lời được bằng security log + `commits/{sha}/status` của GitHub.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — kiểm tra tình trạng chặn merge hiện tại bằng Postman + CI cục bộ

Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**, chạy folder
**`13 - Chặn merge vào master (GitHub)`** (bước 01 → 07; gọi API công khai của GitHub, biến `githubToken` chỉ cần cho bước 07).
CI cục bộ (công tắc = compose CI): `docker compose -f docker-compose.ci.yml up -d --no-build` (BẬT) / `… stop jenkins sonarqube` (TẮT).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng nếu cổng đang hoạt động | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| Repo công khai, `master` được bảo vệ | (không cần) | `13` bước 01, 02 | `private: false`, `default_branch: master`, `protected: true` | Đúng (xanh) — nhưng cờ `protected` gây hiểu nhầm: rule tồn tại mà không chặn gì |
| SC-002 — rule đang chặn merge `master` | (không cần) | `13` bước 03 | Rule `required_status_checks` với 7 context | **Đỏ**: `rules/branches/master` = `[]`, không rule nào đang áp dụng |
| SC-001 — commit master mới nhất có check CI | (không cần) | `13` bước 04 | `total_count: 7` | **Đỏ**: `total_count: 0`, `state: pending` |
| SC-001/SC-002 — PR vừa merge có check | (không cần) | `13` bước 05 → 06 | Commit merge của PR mới nhất có ≥ 1 check | **Đỏ**: 5 PR gần nhất (#48 → #52, cùng ngày 2026-09-26) đều đã merge, commit merge của PR mới nhất có 0 check |
| Token CI đọc được cấu hình protection | Điền `githubToken` = nội dung `.ci-secrets/github-pat` | `13` bước 07 | `200`, `enforce_admins: true` | **Đỏ**: `401 Bad credentials` — PAT hết hạn/vô hiệu |
| Jenkins cục bộ quét được GitHub | BẬT Jenkins (compose CI), đợi healthy | (không có) — xem `jobs/ecommerce/indexing/indexing.log` trong volume | Quét nhánh/PR thành công | **Thất bại**: `"message": "Bad credentials", "status": "401"` … `Finished: FAILURE`; log build cuối (2026-09-06) có `IllegalArgumentException: The supplied credentials are invalid to login` ở `githubNotify` — Jenkins không đăng được check nào |
| Lịch sử build còn lại | BẬT Jenkins | (không có) — đọc `jobs/ecommerce/branches/*/builds/*` | Build đều đăng check | Build cuối **2026-09-06**; các chặng `sonarqube: begin analysis` bị bỏ qua (`skipped due to when conditional`) vì `CI_FAST_ITERATION = 'true'` đang commit (xem [012](012_QA_cổng%20chất%20lượng%20SonarQube.md), QA_Debt) |
| SonarQube cục bộ | BẬT SonarQube (compose CI) | `12` bước 01 → 07 (xem doc 012) | Chạy, có số đo và trạng thái PR | Chạy; lần phân tích cuối của `master` 2026-09-01, 10 PR đều `OK` |
| Dọn dẹp | `docker compose -f docker-compose.ci.yml stop jenkins sonarqube` | (không có) | Không dữ liệu dư | Đã dừng cả hai; không đổi gì trên GitHub/SonarQube |

### Tự động — đọc file cấu hình (không có test C#/TS cho spec này, thuần cấu hình CI)

| Cần xác nhận | File (bấm để mở) | Kết quả |
|---|---|---|
| Các stage đúng thứ tự, `githubNotify` cho từng check; **`CI_FAST_ITERATION = 'true'` đang commit** | [`Jenkinsfile`](../../Jenkinsfile) | Thứ tự đúng; nhưng cờ `'true'` làm 4 chặng bị bỏ qua và không có check thay thế (xem QA_Debt 012) |
| `enforce_admins: true`, `required_approving_review_count: 0`, **7** context bắt buộc | [`scripts/ci/setup-branch-protection.sh`](../../scripts/ci/setup-branch-protection.sh) | Đúng như thiết kế nhưng tài liệu vẫn nói 5 context; script chưa được chạy lại sau spec 018 |
| Java agent Community Branch Plugin ở cả 2 biến, mount `docker.sock`, `TESTCONTAINERS_*` | [`docker-compose.ci.yml`](../../docker-compose.ci.yml) | Đúng — SonarQube/Jenkins khởi động healthy từ dữ liệu cũ |
| Dockerfile Jenkins agent riêng (.NET/Node/Docker CLI, `COREPACK_HOME`) | [`docker/ci/jenkins.Dockerfile`](../../docker/ci/jenkins.Dockerfile) | Đúng |

**Kết quả lượt QA này**: các file cấu hình khớp mô tả của `architecture/013` và `development/013` ở tầng tĩnh (trừ số lượng context: 7, không phải 5).

## Kết luận

**FAIL.** Cấu hình tĩnh đúng tài liệu và cơ chế từng hoạt động đúng lúc viết feature, nhưng hôm nay **cơ chế chặn merge — giá trị cốt lõi duy nhất của 013 — không còn hoạt động**: `rules/branches/master` trả `[]`, commit `master` mới nhất có 0 check,
PR #48 → #52 (cùng ngày) merge bình thường, và Jenkins không đăng/quét được vì PAT GitHub đã vô hiệu (`Bad credentials`) — phủ định FR-003/SC-002. Ghi chú: (1) `CI_FAST_ITERATION = 'true'` đang commit nên 3 trong 7 check không bao giờ được đăng;
(2) số check bắt buộc là 7 (không phải 5) và script áp protection chưa được chạy lại sau spec 018; (3) runbook `docs/github-jenkins-sonarqube-setup.md` bị xoá nhầm ở commit `8fcbbdf`. Chi tiết, bằng chứng và hướng xử lý: [QA_Debt.md](QA_Debt.md) mục 012 và 013.
