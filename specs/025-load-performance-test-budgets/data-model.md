# Data Model: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu

**Feature**: [spec.md](./spec.md) | Tham chiếu: [research.md](./research.md), [plan.md](./plan.md)

Không có thực thể nghiệp vụ mới nào được tạo ra hay lưu trữ lâu dài — đây là các thực thể mô tả cấu
trúc dữ liệu nội bộ của chính bài kiểm thử tải (input đọc từ manifest có sẵn, output là báo cáo mỗi
lần chạy).

## Bước của luồng trọng yếu (Critical Path Step)

Một bước cố định trong chuỗi browse→basket→checkout→order. Có đúng 4 bước, theo thứ tự thực thi thật.

| Trường | Mô tả |
|---|---|
| Tên bước | Ví dụ: "Duyệt sản phẩm", "Thêm vào giỏ hàng", "Thanh toán", "Xem đơn hàng" |
| Route BFF | Phương thức + đường dẫn thật, ví dụ `GET /bff/products` |
| Nhóm ngân sách | Luôn là `client-facing-bff` — cả 4 route đều thuộc BFF (research.md Quyết định 2) |
| Ngưỡng P95 tham chiếu | Đọc trực tiếp từ `services/bff/src/Bff.Api/service-manifest.yaml` tại thời điểm chạy — không hard-code (research.md Quyết định 0) |
| Ngưỡng P99 tham chiếu | Như trên |

4 bước cụ thể (khớp mã nguồn hiện có, xem plan.md Summary):

1. `GET /bff/products` — duyệt sản phẩm.
2. `POST /bff/basket/items` — thêm sản phẩm vào giỏ hàng.
3. `POST /bff/checkout` — thanh toán, tạo đơn hàng.
4. `GET /bff/orders/{orderId}` — xem lại xác nhận đơn hàng vừa tạo.

## Kết quả một bước (Step Result)

Sinh ra sau mỗi lần chạy, một bản ghi cho mỗi Bước của luồng trọng yếu.

| Trường | Mô tả |
|---|---|
| Tên bước | Tham chiếu Bước của luồng trọng yếu |
| P95 đo được | Từ số liệu NBomber của lần chạy này |
| P99 đo được | Từ số liệu NBomber của lần chạy này |
| Ngưỡng P95 / P99 | Sao chép từ Bước của luồng trọng yếu tại thời điểm chạy (để báo cáo tự chứa, không cần tra lại manifest sau này) |
| Trạng thái | `Pass` khi cả P95 và P99 đo được ≤ ngưỡng tương ứng; `Fail` khi vượt bất kỳ ngưỡng nào (FR-004) |

## Kết quả một lần chạy (Load Test Run Result)

Thực thể cấp cao nhất — một lần thực thi `tests/CriticalPathLoadTests`.

| Trường | Mô tả |
|---|---|
| Thời điểm chạy | UTC timestamp lúc bắt đầu |
| Môi trường | Ví dụ `docker-compose.demo.yml` (giống production) hoặc môi trường cục bộ khi chạy tay |
| Danh sách Kết quả bước | 4 bản ghi, một cho mỗi Bước của luồng trọng yếu |
| Trạng thái tổng thể | `Fail` nếu bất kỳ Kết quả bước nào `Fail`; `Pass` khi cả 4 đều `Pass` (FR-004) |
| Đường dẫn báo cáo | Tệp lưu dưới `artifacts/performance/`, tên có timestamp — làm mốc nền để tra cứu lại (FR-005, SC-001) |

Không có quan hệ nào cần một cơ sở dữ liệu: mỗi lần chạy là một tệp báo cáo độc lập; "mốc nền" (SC-001)
là lần chạy trước đó, tra bằng cách mở lại tệp báo cáo tương ứng trong `artifacts/performance/` —
không có bảng lưu trữ, không có service mới.
