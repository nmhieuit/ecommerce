# Tài liệu kỹ thuật tổng quan — Nền tảng Ecommerce

*Viết cho: quản lý không trực tiếp code .NET. Mục tiêu: hiểu codebase hiện tại đang có gì, các phần liên hệ với nhau ra sao, và vì sao nó được thiết kế như vậy — không cần đọc code.*

*Cập nhật lần cuối: sau khi hoàn thành tính năng `004-minimal-shopping-spa` (SCRUM-14) — **toàn bộ luồng "duyệt sản phẩm → thêm giỏ hàng → thanh toán → xác nhận" nay chạy được thật, có giao diện thật**, không còn là API rỗng nữa.*

---

## Điều quan trọng nhất cần biết trước khi đọc tiếp

Đây **không phải** một hệ thống thương mại điện tử đang chạy production. Theo [`docs/roadmap.md`](roadmap.md), đây là **dự án luyện tập cá nhân (solo)** để một người thực hành đầy đủ vòng đời phần mềm (Product Owner → Dev → QA → DevOps → SRE), đang ở **Giai đoạn 1/5 ("Walking Skeleton")** — và với tính năng vừa hoàn thành, **Giai đoạn 1 gần như đã xong**: chỉ còn thiếu "chạy toàn bộ hệ thống bằng một lệnh" (SCRUM-15).

Trong repo có **hai tầng thông tin** dễ nhầm lẫn với nhau:

| Tầng | Là gì | Đã có code chưa? |
|---|---|---|
| **Bản thiết kế mục tiêu** (`docs/system-design.md`, `docs/tech-stack-decisions.md`, `docs/adr/`, `.specify/memory/constitution.md`) | Kiến trúc đầy đủ dự kiến: 6 service nghiệp vụ, Identity Server thật, message queue (saga/outbox), Redis, phân vùng dữ liệu vật lý theo tenant | **Một phần nhỏ** — xem cột bên phải cho từng mục |
| **Codebase thực tế hôm nay** (`services/`, `shared/`, `tests/`, `frontend/`) | 6 service .NET chạy được + **1 giao diện web (SPA) thật**; một khách có thể duyệt sản phẩm, thêm vào giỏ, bấm thanh toán, và nhận xác nhận đơn hàng — **kiểm chứng trực tiếp qua toàn bộ chuỗi thật**, không phải mô phỏng | **Có**, và là toàn bộ những gì tài liệu này mô tả |

**Bốn tính năng đã hoàn thành theo đúng quy trình đặc tả (spec-kit)** — `001-scaffold-service-shells`, `002-gateway-bff-routing`, `003-stub-identity-tenant-context`, và mới nhất **`004-minimal-shopping-spa`** — đều có đủ tài liệu đặc tả/kế hoạch/nhiệm vụ và **100% nhiệm vụ đã đánh dấu hoàn thành** (`004` là **71/71**). Đây là lần đầu tiên toàn bộ 5 giai đoạn của roadmap có một luồng nghiệp vụ chạy trọn vẹn từ giao diện tới cơ sở dữ liệu.

Phần công cụ sinh tự động của Spec-Kit (`.specify/`, các slash-command) không được giải thích ở đây — nhưng nội dung *bên trong* các đặc tả đã hoàn thành ở `specs/` thì có, vì đó là nguồn xác nhận đáng tin cậy cho những gì đã thực sự được xây.

---

## 1. Giải thích codebase hiện tại

### 1.1 Đây là gì, về mặt kỹ thuật

Repo giờ có **hai hệ sinh thái tách biệt**, mỗi hệ có công cụ quản lý riêng:

- **Phần .NET** (backend): C# trên .NET 10. File gốc [`Ecommerce.slnx`](../Ecommerce.slnx) liệt kê **23 dự án con** (6 service + 12 dự án test đi kèm + 2 thư viện dùng chung + 1 test cho thư viện + 2 dự án kiểm tra kiến trúc). Con số này **không đổi** so với lần cập nhật trước — tính năng `004` không thêm dự án .NET mới, chỉ mở rộng code trong các dự án đã có.
- **Phần frontend** (giao diện, **hoàn toàn mới**): thư mục [`frontend/`](../frontend), một workspace pnpm + Turborepo độc lập, **không nằm trong `Ecommerce.slnx`** (đúng thông lệ cho dự án JavaScript/TypeScript). Gồm 2 gói: `apps/web` (chính website, React 19 + Vite + TypeScript strict) và `packages/api-client` (mã gọi API được **sinh tự động** từ tài liệu OpenAPI của BFF — không ai gõ tay code gọi API).
- `dotnet build` cho toàn bộ solution .NET: **sạch, 0 lỗi, 0 cảnh báo**. Build frontend (`pnpm run build`): **thành công**, gói JS nén còn **106.62 KB** (trong ngân sách 115 KB tự đặt ra). Bộ test frontend (Vitest): **45/45 bài qua**, cộng thêm 1 kịch bản kiểm thử đầu-cuối bằng Playwright mô phỏng thao tác thật của người dùng trên trình duyệt.

### 1.2 Sáu service .NET + 1 giao diện web

**Bốn service nghiệp vụ:**

