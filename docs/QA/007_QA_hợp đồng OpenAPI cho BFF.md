# QA: Hợp đồng OpenAPI cho BFF — tài liệu tự sinh và client sinh tự động

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát (US1 → US3)

1. **Hợp đồng luôn khớp route** (US1): BFF tự sinh tài liệu OpenAPI từ chính các route (chỉ công bố ở Development,
   `/openapi/v1.json`), mô tả đường dẫn, phương thức, hình dạng phản hồi và các mã `200/404/502/504`.
2. **Client SPA sinh hoàn toàn từ hợp đồng** (US2): `pnpm --filter @ecommerce/api-client generate` (1 lệnh, dưới 1 phút) và
   `verify-generated` phát hiện lệch giữa client đã commit và tài liệu BFF; SPA không có lời gọi HTTP thô tới BFF ngoài client sinh.
3. **Tolerant reader** (US3): phản hồi BFF có thêm trường lạ không làm SPA vỡ (catalog, giỏ, checkout).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Dựa trên [`specs/007-bff-openapi-contracts/quickstart.md`](../../specs/007-bff-openapi-contracts/quickstart.md)
(quickstart gốc dùng `dotnet run` cục bộ) nhưng đổi sang chạy qua **Docker Desktop** (`docker-compose.local.yml` — BFF chạy
môi trường Development, công bố cổng `5301`, nên phát hành được `/openapi/v1.json`). Lệnh dựng kéo theo các service và DB của chúng nên lần
đầu mất vài phút.

> Nếu `localhost` không gọi được dù container `healthy`: khởi động lại hẳn Docker Desktop (lỗi forwarding
> IPv6 loopback `::1` của WSL2), không phải lỗi ứng dụng.

### Thủ công — dựng BFF và kiểm hợp đồng

```bash
cp .env.example .env    # chỉ cần 1 lần
docker compose -f docker-compose.local.yml up -d --wait bff-api
```

