# Chức năng: Sản phẩm (Catalog)

*Đối tượng đọc: quản lý, người phụ trách nghiệp vụ. Không yêu cầu đọc code.*

## Chức năng này làm gì

Cho phép người mua xem danh sách sản phẩm đang được bán, gồm tên và giá. Đây là bước đầu tiên của luồng mua hàng: khách phải thấy sản phẩm trước khi bỏ vào giỏ.

## Trạng thái thật hiện tại

- Danh sách sản phẩm hiện là **3 sản phẩm mẫu, được nạp sẵn (seed) mỗi lần hệ thống khởi động** — không phải dữ liệu do ai đó nhập vào qua giao diện quản trị. Bằng chứng: `services/products/src/Products.Api/Data/CatalogSeed.cs`.
- **Chưa có màn hình quản trị** để thêm/sửa/xoá sản phẩm. Muốn đổi danh sách sản phẩm, phải sửa trực tiếp trong code.
- Giao diện web chỉ có **một** thao tác trên sản phẩm: xem danh sách (`GET /products`, đi qua BFF rồi Gateway). Chưa có tìm kiếm, lọc theo danh mục, phân trang, hay xem chi tiết từng sản phẩm.
- Mỗi sản phẩm có: mã (Id), tên (Name), giá (Price). Không có mô tả, hình ảnh, hay tồn kho.

## Vì sao thiết kế như vậy

Đây là bước tối thiểu để chứng minh luồng mua hàng chạy được đầu-cuối (giai đoạn "walking skeleton" của roadmap) — cố tình đơn giản để không mất thời gian vào phần chưa cần thiết ở giai đoạn này.

## Ai sở hữu dữ liệu này

Dữ liệu sản phẩm nằm trong cơ sở dữ liệu riêng của dịch vụ **Products**, không dịch vụ nào khác (kể cả Giỏ hàng, Đặt hàng) được phép đọc thẳng vào đó. Khi cần thông tin sản phẩm, các phần khác phải gọi qua API, không được "đi tắt" qua dữ liệu. Đây là quy tắc thiết kế cố định của toàn hệ thống, chi tiết tại [`../thiet-ke-he-thong.md`](../thiet-ke-he-thong.md).

## Tham chiếu kỹ thuật (cho ai muốn tra cứu)

- Mã nguồn: `services/products/src/Products.Api/`
- Endpoint nghiệp vụ: `Features/Catalog/CatalogEndpoints.cs` (`GET /products`)
- Tài liệu tương ứng khi tính năng này được xây (spec-kit): `specs/004-minimal-shopping-spa/` (một phần của tính năng khung sườn mua hàng, không phải spec riêng cho Products)
