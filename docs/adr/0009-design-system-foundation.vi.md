# ADR-0009: Nền tảng Design System phía Frontend

*(Bản dịch tiếng Việt của [`0009-design-system-foundation.md`](0009-design-system-foundation.md) —
bản gốc tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn
đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Principle IX yêu cầu 1 package design-system dùng chung, có version, được cả 2 app tiêu thụ, các
component đạt chuẩn tiếp cận WCAG 2.2 AA, và Principle VIII đặt ra ngân sách Core Web Vitals chặt chẽ
(LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1) cùng 1 ngân sách kích thước bundle JS theo từng route. Nền tảng
phục vụ 2 tenant, ngụ ý design system cần hỗ trợ branding/theming khác nhau, không chỉ 1 bộ nhận diện
hình ảnh duy nhất.

## Quyết định

Dùng **Radix UI primitives + Tailwind CSS** làm nền tảng design-system, được tài liệu hoá và xem
trước bằng **Storybook**.

## Các phương án đã cân nhắc

### Phương án A: Radix UI + Tailwind CSS
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — đội tự xây/sở hữu tầng component đã style |
| Chi phí | Miễn phí (OSS) |
| Khả năng mở rộng | N/A |
| Độ quen thuộc của đội | Trung bình |

**Ưu điểm:** Các primitive của Radix không có style sẵn nhưng đạt chuẩn tiếp cận đầy đủ theo mặc định
(điều hướng bàn phím, ARIA role, quản lý focus đã được xây và test ở thượng nguồn) — phục vụ trực tiếp
yêu cầu "Component PHẢI đạt chuẩn tiếp cận" với ít công sức kỹ thuật accessibility nội bộ nhất; Tailwind
biên dịch lúc build, chi phí runtime để tiêm style gần như bằng 0, khớp với mục tiêu ngân sách bundle
chặt chẽ và CWV; *code* component nằm bên trong package design-system thay vì 1 dependency không thể
can thiệp, nên theming theo từng tenant (token màu, branding) hoàn toàn do đội sở hữu.
**Nhược điểm:** Nhiều công sức lắp ráp ban đầu hơn — đội phải tự xây tầng component hình ảnh thật sự
(Button, Modal...) trên nền các primitive thay vì nhận chúng đã được style sẵn.

### Phương án B: MUI (Material UI)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp lúc bắt đầu |
| Chi phí | Lõi miễn phí, các bậc nâng cao có phí |
| Khả năng mở rộng | N/A |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Bộ component đầy đủ, có style sẵn ngay khi dùng; hệ thống theme-provider trưởng thành hỗ
trợ native các object theme theo từng tenant; tốc độ khởi động nhanh.
**Nhược điểm:** Engine CSS-in-JS chạy lúc runtime mang chi phí kích thước bundle và tiêm style thật
sự, đi ngược ngân sách JS-bundle-theo-route và mục tiêu CWV của Principle VIII; ngôn ngữ hình ảnh
Material Design cần công sức override sâu để tránh "trông giống 1 app Material", cản trở 1 thương
hiệu thương mại điện tử đa tenant có nét riêng.

### Phương án C: Chakra UI
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp-Trung bình |
| Chi phí | Miễn phí (OSS) |
| Khả năng mở rộng | N/A |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Mặc định về khả năng tiếp cận tốt; bề mặt API đơn giản hơn MUI; theming khá ổn.
**Nhược điểm:** Mối lo về chi phí engine styling lúc runtime tương tự MUI (đã cải thiện ở v3 nhưng vẫn
nặng hơn Tailwind biên dịch lúc build); hệ sinh thái nhỏ hơn.

## Phân tích đánh đổi

Yếu tố quyết định là ngân sách hiệu năng cứng của Principle VIII và yêu cầu branding cho 2 tenant — cả
2 đều đẩy về hướng 1 cách tiếp cận styling biên dịch lúc build, hoàn toàn tự sở hữu, thay vì 1 bộ
component runtime đã style sẵn. Các đảm bảo về khả năng tiếp cận của Radix giảm gánh nặng tự test a11y
của đội nhiều hơn bất kỳ phương án thay thế nào, điều này quan trọng vì WCAG 2.2 AA là 1 yêu cầu cứng,
không phải 1 mục tiêu.

## Hệ quả

- Đội sở hữu và phải bảo trì toàn bộ tầng component hình ảnh, không chỉ tiêu thụ component của 1 nhà
  cung cấp — đầu tư kỹ thuật design-system nhiều hơn ngay từ đầu.
- Storybook được thêm vào monorepo để tài liệu hoá/xem trước, dùng plugin `storybook-addon-a11y` để
  bắt lỗi vi phạm WCAG trực tiếp trong CI/lúc review PR.

## Việc cần làm

1. [ ] Dựng khung package design-system với Radix primitives + Tailwind
2. [ ] Thêm Storybook cùng addon accessibility, nối vào các kiểm tra PR
3. [ ] Định nghĩa cơ chế theming/token theo từng tenant
