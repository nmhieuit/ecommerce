# ADR-0010: Công cụ Monorepo phía Frontend

*(Bản dịch tiếng Việt của [`0010-frontend-monorepo-tooling.md`](0010-frontend-monorepo-tooling.md) —
bản gốc tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn
đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Constitution cố định React + TypeScript strict + Vite + TanStack Query trong 1 monorepo duy nhất,
chứa tối thiểu: web SPA, mobile-web SPA, design system dùng chung (ADR-0009), và client API được sinh
ra (ADR-0004). Mục Development Workflow bắt buộc trunk-based development với các nhánh sống ngắn, PR
nhỏ, merge thường xuyên — điều này phụ thuộc vào việc CI vẫn nhanh khi repo phình to.

## Quyết định

Dùng **Turborepo**.

## Các phương án đã cân nhắc

### Phương án A: Turborepo
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp — tầng điều phối (orchestration) mỏng |
| Chi phí | Miễn phí; remote caching là tuỳ chọn |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Trung bình |

**Ưu điểm:** Cấu hình tối thiểu (task pipeline `turbo.json`) — không can thiệp vào Vite, vì mỗi package
chỉ chạy `vite build`/`vite dev` riêng của nó và Turborepo điều phối, cache lại các script đó; caching
build/test tăng dần nhanh phục vụ trực tiếp workflow "merge thường xuyên vào main" bằng cách giữ CI
nhanh; đường học tập thấp so với chỉ dùng workspace của package manager thuần.
**Nhược điểm:** Không có sẵn code generator để dựng khung package/component mới (phải làm thủ công
hoặc tự cấu hình Plop); trực quan hoá đồ thị phụ thuộc kém tinh vi hơn Nx; phát hiện test/lint "bị ảnh
hưởng" (affected) làm được qua lọc git-diff nhưng kém hoàn thiện hơn Nx.

### Phương án B: Nx
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình-Cao — executor, generator, project.json |
| Chi phí | Lõi miễn phí; Nx Cloud cho remote caching |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Đồ thị task và caching mạnh; code generator để dựng khung; phát hiện "affected" và công
cụ đồ thị phụ thuộc mạnh; hệ sinh thái plugin lớn, gồm cả 1 executor cho Vite.
**Nhược điểm:** Mô hình khái niệm áp đặt lên repo nặng nề hơn — nhiều thứ phải học hơn mức 1 đội
C#-first với bề mặt frontend nhỏ hơn cần; các quan điểm đặc thù-Vite của Nx thỉnh thoảng lạc hậu so
với hệ sinh thái Vite mới nhất, gây ma sát với workflow Vite-first mà constitution đã cam kết.

### Phương án C: pnpm workspace thuần (không công cụ điều phối)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp nhất |
| Chi phí | Miễn phí |
| Khả năng mở rộng | Giảm dần khi số package tăng |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Thiết lập đơn giản nhất có thể, không có gì thêm để học hay bảo trì.
**Nhược điểm:** Không có build caching — mọi package build lại hoàn toàn ở mỗi lần chạy CI; không có
cơ chế tự động "chỉ test/build cái đã đổi", nên thời gian CI tăng gần như tuyến tính theo số package,
đánh thẳng vào workflow trunk-based, PR-nhỏ-thường-xuyên mà constitution bắt buộc.

## Phân tích đánh đổi

Mô hình khung nặng nề hơn của Nx bị đánh giá là không tương xứng với 1 monorepo chỉ có vài package
(2 app, 1 design system, 1 client sinh ra) trên 1 đội vốn C#-first ở mọi nơi khác. Turborepo mang lại
lợi ích về tốc độ CI thực sự quan trọng cho trunk-based development mà không áp đặt gánh nặng đó, đánh
đổi lại là mất 1 số tiện lợi dựng khung mà Nx sẽ cung cấp.

## Hệ quả

- Các bước pipeline CI phải được định nghĩa theo từng package qua pipeline `turbo.json`, với cache key
  gắn với hash của source/lockfile.
- Nếu số lượng package frontend tăng đáng kể (nhiều app hoặc thư viện dùng chung hơn) hoặc đội muốn có
  generator/đồ thị phụ thuộc mạnh hơn, xem lại Nx như 1 tu chính có ghi nhận thay vì gắn dần các mối lo
  đó vào Turborepo theo kiểu chắp vá.

## Việc cần làm

1. [ ] Khởi tạo Turborepo trong monorepo frontend với pnpm workspace
2. [ ] Định nghĩa pipeline `turbo.json` cho build/test/lint trên các package web, mobile-web,
   design-system, và client-sinh-ra
3. [ ] Nối remote caching vào pipeline Jenkins nếu thời gian CI đòi hỏi