| Service | Nghiệp vụ | Cổng (máy dev) | API đã có |
|---|---|---|---|
| Products | Danh mục sản phẩm | 5088 | `GET /products` — **nay có 3 sản phẩm mẫu thật** (sổ tay, bộ pha cà phê, tạp dề vải), không còn rỗng |
| Baskets | Giỏ hàng | 5188 | `GET/POST` giỏ hàng của người gọi hiện tại — **nay có dòng hàng thật** (sản phẩm, số lượng, đơn giá), không chỉ là 1 dòng trống |
| Orders | Đơn hàng | 5041 | Tạo đơn hàng từ giỏ, đọc lại đơn theo id |
| Parties | Khách hàng / định danh | 5204 | `GET /parties/{id}` |

**Hai service ở biên:**

| Service | Vai trò | Cổng | API đã có |
|---|---|---|---|
| Gateway | Cửa vào duy nhất; gán danh tính + tenant giả lập | 5300 | Chuyển tiếp mọi thứ sang BFF |
| BFF | Gộp/định hình dữ liệu; **nay điều phối cả luồng thanh toán** | 5301 | `GET /bff/products`, `GET/POST /bff/basket`, `POST /bff/basket/items`, **`POST /bff/checkout`** (mới), `GET /bff/orders/{id}` |

**Giao diện web (mới hoàn toàn):**

| Thành phần | Vai trò | Cổng (máy dev) |
|---|---|---|
| `frontend/apps/web` | Website React — 3 màn hình: Sản phẩm, Giỏ hàng, Xác nhận | 5173 (Vite dev server) |

Giao diện **chỉ gọi đúng một địa chỉ: Gateway (cổng 5300)** — không bao giờ gọi thẳng BFF hay bất kỳ service nào, kể cả lúc đang phát triển. Điều này được ép buộc bằng cấu trúc code (chỉ có đúng 1 chỗ trong toàn bộ mã nguồn frontend được phép dựng địa chỉ gọi API) chứ không phải quy ước bằng lời.

### 1.3 Dữ liệu đã có thật, giỏ hàng đã có dòng hàng, nhưng vẫn còn một khoảng nợ kỹ thuật cũ

- **Products** giờ có 3 dòng dữ liệu mẫu thật (nạp qua migration EF Core, không phải hook chạy lúc khởi động), nên `GET /products` không còn trả về rỗng.
- **Basket** đã đổi cấu trúc đáng kể: thay vì chỉ có `CustomerId`, nay có `CustomerRef` (gắn với danh tính giả lập của người gọi — một người chỉ có đúng 1 giỏ hàng đang mở) và một danh sách **dòng hàng thật** (`BasketLineItem`: sản phẩm, số lượng, đơn giá). Tổng tiền của từng dòng và của cả giỏ **được tính toán tại chỗ** (không lưu sẵn trong CSDL), để không bao giờ lệch với dữ liệu dòng hàng gốc.
- **Giá tiền luôn được lấy lại từ danh mục sản phẩm phía server** khi thêm vào giỏ — API thêm-vào-giỏ không có trường "giá" nào để client tự gửi lên, nên không có cách nào khách hàng (hay lỗi giao diện) tự ý đặt giá.
- **Khoảng nợ kỹ thuật cũ vẫn còn nguyên, không liên quan gì đến tính năng này:** kế hoạch "mỗi tenant một schema CSDL riêng" (từ tính năng `003`) vẫn **chưa được triển khai** — mọi bảng vẫn nằm chung schema `dbo`, chỉ có chốt chặn logic (`RequireTenantId()`) chứ chưa có ngăn cách vật lý. Tài liệu nhiệm vụ của chính tính năng `004` cũng tự nhắc lại điều này như một quyết định còn treo, chưa ai xử lý.

### 1.4 Một lượt mua hàng đi như thế nào (đã kiểm chứng chạy thật đầu-cuối)

```
Trinh duyet (Vite, cong 5173)
      │  CHI biet DUY NHAT dia chi Gateway (cong 5300)
      ▼
  Gateway (5300)     ── xac thuc gia lap LUON thanh cong, gan tenant "contoso"
      │                  + nguoi dung "phase1-stub-user", chuyen tiep nguyen ven
      ▼
  BFF (5301)          ── POST /bff/checkout:
      │                  1. Doc gio hang hien tai cua nguoi goi (goi Baskets)
      │                  2. Neu gio hang RONG -> dung ngay, tra loi 409 (khong tao don)
      │                  3. Tao don hang tu cac dong hang (goi Orders)
      │                  4. XOA gio hang (goi Baskets) -- CHI SAU KHI da co don hang
      ▼                     (thu tu nay co chu dich, xem Muc 2.4)
  Orders (5041)       ── tao ban ghi don hang, tra ve tong tien
      ▼
  SQL Server "orders"
```

Đã đo thật bằng một kịch bản cụ thể trong `tasks.md`: giỏ hàng gồm 2 quyển sổ tay + 1 tạp dề → thanh toán thành công, **tổng tiền $59.25**, đọc lại đơn hàng bằng đúng mã đơn nhận được thấy đúng dữ liệu, giỏ hàng sau đó rỗng, và **bấm thanh toán lần hai** trên giỏ đã rỗng bị chặn với lỗi 409 — không tạo ra đơn hàng thứ hai.

### 1.5 Định danh & ranh giới "tenant" — giờ có thêm "đang phục vụ ai"

Kế thừa từ tính năng `003` (Mục 1.5 bản trước), nay bổ sung thêm một khái niệm **tách biệt**: không chỉ "tenant nào" mà còn **"người gọi cụ thể nào"**.

