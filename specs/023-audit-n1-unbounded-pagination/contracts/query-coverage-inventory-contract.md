# Hợp đồng: Danh sách kiểm kê endpoint danh sách và điểm gọi bị chặn (Query Coverage Inventory)

Danh sách tường minh mà `tests/QueryCoverageTests` dùng làm nguồn sự thật (research.md Decision 7;
data-model.md "List Endpoint Inventory Entry" / "Bounded Query Site Inventory Entry"). Đây là hợp
đồng giữa mã nguồn thực tế và test quét — khi một trong hai lệch khỏi cái còn lại, build PHẢI đỏ.

## Endpoint danh sách (`ExpectedListEndpoints`)

| Name | Service | SourceFile | RequiredMarkers |
|---|---|---|---|
| `products-listing` | products | `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs` | `"DefaultPageSize"`, `"MaxPageSize"` |
| `bff-products-listing` | bff | `services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs` | `"page"`, `"pageSize"` |

## Điểm gọi bị chặn (`ExpectedBoundedQuerySites`)

| Name | Service | SourceFile | RequiredMarkers |
|---|---|---|---|
| `bff-basket-render` | bff | `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs` | `"GetProductsByIdsAsync"` |
| `bff-basket-add-item` | bff | `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs` | `"GetProductsByIdsAsync"` |

## Endpoint danh sách được xác nhận KHÔNG tồn tại (không có dòng scanner, ghi nhận bằng văn bản)

Theo research.md Decision 1 (#3/#4) — `orders` và `parties` không có endpoint trả về danh sách nào
tại thời điểm tính năng này hoàn tất (chỉ get-by-id). Không thêm dòng scanner trỏ tới một endpoint
không tồn tại (một dòng như vậy sẽ luôn đỏ, không đúng mục đích). Khi một trong hai service này có
endpoint danh sách đầu tiên trong tương lai, PR đó PHẢI thêm một dòng mới vào bảng "Endpoint danh
sách" ở trên trong cùng PR — đây là quy tắc ở dưới, không phải một ngoại lệ.

## Quy tắc thay đổi danh sách này

- Thêm một endpoint danh sách mới vào hệ thống (service mới, hoặc `orders`/`parties` có endpoint
  danh sách đầu tiên) PHẢI thêm một dòng tương ứng vào bảng "Endpoint danh sách" VÀ vào
  `QueryCoverageScanner.ExpectedListEndpoints` trong cùng pull request. Thiếu một trong hai là vi
  phạm hợp đồng.
- Thêm một điểm gọi tải-dữ-liệu-quan-hệ mới có nguy cơ N+1/over-fetch (EF Core `Include`/lookup xuyên
  service) PHẢI thêm một dòng vào bảng "Điểm gọi bị chặn" VÀ vào
  `QueryCoverageScanner.ExpectedBoundedQuerySites` trong cùng pull request.
- Xoá một endpoint hoặc điểm gọi khỏi hệ thống PHẢI xoá dòng tương ứng ở cả hai nơi — một dòng còn
  sót lại trỏ tới một file/marker không còn tồn tại sẽ làm `QueryCoverageTests` đỏ, đúng chủ đích
  (buộc dọn dẹp thay vì để lại rác), giống hệt quy tắc đã áp dụng cho
  `tests/ResilienceCoverageTests` (020).
- Marker chỉ chứng minh SỰ CÓ MẶT của một chuỗi văn bản trong file nguồn — không chứng minh ngữ nghĩa
  đúng (ví dụ một `MaxPageSize` bị đặt sai giá trị vẫn qua được scanner). Test động (integration test
  seed 500 sản phẩm, test đếm câu lệnh SQL, test đếm số lần gọi HTTP) là lớp bảo vệ thứ hai, bổ sung
  cho scanner tĩnh này — không thay thế nó (xem `quickstart.md`).