| Bước | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| SC-001 — tài liệu có đủ route | Postman folder `Common` → "Tài liệu OpenAPI của BFF" (`GET {{bffUrl}}/openapi/v1.json`, không cần token); các assertion của request tự kiểm 7 route, `502/504`, `404`, schema `ProductSummary` | Mọi route products/baskets/orders có path + mã phản hồi đúng | Đúng: `200 application/json`, **7 path** — `/bff/products`, `/bff/basket`, `/bff/basket/items`, `/bff/baskets/{basketId}`, `/bff/orders/{orderId}`, `/bff/parties/{partyId}`, `/bff/checkout`. Mọi GET khai `200/502/504`; theo id thêm `404`; `POST /bff/basket/items` khai `200/400/404/502/504`, `POST /bff/checkout` khai `201/409/502/504`; `ProductSummary` có `id,name,price`. *(Tôi chạy lại đúng các assertion của request Postman đó bằng 1 script node vì không điều khiển được ứng dụng Postman ở đây.)* |
| Đối chiếu route ↔ tài liệu | So `Features/*/*Endpoints.cs` với danh sách path ở trên | 1-1 | Khớp 1-1 (spec 007 chỉ cam kết products/baskets/orders; tài liệu còn phủ cả parties và checkout) |
| SC-002/SC-003 — client sinh không lệch | `cd frontend && corepack pnpm --filter @ecommerce/api-client verify-generated` (BFF đang chạy ở `:5301`) | Thoát `0`, không lệch | **FAIL, exit 1**: sinh lại từ BFF thật cho ra khác biệt so với bản đã commit — sửa `endpoints.ts`, `model/index.ts`, `model/productListResponse.ts` (+ thêm `page`, `pageSize`, `totalCount`) và thêm mới 4 file (`listProductsParams.ts`, `productListResponsePage.ts`, `productListResponsePageSize.ts`, `productListResponseTotalCount.ts`). Đã hoàn tác sau khi ghi nhận |
| SC-003 — không có HTTP thô | `grep -rE "fetch\(\|axios" frontend/apps/web/src` | 0 kết quả tới BFF | 1 kết quả: `src/auth/identityClient.ts` gọi `fetch` tới `/connect/token` của identity (không phải endpoint BFF; ngoại lệ đã được spec 004 FR-014 amend). Không có lời gọi thô nào tới BFF |
| SC-005 — sinh lại bằng 1 lệnh < 1 phút | `time corepack pnpm --filter @ecommerce/api-client generate` | Dưới 1 phút | Đúng: **6 giây** |
| SC-004 — trường lạ không làm vỡ SPA | 3 test tolerant-reader (bảng "Tự động") | PASS | 17/17 PASS (3 file) |
| Dọn dẹp | `docker compose -f docker-compose.local.yml down` | | |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm/`it()` (comment mỗi hàm đã gắn `Task nguồn: ...`, xem lại tại đó nếu cần biết test ứng với US/task nào). Test hậu tố `IntegrationTests` tự dựng BFF trong tiến trình (không cần stack Docker). Frontend chạy từ thư mục `frontend/`; máy này không có `pnpm` trên PATH nên dùng `corepack pnpm ...`.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US1/SC-001 — tài liệu OpenAPI sinh từ route có đủ path, khai `200/502/504` cho mọi GET, `404` cho route theo id, schema `ProductListResponse`/`ProductSummary` khớp hợp đồng viết tay | [`GeneratedContractTests.cs:35`](../../services/bff/tests/Bff.Api.IntegrationTests/GeneratedContractTests.cs#L35) · [`:61`](../../services/bff/tests/Bff.Api.IntegrationTests/GeneratedContractTests.cs#L61) · [`:82`](../../services/bff/tests/Bff.Api.IntegrationTests/GeneratedContractTests.cs#L82) · [`:100`](../../services/bff/tests/Bff.Api.IntegrationTests/GeneratedContractTests.cs#L100) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~GeneratedContractTests"` |
| US3/FR-006/SC-004 — catalog: sản phẩm có trường lạ vẫn hiển thị | [`ProductList.test.tsx:154`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L154) | `cd frontend && corepack pnpm --filter @ecommerce/web test -- ProductList` |
| US3/FR-006/SC-004 — giỏ: dòng giỏ có trường lạ vẫn hiển thị | [`BasketView.test.tsx:190`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L190) | `cd frontend && corepack pnpm --filter @ecommerce/web test -- BasketView` |
| US3/FR-006/SC-004 — checkout: phản hồi đơn có trường lạ vẫn hoàn tất, giữ đủ 3 trường | [`DoubleSubmit.test.tsx:178`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L178) | `cd frontend && corepack pnpm --filter @ecommerce/web test -- DoubleSubmit` |
| US2/SC-002/SC-005 — client sinh khớp tài liệu BFF, không chỉnh tay; sinh lại bằng 1 lệnh (cần BFF Development đang chạy ở `:5301`; không có file test) |  | `cd frontend && corepack pnpm --filter @ecommerce/api-client verify-generated` (và `... generate`) |

**Kết quả lượt QA này**: `GeneratedContractTests` **9/9 PASS** (4 hàm, tính cả các dòng `[Theory]`) · 3 test tolerant-reader (ProductList, BasketView,
DoubleSubmit) **17/17 PASS** cùng 3 file (chạy cả file) · tài liệu OpenAPI thật có 7 route · `verify-generated` **FAIL** (client đã commit lỗi thời) ·
`generate` 6 giây.

## Kết luận

**FAIL** ở cổng chống lệch của chính spec này: `verify-generated` (FR-004/SC-002) đang đỏ vì client TypeScript đã commit **không còn khớp** tài liệu OpenAPI của BFF —
sau spec 023 (phân trang `/bff/products`, commit `d73ddb1`) tài liệu có thêm `page`/`pageSize`/`totalCount` và tham số truy vấn nhưng client chưa được sinh lại
(lần cuối là commit `c99783c` của spec 004). Các phần còn lại **đạt**: tài liệu có đủ 7 route với mã phản hồi đúng (SC-001), không có lời gọi HTTP thô tới BFF (SC-003), sinh lại chỉ
6 giây (SC-005), và tolerant reader 17/17 PASS (SC-004). Chi tiết: [QA_Debt.md](QA_Debt.md).
