# Hợp đồng: Một lần chạy `tests/CriticalPathLoadTests`

"Giao diện bên ngoài" của tính năng này không phải một HTTP API — mà là tập hợp bất biến mà bất kỳ ai
chạy hoặc bảo trì bài kiểm thử tải phải tin tưởng được, dù chạy tay hay chạy theo lịch. Vi phạm bất
kỳ bất biến nào dưới đây là một lỗi của chính bài kiểm thử tải, không phải của hệ thống đang được đo.

## Bất biến

1. **Ngưỡng luôn đọc từ manifest, không hard-code**: mọi ngưỡng p95/p99 dùng để so sánh PHẢI được đọc
   tại thời điểm chạy từ `services/bff/src/Bff.Api/service-manifest.yaml`, qua
   `ServiceManifestSloConventionTests.ServiceManifestFixture` — không có bản sao số liệu thứ hai
   trong mã của `CriticalPathLoadTests` (research.md Quyết định 0). Đổi ngân sách trong manifest phải
   tự động phản ánh vào lần chạy kế tiếp mà không cần sửa mã kiểm thử tải.

2. **4 bước, đúng thứ tự thật**: mỗi lần chạy PHẢI thực hiện đúng trình tự `GET /bff/products` →
   `POST /bff/basket/items` → `POST /bff/checkout` → `GET /bff/orders/{orderId}`, qua gateway — không
   được rút gọn hay đổi thứ tự, vì đây là chính luồng nghiệp vụ trọng yếu mà FR-001 mô tả.

3. **Một bước vượt ngưỡng khiến toàn bộ lần chạy Fail**: nếu P95 HOẶC P99 đo được của BẤT KỲ bước nào
   vượt ngưỡng tương ứng, trạng thái tổng thể của lần chạy PHẢI là `Fail` (FR-004) — không có "cảnh
   báo" trung gian.

4. **Mã thoát phản ánh đúng trạng thái**: lần chạy `Fail` PHẢI khiến tiến trình kết thúc với mã thoát
   khác 0 (để `dotnet test` báo thất bại), và lần chạy `Pass` PHẢI kết thúc với mã thoát 0 — đây là
   cơ chế duy nhất mà pipeline CI dùng để quyết định thành/bại (FR-004, FR-008).

5. **Mỗi lần chạy để lại một báo cáo có thể tra cứu lại**: kết quả (P95/P99 đo được, ngưỡng, trạng
   thái từng bước, trạng thái tổng thể, thời điểm chạy) PHẢI được ghi ra một tệp mới dưới
   `artifacts/performance/`, tên tệp chứa timestamp — làm mốc nền cho lần chạy sau tra cứu lại
   (FR-005, SC-001). Ghi đè lên báo cáo của lần chạy trước là một vi phạm.

6. **Không đổi hành vi hệ thống đang được đo**: bài kiểm thử tải chỉ được gọi các endpoint công khai
   hiện có, với dữ liệu hợp lệ như một shopper thật — không được thêm cờ, route, hay nhánh mã đặc
   biệt "chỉ dành cho kiểm thử tải" vào BFF hay bất kỳ service nào (FR-010).

## Ai dùng hợp đồng này

Người viết `CriticalPathLoadTests` (tasks.md) và người viết `scripts/ci/run-performance-tests.sh` /
`Jenkinsfile.performance` — cả hai phía phải giữ đúng 6 bất biến trên. Đổi cách đọc ngưỡng (bất biến
1) hoặc cách quyết định pass/fail (bất biến 3–4) mà không cập nhật tài liệu này là một vi phạm hợp
đồng, kể cả khi mã vẫn chạy được.