- **`X-Tenant-Id`** (từ `003`): tenant cố định `contoso`, chặn mọi truy cập CSDL nếu thiếu.
- **`X-Subject-Id`** (mới, từ `004`): định danh người gọi cụ thể — hiện cố định là `phase1-stub-user` (đúng giá trị đã có sẵn trong cấu hình `StubIdentity` từ trước, nhưng trước đây **chưa từng được lan truyền đi đâu**, giờ mới thực sự dùng tới). Gateway gán, ghi đè giá trị client gửi lên (không tin), lan truyền qua BFF xuống các service nghiệp vụ theo đúng cơ chế đã có cho tenant.
- **Vì sao cần:** đây là cách Basket biết "giỏ hàng này là của ai" mà **không cần** client tự gửi kèm một mã giỏ hàng nào — giỏ hàng được suy ra từ chính danh tính người gọi, không phải tham số client có thể giả mạo.
- Cùng một cơ chế chốt chặn như tenant: thiếu `X-Subject-Id` → lỗi ngay, không âm thầm coi như "khách vãng lai" nào đó.

### 1.6 Giao diện web (mới) — ba màn hình, nói chuyện qua một cửa duy nhất

- **`/`** — Danh sách sản phẩm (Catalog)
- **`/basket`** — Giỏ hàng (xem, chỉnh, bấm thanh toán)
- **`/confirmation`** — Xác nhận đơn hàng sau khi thanh toán thành công

Không có trang "checkout" riêng — thanh toán là một nút bấm ngay trên trang giỏ hàng, không phải một trang mới.

Vài chi tiết đáng chú ý về chất lượng:
- **Khả năng tiếp cận (accessibility)** được đầu tư thật, không phải chiếu lệ: có "liên kết bỏ qua điều hướng" cho người dùng bàn phím, tiêu đề trang cập nhật theo từng màn hình cho người dùng trình đọc màn hình, và focus tự động chuyển tới nội dung mới sau mỗi lần chuyển trang.
- **Ngân sách dung lượng gói JS** (115 KB nén) được đặt ra và kiểm tra tự động — vượt ngân sách sẽ bị chặn, không phải phát hiện sau khi người dùng phàn nàn trang tải chậm.
- Mã gọi API (`packages/api-client`) **không do ai viết tay** — được sinh tự động từ đúng tài liệu OpenAPI mà `GeneratedContractTests` (Mục 2.6) đang canh giữ, nên nếu BFF đổi hợp đồng mà quên cập nhật tài liệu, giao diện sẽ build lỗi ngay thay vì âm thầm gọi sai.

### 1.7 Cách tổ chức code trong mỗi service

Không đổi so với trước: chia theo tính năng ("vertical-slice", `Features/<TênNăngLực>/`), ép buộc bằng máy (Mục 2.6), áp dụng cho cả Gateway và BFF.

---

## 2. Các dự án/component và cách chúng phụ thuộc lẫn nhau

### 2.1 Bản đồ quan hệ

- **4 service nghiệp vụ vẫn hoàn toàn không biết đến nhau.**
- **BFF gọi 4 service nghiệp vụ qua HTTP** — nay bao gồm cả điều phối nhiều bước cho thanh toán (gọi Baskets rồi Orders rồi lại Baskets), nhưng vẫn không tham chiếu code của bất kỳ service nào.
- **Gateway chỉ biết duy nhất địa chỉ của BFF.**
- **Giao diện web chỉ biết duy nhất địa chỉ của Gateway** — không có ngoại lệ nào kể cả khi đang phát triển cục bộ.
- **Cả 6 service .NET dùng chung 2 thư viện:** `shared/ServiceDefaults` (log/theo dõi) và `shared/Tenancy` (nay xác định **cả tenant lẫn người gọi cụ thể**, không chỉ tenant).
- Mã gọi API của frontend (`packages/api-client`) **không tham chiếu code .NET nào** — nó được sinh ra từ tài liệu OpenAPI công khai của BFF, tách biệt hoàn toàn hai hệ sinh thái.

### 2.2 `shared/ServiceDefaults` — không đổi so với lần trước

Chuẩn hoá log/theo dõi (OpenTelemetry) và mã theo dõi (`X-Correlation-Id`). Lỗi lan truyền mã theo dõi giữa Gateway/BFF đã được sửa ở tính năng `002`, không có gì mới ở lần này.

### 2.3 Gateway (`services/gateway`) — nay gán cả tenant lẫn người gọi

Vẫn dùng YARP, vẫn 1 route catch-all duy nhất sang BFF. Từ tính năng `004`, ngoài việc gán tenant cố định, Gateway còn đọc "người dùng giả lập" từ cấu hình `StubIdentity` và ghi header `X-Subject-Id` trước khi chuyển tiếp — cùng cơ chế "luôn ghi đè, không tin giá trị client gửi" như với tenant.

### 2.4 BFF (`services/bff`) — nay là nơi điều phối luồng thanh toán

Ngoài các cơ chế đã có (ngân sách thời gian, phân biệt lỗi 502/504/500/404, cắt gọt dữ liệu, lan truyền tenant+người gọi), tính năng `004` thêm **`POST /bff/checkout`** — điều phối 3 bước tuần tự:

1. Đọc giỏ hàng hiện tại (gọi Baskets)
2. Nếu rỗng → dừng, trả lỗi **409** ngay, không tạo đơn hàng
3. Tạo đơn hàng từ các dòng trong giỏ (gọi Orders)
4. Xoá giỏ hàng (gọi Baskets) — **chỉ sau khi** bước 3 đã thành công

