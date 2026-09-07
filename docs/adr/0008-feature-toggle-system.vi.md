# ADR-0008: Hệ thống Feature Toggle

*(Bản dịch tiếng Việt của [`0008-feature-toggle-system.md`](0008-feature-toggle-system.md) — bản gốc
tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Principle X yêu cầu mọi thay đổi không tầm thường phải được triển khai sau 1 toggle có tên chủ sở hữu
(owner) và ngày gỡ bỏ được ghi lại ngay từ lúc tạo, và yêu cầu rollback được mà không cần đổi code hay
redeploy. Đây là 1 yêu cầu khá hẹp (tồn tại, có chủ sở hữu, có hạn dùng, lật (flip) nhanh) chứ không
phải đòi hỏi 1 hệ thống targeting/thử nghiệm (experimentation) phức tạp.

## Quyết định

Dùng **Unleash**, tự host (self-hosted).

## Các phương án đã cân nhắc

### Phương án A: Unleash (tự host)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — thêm 1 service dựa trên Postgres cần vận hành |
| Chi phí | Miễn phí (OSS); có thể chuyển sang bản hosted sau nếu muốn |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp lúc đầu |

**Ưu điểm:** Tự host được, khớp với mô hình triển khai Ansible/K8s như mọi thứ khác; có sẵn admin UI
cho phép ai đó lật 1 flag mà không cần deploy hay chạy script database — phục vụ trực tiếp mục tiêu
"rollback không cần redeploy" của Principle X; có SDK chính thức cho .NET và React; metadata
owner/tag trên flag có thể mở rộng bằng 1 kiểm tra CI làm fail build nếu 1 toggle đã quá hạn gỡ bỏ đã
ghi, tự động hoá quy tắc "toggle cũ là nợ kỹ thuật."
**Nhược điểm:** Thêm 1 service có trạng thái (dựa trên Postgres) cần vận hành; độ tinh vi của
UI/targeting tốt nhưng không bằng LaunchDarkly ở các kịch bản nâng cao — không cần thiết ở đây.

### Phương án B: LaunchDarkly
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp (được quản lý hoàn toàn) |
| Chi phí | Chi phí SaaS định kỳ, tăng theo mức sử dụng |
| Khả năng mở rộng | Cao (do nhà cung cấp quản lý) |
| Độ quen thuộc của đội | Trung bình |

**Ưu điểm:** UI targeting/thử nghiệm hàng đầu; gần như không có gánh nặng vận hành.
**Nhược điểm:** Chi phí SaaS ngoài định kỳ và 1 dependency bên ngoài, không nhất quán với tư thế tự
host của phần còn lại nền tảng; giá theo số ghế/MAU là 1 khoản chi thật sự, ngày càng tăng, cho 1 tính
năng mà constitution chỉ yêu cầu ở dạng hẹp.

### Phương án C: Flagsmith (tự host)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình |
| Chi phí | Miễn phí (OSS) |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Hồ sơ tự-host, mã nguồn mở tương tự Unleash.
**Nhược điểm:** Cộng đồng và hệ sinh thái nhỏ hơn Unleash; SDK .NET chưa trưởng thành và ít được kiểm
chứng thực chiến bằng.

### Phương án D: Bảng flag tự xây (homegrown)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp lúc đầu, tăng dần theo thời gian |
| Chi phí | Chỉ tốn thời gian kỹ thuật |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Không hạ tầng bên thứ 3 nào; owner/ngày-gỡ-bỏ chỉ là cột trong bảng, dễ dàng thực thi bằng
1 lint CI gắn vào gate SonarQube/Jenkins; toàn quyền kiểm soát, khớp triết lý "mọi thứ đều là C#" của
nền tảng.
**Nhược điểm:** Không có admin UI sẵn có — muốn lật flag an toàn mà không cần deploy nghĩa là phải tự
xây 1 UI, đúng chính công cụ mà Unleash đã cung cấp sẵn; không có engine rollout theo phần trăm hay
targeting nếu nền tảng cần rollout dần dần sau này.

## Phân tích đánh đổi

LaunchDarkly bị loại chủ yếu vì đưa vào nền tảng dependency SaaS bên ngoài định kỳ đầu tiên, trong khi
1 phương án tự host đã đáp ứng đủ yêu cầu thực tế. Giữa Unleash và 1 bảng tự xây, Unleash thắng chủ yếu
nhờ admin UI có sẵn — trọng tâm của Principle X là rollback nhanh, an toàn mà không cần deploy, và 1 UI
thật sự cho người không phải kỹ sư lật flag phục vụ mục tiêu đó tốt hơn là yêu cầu 1 script database
hoặc 1 công cụ nội bộ mà đội sẽ phải tự xây.

## Hệ quả

- Unleash trở thành 1 service khác với yêu cầu sẵn sàng (availability) riêng — việc lật 1 flag trong
  lúc xảy ra sự cố phụ thuộc vào việc Unleash có truy cập được hay không.
- CI phải được mở rộng để truy vấn Unleash (hoặc 1 bản export đồng bộ) và làm fail build với các toggle
  đã quá hạn gỡ bỏ đã ghi.

## Việc cần làm

1. [ ] Triển khai Unleash tự host với kho lưu trữ Postgres
2. [ ] Tích hợp SDK .NET và React lần lượt vào `ServiceDefaults` và package dùng chung phía frontend
3. [ ] Xây dựng kiểm tra CI làm fail build với các toggle đã quá hạn gỡ bỏ đã ghi
