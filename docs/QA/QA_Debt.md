# QA Debt — phát hiện và giới hạn khi rà soát chất lượng happy-case (001-026)

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử. File này gom mọi phát hiện (tài liệu sai lệch với code thật,
narrative thiếu bước cho 1 user story, thiếu bằng chứng định lượng cho tiêu chí nghiệm thu...) khi rà
soát chéo `architecture`/`development`/`summary`/`spec-summary-vi` cho từng spec — để mỗi file
`0NN_QA_*.md` chỉ giữ kết luận ngắn gọn (PASS/PASS kèm ghi chú/FAIL) và trỏ về đây. Không có nội dung
nào bị bỏ — chỉ gom lại 1 chỗ.*

## 001 — Dựng khung 4 dịch vụ

- **Tài liệu sai lệch với code thật**: `architecture/001` mô tả readiness dùng `AddDbContextCheck<T>()`
  để kiểm tra database. Code hiện tại (`services/products/src/Products.Api/Features/HealthCheck/HealthCheckEndpoints.cs`)
  đã đổi sang `.AddSqlServer(...)` (mở kết nối SQL thô) từ khi **003-stub-identity-tenant-context** gate
  `DbContext` theo tenant — health probe của Kubernetes gọi thẳng `/health/ready`, không qua gateway,
  nên không có tenant để dựng `DbContext`. Đây là thay đổi xuyên-spec chưa từng được ghi Amendment ở
  `architecture/001` lẫn `architecture/003`. **Không ảnh hưởng kết quả happy-case** (readiness vẫn đúng
  chức năng — phản ánh thật khả năng kết nối database), chỉ sai phần mô tả cơ chế kỹ thuật cụ thể.
- **PO thiếu 1 bước trải nghiệm cho US2**: mục "Trải nghiệm thực tế" của `summary/001` chỉ có 3 bước,
  ánh xạ tới US1 (bước 1-2) và US3 (bước 3) — US2 (cách ly dữ liệu chéo) không có bước trải nghiệm
  riêng, chỉ được nhắc ở đoạn giới thiệu giải pháp và ở `functional-debt.md`.
- **Thiếu bằng chứng định lượng cho SC-001/002/004**: `architecture`/`development` không trích số đo
  cụ thể (thời gian khởi động, số lần chạy khoẻ, ví dụ cấu trúc thư mục) — khác các spec sau (005, 006)
  đã có số liệu thật. Không phải lỗi, chỉ là đặc điểm của spec sớm trong dự án.

## 002 — Định tuyến gateway-BFF

- **Tài liệu sai lệch với cổng thật**: [`specs/002-gateway-bff-routing/quickstart.md`](../../specs/002-gateway-bff-routing/quickstart.md)
  (dòng "Note the local ports") ghi baskets = `5041`, orders = `5188`. Thực tế
  (`docker-compose.local.yml`, `postman/local.postman_environment.v2.json`, và đã xác nhận lại ở QA
  001) là **ngược lại**: baskets-api publish `5188`, orders-api publish `5041`. Không ảnh hưởng happy-
  case (Postman collection dùng biến `{{basketsUrl}}`/`{{ordersUrl}}` nên không bị lỗi cổng), chỉ gây
  nhầm nếu ai đó gọi `curl` tay theo đúng số quickstart ghi.
- **2 request CORS preflight trong folder Postman `Gateway`** ("CORS preflight từ storefront/origin
  lạ") không thuộc phạm vi happy-case của spec 002 — `specs/002-gateway-bff-routing/spec.md` không có
  FR/SC nào về CORS; theo chú thích trong `docker-compose.yml` (`Cors__AllowedOrigins__0`), CORS được
  thêm bởi spec 004 (SPA storefront) sau khi phát hiện lỗi thật lúc walkthrough. Cả 2 request này nên
  được rà soát ở QA của spec 004, không phải ở đây — chỉ ghi chú để không bị hiểu nhầm là thiếu sót của
  002.
- **Bảng "Tự động" (bản trước khi sửa) gán nhầm 1 file test của spec 016 cho spec 002**:
  `services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` bị liệt kê chung với
  `Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs` như thể cả 2 cùng là regression test
  cho bug correlation-ID của 002. Đã xác minh qua `git log --follow`: file BFF được tạo ở commit
  `2abe34d` ("feat(correlation-id): implement correlation ID propagation from edge to frontend",
  2026-09-06) — chính là **016-correlation-id-propagation** — muộn hơn spec 002 (hoàn tất 2026-08-15)
  gần 3 tuần; bản thân class đó cũng tự chú thích "016-correlation-id-propagation spec US1/US3". File
  Gateway (`CorrelationIdPropagationTests.cs`) mới thật sự là của spec 002 (commit `c9db644`, cùng ngày
  với phần còn lại của 002, và được `tasks.md` của 002 ghi nhận trong "Phase 6 implementation notes").
  Đã sửa bảng "Tự động" của `002_QA` để chỉ còn trỏ tới file Gateway; việc rà soát `CorrelationPropagationTests.cs`
  (bff) để dành cho QA của spec 016.
