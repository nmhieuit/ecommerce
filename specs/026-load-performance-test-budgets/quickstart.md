# Quickstart: xác thực kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Hướng dẫn này xác thực đúng 3 kịch bản kiểm thử của Jira SCRUM-32: (1) chạy kiểm thử tải trên lát cắt
hiện tại và ghi lại số đo p95/p99 nền; (2) đưa vào một truy vấn CSDL chậm có chủ đích, xác nhận kiểm
thử thất bại; (3) sửa hồi quy và xác nhận kiểm thử thành công trở lại. Bước 1–3 chạy tay hoặc theo
lịch — KHÔNG phải một phần của cổng chặn PR hiện có (`Jenkinsfile`), đúng như hợp đồng
[performance-pipeline-stage-contract.md](./contracts/performance-pipeline-stage-contract.md).

> **Trạng thái đã biết (xem research.md Quyết định 6)**: trên một stack `docker-compose.yml` +
> `docker-compose.demo.yml` mới dựng, Bước 1 hiện **FAIL ở `GET /bff/products` với 401** — không phải
> lỗi của bài kiểm thử tải, mà là một khoảng trống thật của nền tảng: không có cách nào để một client
> HTTP bên ngoài lấy được bearer token thật hôm nay (gateway ở chế độ stub không chuyển tiếp token
> xuống BFF; BFF luôn đòi JWT thật; không có tài khoản demo/UI đăng nhập/endpoint tự đăng ký). Đây là
> bằng chứng cho US2 (một lỗi thật khiến lần chạy thất bại rõ ràng — đúng thiết kế), không phải bằng
> chứng US1 chạy thành công. Đã tạo task riêng theo dõi khoảng trống xác thực này; Bước 1 chỉ cho ra
> baseline thật khi task đó được giải quyết.

## Chuẩn bị

```bash
cp .env.example .env          # nếu chưa có
./scripts/demo.ps1            # hoặc demo.sh — khởi động toàn bộ stack ở chế độ giống production
```

## Bước 1 — Chạy kiểm thử tải trên lát cắt hiện tại, ghi nhận baseline (Test Scenario 1)

```bash
scripts/ci/run-performance-tests.sh
```

**Kỳ vọng**: mã thoát 0. Một báo cáo mới xuất hiện dưới `artifacts/performance/`, liệt kê P95/P99 đo
được cho cả 4 bước (`GET /bff/products`, `POST /bff/basket/items`, `POST /bff/checkout`,
`GET /bff/orders/{orderId}`) cùng ngưỡng tham chiếu đọc từ
`services/bff/src/Bff.Api/service-manifest.yaml`, và trạng thái tổng thể `Pass` — đây là mốc nền
(baseline, FR-005/SC-001) cho các lần chạy sau.

**Xác nhận không hard-code ngưỡng**: mở báo cáo, đối chiếu cột "Ngưỡng" với đúng giá trị
`slos.latency.p95`/`p99` đang có trong `service-manifest.yaml` của `bff` — hai nơi phải khớp tuyệt
đối vì báo cáo đọc trực tiếp từ manifest (contracts/load-test-run-contract.md bất biến 1).

---

## Bước 2 — Đưa vào một hồi quy có chủ đích, xác nhận kiểm thử thất bại (Test Scenario 2)

Thêm tạm một độ trễ vào một handler đọc dữ liệu của service `baskets` hoặc `orders` (ví dụ
`await Task.Delay(TimeSpan.FromMilliseconds(600))` ngay trước khi truy vấn CSDL), mô phỏng một truy
vấn chậm — cùng kỹ thuật mà `specs/021-declare-service-slos/quickstart.md` Bước 4 đã dùng.

Build lại và chạy lại đúng lệnh ở Bước 1:

```bash
scripts/ci/run-performance-tests.sh
```

**Kỳ vọng**: mã thoát KHÁC 0. Báo cáo mới cho thấy bước tương ứng (ví dụ `POST /bff/basket/items`
nếu độ trễ được thêm vào `baskets`) có P95 hoặc P99 đo được vượt ngưỡng, và trạng thái tổng thể là
`Fail` — không phải "thành công có ghi chú" (spec FR-004, contracts/load-test-run-contract.md bất
biến 3–4).

---

## Bước 3 — Khắc phục hồi quy, xác nhận kiểm thử thành công trở lại (Test Scenario 3)

Gỡ bỏ `Task.Delay` vừa thêm ở Bước 2, build lại, và chạy lại đúng lệnh ở Bước 1 một lần nữa.

**Kỳ vọng**: mã thoát trở lại 0, trạng thái tổng thể `Pass` — chứng minh đây là một cổng chặn có thể
tin cậy (phát hiện đúng khi có vi phạm thật, không đúng khi không có), không phải một cổng luôn báo
cùng một kết quả bất kể tình trạng thật của hệ thống (spec FR-009, User Story 2 Acceptance Scenario
3).

---

## (Bổ sung) Đối chiếu chéo với ngân sách `internal-service-api` qua dashboard đã có

Theo research.md Quyết định 2, cổng chặn tự động (Bước 1–3) chỉ đo lớp `client-facing-bff`. Để đối
chiếu thêm với ngân sách `internal-service-api` của 4 service phía sau BFF trong cùng cửa sổ traffic
vừa tạo ở Bước 1, mở lại dashboard đã dựng ở
`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` (hạng mục 021) và xem số đo
p95/p99 của `baskets`/`orders`/`products` trong khoảng thời gian bài kiểm thử tải vừa chạy — đây là
một bước xác nhận bổ sung, không phải một phần của mã thoát pass/fail tự động.
