# ADR-0004: Sinh client TypeScript từ OpenAPI (Codegen)

*(Bản dịch tiếng Việt của [`0004-openapi-client-codegen.md`](0004-openapi-client-codegen.md) — bản
gốc tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Principle II yêu cầu client API phải được **sinh ra**, không bao giờ viết tay. Principle IX yêu cầu
trạng thái phía server được quản lý bằng TanStack Query, và gọi "các UI primitive trùng lặp hoặc lời
gọi API viết tay giữa các app" là 1 vi phạm.

## Quyết định

Dùng **Orval**.

## Các phương án đã cân nhắc

### Phương án A: Orval
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp-Trung bình | **Chi phí** | Miễn phí (OSS) | **Khả năng mở rộng** | N/A | **Độ quen thuộc** | Trung bình |

**Ưu điểm:** Phương án DUY NHẤT ở đây sinh trực tiếp TanStack Query hook (`useQuery`/`useMutation`) từ
đặc tả OpenAPI — không cần code kết nối viết tay nào giữa client sinh ra và tầng lấy dữ liệu bắt buộc,
đúng chính xác điều Principle IX muốn loại bỏ.
**Nhược điểm:** Cộng đồng bảo trì nhỏ hơn openapi-typescript; đầu ra mang tính áp đặt, gắn chặt vào
TanStack Query (chấp nhận được vì đó vốn đã là lựa chọn hiến pháp); schema OpenAPI phức tạp/đa hình có
thể cần cấu hình thêm.

### Phương án B: openapi-typescript (+ openapi-fetch)
**Ưu điểm:** Rất phổ biến, ít "phép màu", đầu ra có kiểu đầy đủ; được bảo trì rất tốt.
**Nhược điểm:** Chỉ sinh type + 1 wrapper fetch mỏng — không có TanStack Query hook, nên mỗi call site
cần 1 wrapper `useQuery` viết tay, tái tạo lại đúng mẫu hình lời-gọi-API-viết-tay mà Principle IX cấm.

### Phương án C: Kiota
**Ưu điểm:** Do Microsoft duy trì, đa ngôn ngữ (có thể sinh cả client TS lẫn 1 client C# non-BFF tương
lai từ cùng 1 spec); tín hiệu hỗ trợ dài hạn mạnh.
**Nhược điểm:** Không nhận biết TanStack Query; client kiểu builder-pattern sinh ra dài dòng hơn và ít
tự nhiên trong React hơn hook của Orval; mức áp dụng hạn chế trong hệ sinh thái React.

### Phương án D: NSwag
**Ưu điểm:** Trưởng thành, theo lịch sử phổ biến trong các đội .NET cho codegen full-stack.
**Nhược điểm:** Cách viết TS sinh ra lạc hậu hơn so với các công cụ tập trung vào React; không sinh
native TanStack Query hook; đà phát triển đã chậm lại so với Orval.

## Phân tích đánh đổi

Yếu tố quyết định là yêu cầu bắt buộc TanStack Query của Principle IX — Orval là ứng viên duy nhất
đóng được khoảng cách giữa "client sinh ra" và "TanStack Query hook" mà không cần code wrapper viết
tay ở mọi call site — đúng chính loại trùng lặp mà constitution gắn cờ là vi phạm.

## Hệ quả

- Code lấy dữ liệu phía frontend được sinh lại mỗi khi đặc tả OpenAPI đổi — CI phải tự sinh lại và làm
  fail build nếu có sai lệch giữa spec và đầu ra đã sinh được commit.
- Cả app web lẫn mobile-web dùng chung đúng 1 package hook đã sinh, thực thi quy tắc "1 client API
  sinh ra duy nhất" bằng cấu trúc, không phải bằng quy ước.

## Việc cần làm

1. [ ] Thêm Orval vào frontend monorepo, cấu hình trỏ vào đầu ra OpenAPI của BFF
2. [ ] Thêm 1 kiểm tra CI làm fail build nếu đầu ra đã sinh bị lỗi thời so với đặc tả OpenAPI