**Thứ tự "tạo đơn trước, xoá giỏ sau" là quyết định có chủ đích**, ghi lại nguyên văn trong code: nếu bước 4 lỡ thất bại giữa chừng, khách hàng vẫn có một đơn hàng thật để xem — ngược lại, nếu xoá giỏ trước rồi tạo đơn thất bại, khách sẽ mất cả giỏ hàng lẫn đơn hàng, không có gì để bù đắp.

**Một quyết định kiến trúc đã được ghi nhận công khai, không giấu:** đây là một luồng đồng bộ hai bước do BFF điều phối trực tiếp, **không phải** cơ chế "saga có bù trừ" hay "outbox" như bản thiết kế mục tiêu yêu cầu (nguyên tắc IV trong `constitution.md`) — vì hệ thống chưa có RabbitMQ/MassTransit, chưa có bảng outbox nào. Đây là **sai lệch có ghi chép, có thời hạn**, nêu rõ trong [`docs/adr/0011-checkout-orchestration.md`](adr/0011-checkout-orchestration.md), gắn với công việc tương lai SCRUM-18/SCRUM-31 (Giai đoạn 2 và 4). Nói cách khác: nhóm phát triển tự nhận ra mình đang đi tắt so với quy chuẩn đã đặt ra, và ghi lại lý do — thay vì lặng lẽ làm khác đi.

**Chống bấm-thanh-toán-hai-lần:** không cần cơ chế đặc biệt riêng — vì bước 2 (giỏ rỗng → 409) tự nhiên chặn lần bấm thứ hai, do giỏ đã bị xoá sau lần đầu thành công. Đã kiểm chứng bằng kịch bản thật (Mục 1.4).

### 2.5 `shared/Tenancy` — nay xác định cả "tenant" lẫn "người gọi"

Mở rộng từ tính năng `003`. Có thêm `CallerContext` (giống hệt cấu trúc `TenantContext`: `SubjectId` nullable, `RequireSubjectId()` ném lỗi nếu thiếu) và middleware đọc header `X-Subject-Id`. Toàn bộ được gắn tự động vào mọi service qua một lệnh cấu hình chung, không cần sửa `Program.cs` của từng service.

### 2.6 Sáu "lưới an toàn" kiến trúc tự động — không đổi số lượng, có mở rộng phạm vi

Danh sách 6 loại vẫn như bản trước (xem lại nếu cần), riêng `Bff.Api.IntegrationTests/GeneratedContractTests` nay canh thêm cả hợp đồng của các route giỏ hàng/thanh toán mới, và có thêm kiểm tra CORS canh đúng việc Gateway chỉ cho phép giao diện web gọi vào (`Gateway.Api.IntegrationTests/StorefrontCorsTests`) — một lỗi CORS thật đã bị bắt và sửa trong quá trình làm tính năng này.

### 2.7 Hạ tầng cục bộ và triển khai

- Vẫn chỉ có `docker-compose.deps.yml` (4 container SQL Server). **Chưa có** compose/script chạy toàn bộ 6 service .NET + giao diện web bằng một lệnh (SCRUM-15) — hiện phải tự chạy từng phần theo đúng thứ tự trong `specs/004-minimal-shopping-spa/quickstart.md`.
- Vẫn chưa có CI/CD.

---

## 3. Mục đích từng phần + các tình huống thực tế được giải quyết

### 3.1 Mục đích từng dự án/thành phần

| Dự án/thành phần | Mục đích |
|---|---|
| `Products.Api` | Danh mục sản phẩm — nay có dữ liệu mẫu thật. |
| `Baskets.Api` | Giỏ hàng — nay có dòng hàng, số lượng, đơn giá, tự tính tổng. |
| `Orders.Api` | Tạo và đọc đơn hàng. |
| `Parties.Api` | Định danh khách hàng/đối tác. |
| `Gateway.Api` | Cửa vào duy nhất; gán tenant + người gọi giả lập. |
| `Bff.Api` | Gộp dữ liệu; **điều phối luồng thanh toán 3 bước**; xử lý lỗi có kiểm soát. |
| `shared/ServiceDefaults` | Chuẩn hoá log/theo dõi và mã tra cứu. |
| `shared/Tenancy` | Xác định + ép buộc cả "tenant nào" lẫn "người gọi nào". |
| `frontend/apps/web` | Giao diện mua hàng thật: 3 màn hình, chỉ gọi qua Gateway. |
| `frontend/packages/api-client` | Mã gọi API sinh tự động từ hợp đồng OpenAPI của BFF — không viết tay. |
| `tests/CrossServiceIsolation.Tests` | Chặn đọc/ghi nhầm CSDL service khác; chặn CSDL bị đọc mà chưa qua chốt tenant. |
| `tests/StructureConventionTests` | Chặn phá vỡ quy ước tổ chức code. |

### 3.2 Tám tình huống thật mà giải pháp này giải quyết

**1–6)** Giữ nguyên như bản trước (ngăn rò rỉ CSDL chéo service; health-check tách biệt; giữ chất lượng kiến trúc theo thời gian; một service chết không kéo sập cả hệ thống; lỗi có mã tra cứu; frontend không bỏ sót trường hợp lỗi nhờ hợp đồng API sinh tự động).

**7) "Quên xác định đang phục vụ ai" biến thành lỗi ồn ào ngay lập tức** — nay áp dụng cho cả tenant lẫn người gọi cụ thể, không chỉ tenant.

