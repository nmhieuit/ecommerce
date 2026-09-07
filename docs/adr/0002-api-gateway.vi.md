# ADR-0002: Triển khai API Gateway

*(Bản dịch tiếng Việt của [`0002-api-gateway.md`](0002-api-gateway.md) — bản gốc tiếng Anh vẫn được
giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận (Accepted) | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Constitution cố định chuỗi edge là load balancer → API gateway → BFF, và yêu cầu gateway tự xác thực
token độc lập (Principle VI — "gateway không phải là ranh giới tin cậy các service khác dựa vào").
Nền tảng tự host trên K8s qua Ansible, hoàn toàn C#/.NET ở mọi nơi khác trong stack.

## Quyết định

Dùng **YARP** (Yet Another Reverse Proxy), host như 1 ứng dụng ASP.NET Core.

## Các phương án đã cân nhắc

### Phương án A: YARP
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — code-first, ít "đóng gói sẵn" hơn Kong |
| Chi phí | Miễn phí, do Microsoft duy trì |
| Khả năng mở rộng | Cao — ASP.NET Core không trạng thái |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Chạy như 1 app ASP.NET Core bình thường — cùng container/pipeline/telemetry
`ServiceDefaults` với mọi thứ khác; xác thực token dùng middleware auth chuẩn của ASP.NET Core, nên
mã xác thực ở tầng gateway và tầng service dùng chung 1 khuôn mẫu; được Microsoft chủ động duy trì và
dùng thật trong production bên trong chính Azure; chính sách định tuyến deny-by-default diễn đạt được
bằng đúng mô hình policy C# mà các service dùng.
**Nhược điểm:** Không có chợ plugin — rate limiting, biến đổi request... phải tự viết bằng middleware
YARP thay vì cấu hình từ 1 danh mục có sẵn; ít tính năng "gateway đóng gói sẵn" hơn Kong.

### Phương án B: Ocelot
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp — cấu hình dẫn dắt |
| Chi phí | Miễn phí |
| Khả năng mở rộng | Trung bình-Cao |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** .NET-native như YARP; định tuyến bằng file cấu hình dựng nhanh; theo lịch sử là lựa chọn
mặc định cho API gateway .NET.
**Nhược điểm:** Nhịp độ bảo trì đã chậm lại so với YARP, vốn giờ có Microsoft hậu thuẫn và là phương án
tiến hoá tích cực hơn; ít tính năng load-balancing/health-check nâng cao hơn YARP.

### Phương án C: Kong
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — chế độ có DB hoặc không DB, cấu hình plugin |
| Chi phí | Miễn phí (OSS) / có bản thương mại |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Hệ sinh thái plugin lớn (rate limiting, auth, biến đổi request) qua cấu hình thay vì code;
đã được kiểm chứng ở quy mô lớn trên nhiều stack.
**Nhược điểm:** Dựa trên Lua/OpenResty — cùng vấn đề "runtime lạ phá vỡ observability `ServiceDefaults`"
như Keycloak ở ADR-0001; thêm 1 admin API và (ở chế độ DB) 1 dependency datastore không có sẵn trong hạ
tầng này; đội chưa có kinh nghiệm Kong.

## Phân tích đánh đổi

Cùng lý lẽ như quyết định máy chủ định danh: ở lại trong hạm đội C#/.NET giữ cho gateway quan sát được
qua đúng thành phần telemetry dùng chung bắt buộc, và giữ mã xác thực token nhất quán giữa gateway và
mọi service phía sau nó. YARP được chọn thay vì Ocelot cụ thể vì quỹ đạo bảo trì tích cực hơn và có
Microsoft hậu thuẫn.

## Hệ quả

- Các tính năng tầng gateway không có sẵn trong YARP (rate limiting nâng cao, biến đổi request/response
  vượt ngoài mức cơ bản) là middleware do chính đội viết và test.
- Gateway là 1 service triển khai hạng nhất trong cùng pipeline CI/CD, không phải 1 appliance vận hành
  riêng biệt.

## Việc cần làm

1. [ ] Dựng khung project gateway YARP có nối sẵn `ServiceDefaults`
2. [ ] Cài đặt xác thực JWT bearer khớp với token do máy chủ định danh ở ADR-0001 phát hành
3. [ ] Định nghĩa cấu hình route/cluster YARP cho BFF
