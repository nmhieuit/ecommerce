# ADR-0003: Mô hình triển khai BFF

*(Bản dịch tiếng Việt của [`0003-bff-implementation-pattern.md`](0003-bff-implementation-pattern.md)
— bản gốc tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn
đúng.)*

**Trạng thái:** Đã chấp thuận (Accepted) | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Principle II yêu cầu HTTP API được định nghĩa bằng OpenAPI với client được sinh ra (không viết tay).
Các ràng buộc nói BFF phải "tổng hợp thay vì tự cài đặt logic nghiệp vụ" và phục vụ 2 ứng dụng client
(web SPA, mobile-web) cho 2 tenant.

## Quyết định

Dùng **ASP.NET Core Minimal APIs**.

## Các phương án đã cân nhắc

### Phương án A: Minimal APIs
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp — handler mỏng, ít nghi thức |
| Chi phí | N/A |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Sinh tài liệu OpenAPI có sẵn, native (không cần dependency bên thứ 3 nào cho chính bản
hợp đồng); ít nghi thức trên mỗi endpoint, khớp đúng "tổng hợp thay vì tự cài đặt logic nghiệp vụ";
khởi động nhanh, dấu chân gọn — phù hợp cho 1 tầng thuần tổng hợp.
**Nhược điểm:** Quy ước filter/model-binding chưa trưởng thành bằng MVC cho các kịch bản validate rất
phức tạp; số lượng route lớn cần tổ chức có chủ đích (route group) để giữ dễ đọc.

### Phương án B: Controllers (MVC)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — nhiều boilerplate hơn |
| Chi phí | N/A |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Quy ước trưởng thành, được hiểu rõ; tích hợp Swashbuckle/OpenAPI đã qua kiểm chứng; quen
thuộc với số đông lập trình viên .NET nhất.
**Nhược điểm:** Nhiều boilerplate trên mỗi endpoint hơn mức 1 tầng thuần-tổng-hợp cần; bề mặt
DI/action-filter nặng hơn mức công việc của BFF đòi hỏi.

### Phương án C: GraphQL BFF (vd: HotChocolate)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Cao |
| Chi phí | N/A |
| Khả năng mở rộng | Trung bình — rủi ro resolver N+1 cần quản lý chủ động |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Mỗi client (web vs. mobile-web) có thể tự xin đúng hình dạng dữ liệu mình cần, giảm
over/under-fetching; mô hình resolver khớp tự nhiên với việc "ghép nhiều service downstream lại".
**Nhược điểm:** Mâu thuẫn trực tiếp với Principle II — "HTTP API được định nghĩa bằng OpenAPI";
GraphQL không sinh ra hợp đồng OpenAPI, nên sẽ cần 1 kỷ luật contract-first song song và tính là 1 sai
lệch hiến pháp cần ghi nhận; thêm độ phức tạp thật (vấn đề N+1, cache/rate-limiting tuỳ biến) không
tương xứng với 1 tầng vốn chỉ nên tổng hợp.

## Phân tích đánh đổi

GraphQL bị loại chủ yếu vì lý do hiến pháp, không phải giá trị kỹ thuật — nó sẽ cần hoặc tu chính
Principle II hoặc chấp nhận 1 sai lệch chưa từng được ghi nhận. Giữa Minimal APIs và Controllers,
Minimal APIs thắng vì là lựa chọn gọn hơn cho 1 tầng mà toàn bộ công việc là tổng hợp, không phải logic
nghiệp vụ, và việc sinh OpenAPI của nó giờ đã là tính năng hạng nhất trong .NET.

## Hệ quả

- Endpoint BFF được giữ mỏng có chủ đích; bất kỳ endpoint nào bắt đầu tích tụ logic nghiệp vụ thật là
  tín hiệu nó thuộc về 1 service, không phải BFF.
- Nếu độ linh hoạt hình dạng-theo-từng-client trở thành 1 điểm đau thật sự sau này (nhiều endpoint tuỳ
  biến riêng cho từng client), xem lại GraphQL như 1 tu chính có ghi nhận thay vì lách qua Principle II
  1 cách không chính thức.

## Việc cần làm

1. [ ] Thiết lập quy ước route-group để tổ chức endpoint BFF
2. [ ] Xác nhận đầu ra OpenAPI của Minimal API nạp gọn gàng vào công cụ codegen của ADR-0004