**8) Thanh toán trùng lặp do bấm nhầm/mạng chậm không tạo ra hai đơn hàng.**
Một tình huống rất thật trong thương mại điện tử: khách hàng bấm "Thanh toán", mạng chậm, khách sốt ruột bấm thêm lần nữa. Vì bước đầu tiên của thanh toán luôn kiểm tra giỏ hàng có còn hàng không, và giỏ đã bị xoá ngay sau lần thanh toán thành công đầu tiên, lần bấm thứ hai tự động nhận lỗi rõ ràng (409 — "giỏ hàng trống") thay vì tạo ra đơn hàng thứ hai và tính tiền hai lần. Không cần viết thêm cơ chế "chống trùng lặp" riêng — tác dụng phụ tự nhiên của đúng thứ tự các bước.

---

## 4. Sơ đồ kiến trúc hiện tại (dùng được với draw.io)

Sơ đồ vẽ **đúng những gì đang có trong code hôm nay**, bao gồm cả giao diện web mới. Phần dưới (viền đứt màu đỏ) là những gì vẫn chỉ nằm trên giấy.

> **Lưu ý phân biệt:** repo có sẵn 3 sơ đồ khác ở [`docs/system-design.md`](system-design.md) — nhưng chúng vẽ **kiến trúc mục tiêu đầy đủ**, không phải trạng thái hiện tại.

