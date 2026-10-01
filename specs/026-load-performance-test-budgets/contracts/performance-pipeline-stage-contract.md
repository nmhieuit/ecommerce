# Hợp đồng: Pipeline hiệu năng theo lịch ↔ Pipeline PR gate hiện có

"Giao diện bên ngoài" của phần tự động hoá trong tính năng này là ranh giới giữa `Jenkinsfile` (chặn
PR, specs/013) và pipeline hiệu năng mới — hai pipeline PHẢI không giẫm chân nhau. Vi phạm ranh giới
này (ví dụ để dự án kiểm thử tải lọt vào tier chặn PR) là một hồi quy cho cổng chặn PR hiện có, không
chỉ là một lỗi của tính năng này.

## 1. Tier "performance" độc lập với 3 tier hiện có của `run-dotnet-tests.sh`

| Tier | Chạy khi nào | Cần stack sống? | Dự án khớp |
|---|---|---|---|
| `unit` (đã có) | Mọi PR (`Jenkinsfile`, stage "unit tests") | Không | Mọi `*Tests.csproj` KHÔNG khớp `ContractTests`/`IntegrationTest`/`CriticalPathLoadTests` |
| `integration` (đã có) | Mọi PR khi `CI_FAST_ITERATION=false` | Có (Testcontainers) | `*IntegrationTest*.csproj` |
| `contract` (đã có) | Mọi PR khi `CI_FAST_ITERATION=false` | Không | `*ContractTests.csproj` |
| `performance` (mới) | Theo lịch, KHÔNG chạy trên PR | Có (toàn bộ stack qua `docker-compose.demo.yml`) | `tests/CriticalPathLoadTests/CriticalPathLoadTests.csproj` |

**Bất biến bắt buộc**: điều kiện lọc của tier `unit` trong `scripts/ci/run-dotnet-tests.sh` PHẢI loại
trừ tường minh `CriticalPathLoadTests` (ví dụ thêm `grep -v 'CriticalPathLoadTests'` cạnh
`grep -v 'IntegrationTest'` hiện có). Thiếu điều kiện loại trừ này khiến dự án kiểm thử tải tự động
lọt vào tier `unit` — chạy trên MỌI PR mà không có stack sống, dẫn đến thất bại hàng loạt hoặc phải
âm thầm bỏ qua assertion, cả hai đều là vi phạm nghiêm trọng hơn việc không có tính năng này.

## 2. Tên check và lịch chạy (Jenkins)

| Mục | Giá trị |
|---|---|
| Tệp pipeline | `Jenkinsfile.performance` (tách biệt khỏi `Jenkinsfile` chặn PR) |
| Tên check đăng lên GitHub | `ci/performance-gate` |
| Kích hoạt | Theo lịch (cron, ví dụ hàng đêm) — KHÔNG đăng ký vào danh sách required status checks của branch protection mà specs/013 đang quản lý |
| Môi trường mục tiêu | `docker-compose.demo.yml` (giống production, đã dùng cho demo end-to-end của hạng mục 006) |
| Hành động khi Fail | Đăng trạng thái `FAILURE` cho check `ci/performance-gate`; PHẢI có khả năng chặn phát hành (FR-008) qua cùng cơ chế mà quy trình phát hành hiện tại đã dùng để đọc trạng thái Jenkins — cơ chế wiring cụ thể vào bước phát hành là chi tiết triển khai, thuộc `tasks.md` |

## 3. Không thay đổi hợp đồng 5-stage hiện có

`Jenkinsfile.performance` là một pipeline hoàn toàn mới, KHÔNG thêm stage vào `Jenkinsfile` hiện có
và KHÔNG đổi bất kỳ tên check nào trong bảng ở
`specs/013-sonarqube-merge-blocker/contracts/pipeline-stage-contract.md` §1. Người bảo trì
`Jenkinsfile.performance` không cần (và không được phép) sửa `scripts/ci/setup-branch-protection.sh`
cho tính năng này.

## Ai dùng hợp đồng này

Người viết `scripts/ci/run-performance-tests.sh`, `Jenkinsfile.performance`, và người sửa
`scripts/ci/run-dotnet-tests.sh` (tasks.md) — cả ba phải giữ đúng ranh giới trên. Người cấu hình lịch
Jenkins (cron) tham chiếu mục 2 để đặt đúng tên check và không đăng ký nhầm vào required status
checks của PR gate.