### Cách dùng
1. Mở [app.diagrams.net](https://app.diagrams.net) (draw.io).
2. Vào menu **Extras → Edit Diagram…**
3. Xoá nội dung trống, dán toàn bộ khối XML bên dưới vào, bấm **Save/OK**.

*(Hoặc mở trực tiếp file [`docs/diagrams/current-state-architecture.drawio`](diagrams/current-state-architecture.drawio) đã có sẵn trong repo — nội dung giống hệt khối XML bên dưới.)*

```xml
<mxfile host="app.diagrams.net" modified="2026-08-16T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="current-state-004" name="Trang thai hien tai - sau 004">
    <mxGraphModel dx="1450" dy="1100" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1450" pageHeight="1650" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="title1" value="PHAN 1 -- DA CO TRONG CODE HOM NAY (23 du an .NET + 1 workspace frontend; build sach 0 loi 0 canh bao)" style="text;html=1;fontStyle=1;fontSize=15;fontColor=#2d6a2d;" vertex="1" parent="1">
          <mxGeometry x="30" y="10" width="1300" height="26" as="geometry" />
        </mxCell>

        <mxCell id="client" value="Web SPA (frontend/apps/web)&#10;React 19 + Vite, cong 5173&#10;3 man hinh: San pham (/) - Gio hang (/basket) - Xac nhan (/confirmation)&#10;CHI goi DUY NHAT dia chi Gateway (5300) -- ep buoc bang cau truc code&#10;Ma goi API sinh tu dong tu OpenAPI cua BFF (packages/api-client)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="50" width="460" height="110" as="geometry" />
        </mxCell>

        <mxCell id="sd" value="shared/ServiceDefaults&#10;(thu vien dung chung, KHONG phai service)&#10;- OpenTelemetry: log / trace / metric&#10;- Correlation-Id (X-Correlation-Id): sinh o Gateway,&#10;  ghi vao request de moi hop sau dung chung 1 ma" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="860" y="50" width="280" height="110" as="geometry" />
        </mxCell>

        <mxCell id="tenancy" value="shared/Tenancy&#10;(thu vien dung chung, KHONG phai service)&#10;- TenantContext.RequireTenantId() (tu 003)&#10;- MOI (004): CallerContext.RequireSubjectId()&#10;- Header: X-Tenant-Id + X-Subject-Id&#10;- Chan MOI ket noi CSDL neu thieu 1 trong 2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1160" y="50" width="260" height="110" as="geometry" />
        </mxCell>

        <mxCell id="gw" value="Gateway.Api (services/gateway) -- YARP&#10;Cong 5300&#10;Bang route = 1 dong duy nhat: MOI duong dan -&gt; BFF&#10;StubIdentity: xac thuc LUON thanh cong, gan co dinh&#10;tenant &quot;contoso&quot; + nguoi dung &quot;phase1-stub-user&quot;, ghi de&#10;ca X-Tenant-Id va X-Subject-Id (khong tin gia tri tu ngoai)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="200" width="460" height="140" as="geometry" />
        </mxCell>

        <mxCell id="gwnote" value="Vi sao 1 route catch-all: neu Gateway phai liet ke tung duong dan cua BFF thi&#10;moi lan BFF them tinh nang lai phai sua + deploy Gateway.&#10;&#10;StorefrontCorsTests canh dung viec CHI web SPA (5173) duoc phep goi qua CORS&#10;-- 1 loi CORS that da bi bat va sua trong qua trinh lam tinh nang 004." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="510" y="200" width="650" height="140" as="geometry" />
        </mxCell>

        <mxCell id="bff" value="Bff.Api (services/bff) -- Backend For Frontend&#10;Cong 5301  |  KHONG co co so du lieu&#10;GET /bff/products, GET/POST /bff/basket,&#10;POST /bff/basket/items, GET /bff/orders/{id}&#10;MOI: POST /bff/checkout -- dieu phoi 3 buoc (xem ghi chu duoi)&#10;TenantPropagationHandler: gan lai CA X-Tenant-Id lan X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="360" width="460" height="150" as="geometry" />
        </mxCell>

        <mxCell id="bffnote" value="Ngan sach thoi gian cho MOI loi goi ra ngoai: 1 giay / lan thu (toi da 2 lan lai) | toi da 3 giay tong cong | cau dao ngat sau 10s loi lien tuc&#10;502 = downstream khong ket noi duoc / cau dao da ngat    504 = vuot thoi gian cho    500 = loi cua chinh BFF    404 = khong tim thay (KHONG phai loi)&#10;&#10;POST /bff/checkout -- dieu phoi dong bo 3 buoc, theo DUNG thu tu nay (co chu dich, xem docs/adr/0011):&#10;  1. Doc gio hang hien tai (goi Baskets)   2. Gio RONG -&gt; dung ngay, tra loi 409, KHONG tao don&#10;  3. Tao don hang tu cac dong hang (goi Orders)   4. XOA gio hang (goi Baskets) -- CHI SAU KHI da co don hang that&#10;Day la dieu phoi dong bo do BFF tu lam, KHONG phai saga/outbox theo dung chuan thiet ke muc tieu -- sai lech co ghi chep, co thoi han (ADR-0011, SCRUM-18/31)." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="510" y="360" width="890" height="150" as="geometry" />
        </mxCell>

        <mxCell id="svcnote" value="4 service nghiep vu HOAN TOAN khong biet den nhau: khong cai nao goi cai nao, khong cai nao tham chieu code cua cai nao." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="30" y="525" width="1390" height="20" as="geometry" />
        </mxCell>

        <mxCell id="svc1" value="Products.Api&#10;Cong 5088&#10;GET /products -- NAY CO 3 SAN PHAM MAU THAT (seed migration)&#10;+ /health/live, /health/ready&#10;Bang: Product (Id, Name, Price)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="550" width="320" height="145" as="geometry" />
        </mxCell>
        <mxCell id="svc2" value="Baskets.Api&#10;Cong 5188&#10;GET/POST gio hang cua nguoi goi (theo CustomerRef, khong can id)&#10;+ /health/live, /health/ready&#10;Bang: Basket (CustomerRef, Total tinh tai cho) + BasketLineItem&#10;  (ProductId, Quantity, UnitPrice, LineTotal tinh tai cho)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="380" y="550" width="320" height="145" as="geometry" />
        </mxCell>
        <mxCell id="svc3" value="Orders.Api&#10;Cong 5041&#10;Tao don hang tu cac dong gio hang; doc lai theo id&#10;+ /health/live, /health/ready&#10;Bang: Order (Id, PlacedAtUtc, Total)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="730" y="550" width="320" height="145" as="geometry" />
        </mxCell>
        <mxCell id="svc4" value="Parties.Api&#10;Cong 5204&#10;GET /parties/{id}&#10;+ /health/live, /health/ready&#10;Bang: Party (Id, DisplayName)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1080" y="550" width="320" height="145" as="geometry" />
        </mxCell>

        <mxCell id="composebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#999999;dashed=1;verticalAlign=top;fontColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="20" y="710" width="1400" height="140" as="geometry" />
        </mxCell>
        <mxCell id="composelbl" value="docker-compose.deps.yml -- 4 container SQL Server rieng biet + 4 job tao database rong. CHUA co compose/script chay ca 6 service .NET + frontend cung luc (SCRUM-15)." style="text;html=1;fontSize=11;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="40" y="717" width="1350" height="20" as="geometry" />
        </mxCell>

        <mxCell id="db1" value="SQL Server (container rieng)&#10;Database: products -- cong 14331&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="30" y="750" width="320" height="85" as="geometry" />
        </mxCell>
        <mxCell id="db2" value="SQL Server (container rieng)&#10;Database: baskets -- cong 14332&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="380" y="750" width="320" height="85" as="geometry" />
        </mxCell>
        <mxCell id="db3" value="SQL Server (container rieng)&#10;Database: orders -- cong 14333&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="730" y="750" width="320" height="85" as="geometry" />
        </mxCell>
        <mxCell id="db4" value="SQL Server (container rieng)&#10;Database: parties -- cong 14330&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="1080" y="750" width="320" height="85" as="geometry" />
        </mxCell>

        <mxCell id="guardtitle" value="'Luoi an toan' kien truc tu dong -- chay nhu bai test moi lan build, FAIL build khi bi vi pham (6 loai)" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="870" width="1100" height="24" as="geometry" />
        </mxCell>

        <mxCell id="guard1" value="tests/CrossServiceIsolation.Tests&#10;- Service A cam chuoi ket noi CSDL cua service B&#10;- Gateway/BFF cam BAT KY chuoi ket noi nao (khong so huu du lieu)&#10;- Moi service phai co DUNG 1 diem khoi tao DbContext,&#10;  va diem do phai goi RequireTenantId()" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="905" width="460" height="120" as="geometry" />
        </mxCell>
        <mxCell id="guard2" value="tests/StructureConventionTests&#10;FAIL build neu service co thu muc Controllers/, Services/,&#10;Repositories/... Bat buoc moi service co it nhat 1 thu muc&#10;Features/&lt;TenNangLuc&gt; -- ap dung ca Gateway va BFF" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="510" y="905" width="460" height="120" as="geometry" />
        </mxCell>
        <mxCell id="guard3" value="Gateway.Api.UnitTests / IntegrationTests&#10;- RouteConfigurationTests: cau hinh dinh tuyen sai, hoac co&#10;  route di thang toi service nghiep vu&#10;- ForwardingTimeoutBudgetTests: timeout Gateway (10s) &lt; 3s BFF&#10;- MOI: StorefrontCorsTests -- chi web SPA duoc goi qua CORS" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="990" y="905" width="430" height="120" as="geometry" />
        </mxCell>

        <mxCell id="guard4" value="Bff.Api.IntegrationTests/GeneratedContractTests&#10;Tai lieu API sinh tu dong phai khai bao DU ca truong hop loi&#10;(404/502/504), khong chi thanh cong -- nay bao gom ca hop dong&#10;cua /bff/basket, /bff/basket/items, /bff/checkout. Frontend&#10;build LOI ngay neu hop dong lech, khong am tham goi sai." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="1040" width="460" height="110" as="geometry" />
        </mxCell>
        <mxCell id="guard5" value="services/*/tests/*.IntegrationTests -- Testcontainers.MsSql:&#10;chay SQL Server THAT trong container, khong dung gia lap.&#10;Test cua BFF con chay ca 4 service THAT trong bo nho de&#10;kiem tra that su goi duoc, thay vi gia lap cau tra loi." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="510" y="1040" width="460" height="110" as="geometry" />
        </mxCell>
        <mxCell id="guard6" value="*/tests/*.IntegrationTests/TenantEnforcementTests&#10;(1 bo / service nghiep vu)&#10;Request KHONG co X-Tenant-Id / X-Subject-Id phai nhan&#10;loi 500, khong duoc am tham tra ve du lieu" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="990" y="1040" width="430" height="110" as="geometry" />
        </mxCell>

        <mxCell id="e_client_gw" value="HTTP :5300 (CHI Gateway)" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="client" target="gw">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_gw_bff" value="YARP chuyen tiep TAT CA + X-Tenant-Id + X-Subject-Id + X-Correlation-Id" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="gw" target="bff">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_bff_1" value="1: doc gio hang / 4: xoa gio hang" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_3" value="3: tao don hang" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_1p" value="GET /bff/products" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_4" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_db1" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc1" target="db1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db2" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc2" target="db2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db3" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc3" target="db3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db4" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc4" target="db4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_sd_gw" value="tham chieu thu vien (ca 6 service)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="gw">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="920" y="185" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_tn_gw" value="tham chieu thu vien (ca 6 service)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="tenancy" target="gw">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1290" y="185" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_tn_svc1" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="tenancy" target="svc1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="title2" value="PHAN 2 -- CHUA XAY DUNG, MOI LA KE HOACH (Phase 2-5, xem docs/roadmap.md)" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#a03030;" vertex="1" parent="1">
          <mxGeometry x="30" y="1170" width="1000" height="26" as="geometry" />
        </mxCell>

        <mxCell id="futurebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#fafafa;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="20" y="1205" width="1410" height="180" as="geometry" />
        </mxCell>

        <mxCell id="f1" value="Chay toan bo bang&#10;1 lenh duy nhat&#10;(SCRUM-15 -- viec&#10;con lai cuoi cung&#10;cua Giai doan 1)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="35" y="1230" width="215" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f2" value="Identity Server that&#10;(Duende)&#10;(SCRUM-23, Giai&#10;doan 3)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="270" y="1230" width="215" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f3" value="Ngan cach VAT LY&#10;theo tenant (da thu,&#10;da chu dong huy --&#10;xem muc 1.3)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="505" y="1230" width="215" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f4" value="Saga + Outbox that&#10;cho thanh toan&#10;(RabbitMQ+MassTransit,&#10;xem ADR-0011)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="740" y="1230" width="215" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f5" value="Redis, Logistics +&#10;Invoices service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="975" y="1230" width="215" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f6" value="Jenkins CI/CD,&#10;Vault, Unleash,&#10;Pact Broker" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1210" y="1230" width="215" height="80" as="geometry" />
        </mxCell>

        <mxCell id="fnote" value="Cac khoi nay moi la quyet dinh trong ADR / ban ve trong docs/system-design.md, hoac (voi &quot;Ngan cach vat ly theo tenant&quot;) da thu va bi huy giua chung khi trien khai -- chua co dong code chay that nao cho chung. Web SPA, Gateway, BFF, va co che tenant+nguoi goi gia lap DA o Phan 1, khong con la ke hoach nua." style="text;html=1;fontSize=10;fontColor=#a03030;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="35" y="1320" width="1370" height="55" as="geometry" />
        </mxCell>

        <mxCell id="lgtitle" value="Chu giai" style="text;html=1;fontStyle=1;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="30" y="1400" width="100" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="30" y="1430" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" value="Service nghiep vu (so huu du lieu rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1430" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;" vertex="1" parent="1">
          <mxGeometry x="30" y="1460" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" value="Service o bien (khong so huu du lieu)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1460" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc7" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;" vertex="1" parent="1">
          <mxGeometry x="30" y="1490" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc7t" value="Giao dien web (frontend, ngoai .slnx)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1490" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="390" y="1430" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" value="Thu vien dung chung (khong phai service) -- 2 cai" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1430" width="330" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="390" y="1460" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" value="Luoi an toan kien truc (tests) -- 6 loai" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1460" width="330" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="390" y="1490" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" value="Co so du lieu (moi service 1 CSDL rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1490" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="790" y="1430" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6t" value="Chi la ke hoach, hoac da thu va bi huy -- chua co code" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="815" y="1430" width="330" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="790" y="1460" as="sourcePoint" />
            <mxPoint x="840" y="1460" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" value="Goi that luc chay (HTTP / EF Core)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="850" y="1450" width="230" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="790" y="1490" as="sourcePoint" />
            <mxPoint x="840" y="1490" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" value="Tham chieu thu vien luc build" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="850" y="1480" width="230" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

---

## 5. Rủi ro và việc còn treo

**1) [ĐÃ SỬA] Lỗi cấu hình cổng BFF (Baskets↔Orders).** Bản cập nhật trước của tài liệu này từng ghi nhận đây là rủi ro thật, đang treo. Đã xác minh lại trực tiếp: **lỗi này đã được sửa** như một phần của tính năng `004` (nhiệm vụ T010 trong `tasks.md`), file `services/bff/src/Bff.Api/appsettings.Development.json` nay trỏ đúng cổng cho cả Baskets và Orders. **Còn sót một dấu vết tài liệu lỗi thời** (không ảnh hưởng chức năng): `specs/002-gateway-bff-routing/quickstart.md` dòng 56 vẫn ghi cặp cổng cũ bị hoán đổi — nên xem hướng dẫn chạy thử mới trong `specs/004-minimal-shopping-spa/quickstart.md` là bản đúng, bản cũ đã lỗi thời.

**2) Chưa có ngăn cách vật lý dữ liệu theo tenant.** Không đổi từ bản trước — kế hoạch "mỗi tenant một schema riêng" đã thử và huỷ giữa chừng khi làm tính năng `003`. Tài liệu nhiệm vụ của chính tính năng `004` cũng tự liệt kê lại đây là quyết định còn treo, chưa ai xử lý tiếp.

**3) Chưa có cơ chế saga/bù trừ hoặc outbox cho thanh toán — một sai lệch có chủ đích, có ghi chép.** Luồng thanh toán hiện là điều phối đồng bộ 2 bước do BFF thực hiện trực tiếp, không phải mô hình saga theo đúng nguyên tắc IV trong bản thiết kế mục tiêu. Đã được ghi lại minh bạch trong `docs/adr/0011-checkout-orchestration.md`, gắn với công việc tương lai (SCRUM-18, SCRUM-31, Giai đoạn 2 và 4). Rủi ro thực tế ở quy mô hiện tại thấp (không có tải đồng thời cao, không có nhiều consumer downstream), nhưng sẽ cần xử lý trước khi có thêm luồng nghiệp vụ phức tạp hơn (ví dụ Logistics/Invoices).

**4) Chưa có CI/CD, và chưa có lệnh chạy toàn bộ hệ thống bằng một bước (SCRUM-15).** Đây là việc duy nhất còn lại của Giai đoạn 1. Hiện phải tự chạy từng service theo đúng thứ tự.

**5) Chưa có xác thực/phân quyền thật.** Toàn bộ "định danh" (cả tenant lẫn người gọi) hôm nay là giá trị **giả lập cố định** trong cấu hình, luôn xác thực thành công. Thuộc SCRUM-23 (Identity Server thật), Giai đoạn 3.

**6) Một vài ghi chú trạng thái trong tài liệu đặc tả bị lỗi thời (không ảnh hưởng chức năng).** `specs/002-gateway-bff-routing/spec.md` vẫn ghi "Draft"; `specs/004-minimal-shopping-spa/spec.md` cũng vẫn ghi dòng trạng thái "Draft" dù `tasks.md` xác nhận 71/71 nhiệm vụ đã xong. Chỉ là quên cập nhật dòng trạng thái đầu file, không phải khoảng trống thực thi — đã xác minh qua chính nội dung nhiệm vụ và qua chạy thử trực tiếp.

---

## Tổng kết ngắn cho quản lý

- **Giai đoạn 1 (Walking Skeleton) gần như hoàn tất.** Lần đầu tiên có một luồng nghiệp vụ chạy trọn vẹn, kiểm chứng được bằng tay: mở trình duyệt, xem sản phẩm thật, thêm vào giỏ, bấm thanh toán, nhận xác nhận đơn hàng — không có bước nào là giả lập hay mô tả trên giấy.
- Bốn tính năng đã hoàn thành đúng quy trình đặc tả, 100% nhiệm vụ mỗi tính năng: dựng vỏ service (`001`), nối Gateway/BFF (`002`), định danh + tenant giả lập (`003`), và giao diện mua hàng đầu-cuối (`004`).
- Codebase = **23 dự án .NET** (build sạch) + **1 workspace frontend độc lập** (build sạch, 45/45 test qua, đúng ngân sách dung lượng).
- Một rủi ro cấu hình từng treo ở lần cập nhật trước **đã được xác nhận sửa xong**. Vẫn còn ba khoản nợ kỹ thuật thật, đều đã được chính đội phát triển ghi chép công khai (không giấu): chưa ngăn cách vật lý dữ liệu theo tenant, thanh toán chưa dùng saga/outbox theo đúng chuẩn thiết kế, và chưa có xác thực thật.
- Việc còn lại của Giai đoạn 1: **chạy toàn bộ hệ thống bằng một lệnh (SCRUM-15)**. Sau đó, roadmap chuyển sang Giai đoạn 2 (kỷ luật hợp đồng & test) theo đúng kế hoạch đã vạch từ đầu.
- Việc đọc hiểu tiến độ nên dựa vào [`docs/roadmap.md`](roadmap.md) và trạng thái `[X]`/`[ ]` trong từng `specs/*/tasks.md` hơn là dựa vào độ dày tài liệu thiết kế trong `docs/`.
