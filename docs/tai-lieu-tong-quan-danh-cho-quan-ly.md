# Tài liệu kỹ thuật tổng quan — Nền tảng Ecommerce

*Viết cho: quản lý không trực tiếp code .NET. Mục tiêu: hiểu codebase hiện tại đang có gì, các phần liên hệ với nhau ra sao, và vì sao nó được thiết kế như vậy — không cần đọc code.*

*Cập nhật lần cuối: sau khi hoàn thành tính năng `005-one-command-local-run` (SCRUM-15) — **giờ có đúng một lệnh để dựng toàn bộ hệ thống (6 service .NET + giao diện web + cơ sở dữ liệu) trên máy trống**, không cần cài .NET SDK, Node, hay pnpm. Đây là việc cuối cùng còn treo của Giai đoạn 1.*

---

## Điều quan trọng nhất cần biết trước khi đọc tiếp

Đây **không phải** một hệ thống thương mại điện tử đang chạy production. Theo [`docs/roadmap.md`](roadmap.md), đây là **dự án luyện tập cá nhân (solo)** để một người thực hành đầy đủ vòng đời phần mềm (Product Owner → Dev → QA → DevOps → SRE).

**Đánh giá của người viết tài liệu này (không phải một dòng nào trong repo tự tuyên bố điều này — cần nói rõ đây là suy luận, không phải trích dẫn):** với 5 tính năng đã hoàn thành, **toàn bộ "Giai đoạn 1 — Walking Skeleton" (SCRUM-10 đến SCRUM-16) coi như đã xong về mặt kỹ thuật** — có luồng mua hàng thật, có giao diện thật, và giờ có cả cách dựng toàn bộ hệ thống bằng một lệnh. `docs/roadmap.md` chưa được cập nhật để đánh dấu việc này (nó chỉ là danh sách liên kết Jira tĩnh, không có ô đánh dấu hoàn thành), nên nếu quản lý muốn có xác nhận chính thức, nên đối chiếu trực tiếp với bảng Jira SCRUM.

Trong repo có **hai tầng thông tin** dễ nhầm lẫn với nhau:

| Tầng | Là gì | Đã có code chưa? |
|---|---|---|
| **Bản thiết kế mục tiêu** (`docs/system-design.md`, `docs/tech-stack-decisions.md`, `docs/adr/`, `.specify/memory/constitution.md`) | Kiến trúc đầy đủ dự kiến: 6 service nghiệp vụ, Identity Server thật, message queue (saga/outbox), phân vùng dữ liệu vật lý theo tenant, CI/CD | **Một phần nhỏ** — xem cột bên phải cho từng mục |
| **Codebase thực tế hôm nay** (`services/`, `shared/`, `tests/`, `frontend/`, `docker-compose.yml`) | 6 service .NET + 1 giao diện web thật, **dựng được toàn bộ bằng một lệnh trên máy chỉ cần cài Docker** | **Có**, và là toàn bộ những gì tài liệu này mô tả |

**Năm tính năng đã hoàn thành theo đúng quy trình đặc tả (spec-kit)**, mỗi tính năng **100% nhiệm vụ đã đánh dấu hoàn thành**: `001-scaffold-service-shells`, `002-gateway-bff-routing`, `003-stub-identity-tenant-context`, `004-minimal-shopping-spa` (**71/71**), và mới nhất **`005-one-command-local-run`** (**43/43**).

Phần công cụ sinh tự động của Spec-Kit (`.specify/`, các slash-command) không được giải thích ở đây — nhưng nội dung *bên trong* các đặc tả đã hoàn thành ở `specs/` thì có, vì đó là nguồn xác nhận đáng tin cậy cho những gì đã thực sự được xây.

---

## 1. Giải thích codebase hiện tại

### 1.1 Đây là gì, về mặt kỹ thuật

- **Phần .NET** (backend): C# trên .NET 10. [`Ecommerce.slnx`](../Ecommerce.slnx) liệt kê **24 dự án con** (tăng từ 23 — tính năng `005` thêm 1 dự án kiểm tra kiến trúc mới, xem Mục 2.6). `dotnet build`: **sạch, 0 lỗi, 0 cảnh báo**.
- **Phần frontend**: thư mục [`frontend/`](../frontend), workspace pnpm + Turborepo độc lập, **không nằm trong `.slnx`**. Build sạch, 45/45 test qua, đúng ngân sách dung lượng gói JS.
- **Mới từ tính năng `005`:** file [`docker-compose.yml`](../docker-compose.yml) ở gốc repo — **không phải bản cũ chỉ chạy CSDL**, mà dựng **toàn bộ hệ thống**: cả 6 service .NET, giao diện web, và hạ tầng đi kèm, bằng đúng 2 bước (xem Mục 1.8).

### 1.2 Sáu service .NET + 1 giao diện web

**Bốn service nghiệp vụ** (cổng dưới đây chỉ mở khi chạy ở chế độ debug — mặc định chạy bằng một lệnh thì các cổng này **không** lộ ra ngoài, chỉ Gateway mới có):

| Service | Nghiệp vụ | Cổng debug | API đã có |
|---|---|---|---|
| Products | Danh mục sản phẩm | 5088 | `GET /products` — 3 sản phẩm mẫu thật |
| Baskets | Giỏ hàng | 5188 | Giỏ hàng có dòng hàng thật, tự tính tổng |
| Orders | Đơn hàng | 5041 | Tạo đơn hàng từ giỏ, đọc lại đơn theo id |
| Parties | Khách hàng / định danh | 5204 | `GET /parties/{id}` |

**Hai service ở biên:**

| Service | Vai trò | Cổng | Ghi chú |
|---|---|---|---|
| Gateway | Cửa vào duy nhất; gán tenant + người gọi giả lập | **5300** — **cổng backend DUY NHẤT được công bố ra ngoài** khi chạy bằng một lệnh | Tự khởi động sau khi BFF báo sẵn sàng |
| BFF | Gộp dữ liệu; điều phối thanh toán | 5301 (chỉ mở ở chế độ debug) | Tự khởi động sau khi cả 4 service nghiệp vụ báo sẵn sàng |

**Giao diện web:**

| Thành phần | Vai trò | Cổng |
|---|---|---|
| Storefront (chạy bằng một lệnh, container nginx) | Website React đã build sẵn — 3 màn hình: Sản phẩm, Giỏ hàng, Xác nhận | **4173** |
| `frontend/apps/web` (cách chạy thay thế, dành cho lúc đang sửa giao diện) | Vite dev server | 5173 |

Dù chạy theo cách nào, giao diện **chỉ gọi đúng một địa chỉ: Gateway** — ép buộc bằng cấu trúc code, không phải quy ước. Gateway chỉ chấp nhận request CORS từ đúng 2 địa chỉ này (5173 và 4173), có test riêng canh giữ.

### 1.3 Dữ liệu đã có thật, nhưng vẫn còn một khoảng nợ kỹ thuật cũ

Không đổi so với bản trước: **Products** có 3 dòng dữ liệu mẫu thật; **Basket** có dòng hàng thật, tự tính tổng; giá luôn lấy lại từ server, client không tự đặt được. **Khoảng nợ kỹ thuật cũ vẫn còn nguyên:** kế hoạch "mỗi tenant một schema CSDL riêng" (từ tính năng `003`) vẫn **chưa được triển khai** — chỉ có chốt chặn logic, chưa có ngăn cách vật lý.

### 1.4 Một lượt mua hàng đi như thế nào (không đổi)

```
Trinh duyet (Storefront, cong 4173 hoac Vite dev :5173)
      │  CHI biet DUY NHAT dia chi Gateway (cong 5300)
      ▼
  Gateway (5300)     ── xac thuc gia lap LUON thanh cong, gan tenant "contoso"
      │                  + nguoi dung "phase1-stub-user", chuyen tiep nguyen ven
      ▼
  BFF (5301)          ── POST /bff/checkout: 1. doc gio hang  2. gio RONG -> 409, dung ngay
      │                  3. tao don hang (goi Orders)  4. XOA gio hang -- CHI SAU KHI co don that
      ▼
  Orders             ── tao ban ghi don hang, tra ve tong tien
      ▼
  SQL Server "orders"
```

Đã đo thật: giỏ 2 quyển sổ tay + 1 tạp dề → thanh toán → **tổng tiền $59.25**, đọc lại đúng, giỏ hàng rỗng sau đó, thanh toán lần hai trên giỏ rỗng bị chặn 409.

### 1.5 Định danh & ranh giới "tenant" (không đổi)

`X-Tenant-Id` (tenant cố định `contoso`) và `X-Subject-Id` (người gọi cố định `phase1-stub-user`) — cả hai do Gateway gán, lan truyền qua BFF xuống service, chặn cứng nếu thiếu.

### 1.6 Giao diện web — ba màn hình (không đổi)

`/` (sản phẩm), `/basket` (giỏ hàng), `/confirmation` (xác nhận). Có đầu tư về khả năng tiếp cận và ngân sách dung lượng gói JS. Mã gọi API sinh tự động từ hợp đồng OpenAPI của BFF.

### 1.7 Cách tổ chức code trong mỗi service (không đổi)

Chia theo tính năng ("vertical-slice"), ép buộc bằng máy, áp dụng cho cả Gateway và BFF.

### 1.8 Dựng toàn bộ hệ thống bằng một lệnh (mới — tính năng `005`)

Đây là thay đổi quan trọng nhất của lần cập nhật này: **lần đầu tiên một người không biết gì về .NET/Node cũng dựng được toàn bộ hệ thống để xem thử**, chỉ cần máy có cài Docker.

**Đúng 2 bước** (đo được: lần đầu dưới 10 phút, các lần sau dưới 3 phút):
```
cp .env.example .env      # không cần sửa gì
./scripts/up.sh            # hoặc ./scripts/up.ps1 trên Windows
```

Script `up` tự làm 3 việc trước khi bất kỳ container nào khởi động, để không ai phải tự đoán lỗi từ thông báo khó hiểu của Docker Compose:
1. Kiểm tra Docker đã cài và daemon đang chạy
2. Kiểm tra file `.env` tồn tại
3. Kiểm tra Docker được cấp đủ RAM (tối thiểu 6GB)

Thiếu điều kiện nào, script dừng ngay với đúng một câu thông báo rõ ràng nêu tên thứ còn thiếu — không chạy nửa chừng rồi lỗi mơ hồ.

**Toàn bộ hệ thống dựng lên gồm 15 thành phần**: 4 service nghiệp vụ, Gateway, BFF, giao diện web (storefront), 1 SQL Server dùng chung (mỗi service một database bên trong, thay vì 4 container SQL riêng như cách debug cũ), 4 "migrator" chạy đúng một lần để tạo bảng + nạp dữ liệu mẫu rồi tự thoát, Redis, RabbitMQ, và một bộ thu thập log/theo dõi (OTel Collector). Redis và RabbitMQ **chạy sẵn nhưng chưa có service nào dùng tới** — hạ tầng chuẩn bị trước cho các tính năng tương lai (Mục 5), không phải lãng phí.

**Thứ tự khởi động được xếp hàng chờ nhau bằng health-check**, không phải đoán bằng cách chờ vài giây: migrator đợi SQL Server sẵn sàng → service nghiệp vụ đợi migrator của chính nó chạy xong → BFF đợi cả 4 service nghiệp vụ sẵn sàng → Gateway đợi BFF sẵn sàng. Giao diện web không cần đợi gì (chỉ là file tĩnh).

**Một chi tiết vận hành tinh tế đã được xử lý:** ngay sau khi toàn bộ container báo "khỏe mạnh", script tự gửi vài request thử qua Gateway trước khi báo "hệ thống đã sẵn sàng". Lý do: lần gọi *thật* đầu tiên vào một container vừa khởi động phải trả giá cho việc biên dịch mã lần đầu, dựng mô hình dữ liệu, mở kết nối CSDL — tất cả cộng lại có thể vượt quá ngân sách 3 giây của BFF (Mục 2.4), khiến khách hàng đầu tiên gặp lỗi dù hệ thống đã "khỏe mạnh" theo Docker. Đây không phải phòng ngừa lý thuyết — chính là lỗi thật đã gặp và sửa trong lúc làm tính năng này.

**Dừng và làm lại:**
- `./scripts/down.sh` — dừng, **giữ nguyên dữ liệu** (đơn hàng, giỏ hàng còn nguyên khi bật lại)
- `./scripts/reset.sh` — dừng và **xoá sạch dữ liệu**, lần bật lại tiếp theo giống hệt lần chạy đầu tiên

**Cách debug khi cần:** `./scripts/up.sh --debug` mở thêm các cổng nội bộ (BFF, từng service, giao diện quản trị RabbitMQ) để soi vào bên trong — không dùng mặc định, chỉ bật khi cần.

**Một lỗi thật đã bị bắt nhờ thử đi thử lại (không phải chỉ chạy một lần rồi kết luận "ổn"):** khi kiểm tra 10 lần dừng-bật liên tiếp, 4/10 lần thất bại vì cách kiểm tra "SQL Server đã sẵn sàng" trước đó chỉ hỏi "server có phản hồi không", trong khi từng database bên trong (products, baskets, orders, parties) có thể vẫn đang tự phục hồi — dẫn tới lỗi "database đã tồn tại" ngẫu nhiên. Đã sửa bằng cách đổi câu kiểm tra sang "mọi database đều ở trạng thái ONLINE", và cho phép tự thử lại 3 lần cho bước tạo bảng. Sau khi sửa: **10/10 lần dừng-bật đều sạch**, không container mồ côi, không cổng bị giữ.

---

## 2. Các dự án/component và cách chúng phụ thuộc lẫn nhau

### 2.1–2.5 (không đổi so với bản trước)

Bản đồ quan hệ giữa các service, `shared/ServiceDefaults`, Gateway, BFF (điều phối thanh toán 3 bước, ADR-0011), `shared/Tenancy` — giữ nguyên như lần cập nhật trước, không có thay đổi từ tính năng `005`.

### 2.6 Bảy "lưới an toàn" kiến trúc tự động (tăng từ 6 — có thêm 1 loại mới)

| Cơ chế | Chặn điều gì |
|---|---|
| `tests/CrossServiceIsolation.Tests` | Service A cầm chuỗi kết nối CSDL của service B; Gateway/BFF cầm bất kỳ chuỗi kết nối nào; mỗi service phải có đúng 1 điểm khởi tạo `DbContext`, gọi `RequireTenantId()` |
| `tests/StructureConventionTests` | Phá vỡ quy ước tổ chức code theo tính năng |
| `Gateway.Api` Tests | Cấu hình định tuyến sai; timeout Gateway thấp hơn ngân sách BFF |
| `Bff.Api.IntegrationTests/GeneratedContractTests` | Tài liệu API sinh tự động thiếu trường hợp lỗi |
| `*/tests/*.IntegrationTests/TenantEnforcementTests` | Request thiếu `X-Tenant-Id`/`X-Subject-Id` lại được phục vụ bình thường |
| **`tests/ContainerConventionTests` (MỚI, tính năng `005`)** | **Dockerfile của một service thiếu lệnh COPY cho thư viện dùng chung mà chính service đó tham chiếu trong code** — nghe rất kỹ thuật, nhưng hậu quả rất thật: bài test này **đã bắt được 5/6 image build lỗi** ngay khi được thêm vào, vì `Dockerfile` của 5 service quên copy thư mục `shared/Tenancy` dù code đã dùng thư viện này từ tính năng `003`. Không ai phát hiện ra trước đó đơn giản vì **chưa từng có ai build container nào cho tới lúc này** — một ví dụ rõ ràng cho việc "test qua hết" không đồng nghĩa "chạy được trong container" |

### 2.7 Hạ tầng cục bộ và triển khai — viết lại hoàn toàn cho tính năng `005`

- **`docker-compose.yml`** (mới, ở gốc repo) — dựng **toàn bộ** hệ thống bằng một lệnh (Mục 1.8). Đây là file chính, khác với `docker-compose.deps.yml` cũ (chỉ 4 container CSDL riêng biệt, vẫn còn giữ lại để dùng khi cần chạy từng service riêng lẻ lúc debug).
- Cả 6 service .NET và giao diện web **đều đã có Dockerfile thật, build được** — trước tính năng `005`, một số Dockerfile này thực ra **không build được** (thiếu copy thư viện dùng chung), chỉ là chưa ai thử.
- **Vẫn chưa có CI/CD** (không có `.github/workflows`, chưa có cấu hình Jenkins) — mọi kiểm tra (bao gồm cả "lưới an toàn kiến trúc" 7 loại) vẫn chạy thủ công trên máy cá nhân.

---

## 3. Mục đích từng phần + các tình huống thực tế được giải quyết

### 3.1 (không đổi — thêm 1 dòng)

Bổ sung vào bảng mục đích từng dự án ở bản trước: `tests/ContainerConventionTests` — đảm bảo Dockerfile của một service không "quên" thư viện dùng chung mà code của nó thực sự cần.

### 3.2 Mười tình huống thật mà giải pháp này giải quyết

**1–8)** Giữ nguyên như bản trước (ngăn rò rỉ CSDL chéo service; health-check tách biệt; giữ chất lượng kiến trúc theo thời gian; một service chết không kéo sập cả hệ thống; lỗi có mã tra cứu; frontend không bỏ sót trường hợp lỗi; "quên xác định đang phục vụ ai" thành lỗi ồn ào; thanh toán trùng lặp không tạo hai đơn hàng).

**9) "Test đều xanh" không có nghĩa là "chạy được" — cho tới khi có ai thực sự thử đóng gói.**
Trong nhiều tháng, toàn bộ 96+ bài test của hệ thống đều báo xanh, `dotnet build` luôn sạch — nhưng **5 trên 6 Dockerfile thực ra không build được**, vì thiếu một dòng copy thư viện dùng chung. Không có bài test .NET nào từng phát hiện ra, đơn giản vì không có bài test .NET nào từng thử build container. Chỉ khi tính năng `005` thực sự cần chạy container thật, lỗi mới lộ ra — và ngay khi lộ ra, đã được biến thành một bài test tự động (`ContainerConventionTests`) để không bao giờ tái diễn âm thầm. Bài học quản lý: "test xanh" chỉ chứng minh những gì test đó *có kiểm tra*, không hơn.

**10) Một lỗi chỉ xuất hiện khi thử lại nhiều lần, không xuất hiện ở lần chạy đầu tiên — và cách duy nhất tìm ra nó là chủ động thử lại nhiều lần.**
Cách kiểm tra "CSDL đã sẵn sàng chưa" tưởng như đơn giản (hỏi server có phản hồi không) thực ra **sai trong khoảng 40% trường hợp** khi khởi động lại: server phản hồi trước khi từng database bên trong phục hồi xong, dẫn tới lỗi ngẫu nhiên "database đã tồn tại". Nếu chỉ thử một lần rồi kết luận "chạy được", lỗi này sẽ ngủ yên và một ngày nào đó xuất hiện ngẫu nhiên trong tay người dùng thật, không cách nào lặp lại để điều tra. Chỉ vì tính năng `005` chủ động yêu cầu thử "10 lần dừng-bật liên tiếp" thay vì "chạy một lần cho có", lỗi mới lộ diện đủ để sửa tận gốc.

---

## 4. Sơ đồ kiến trúc hiện tại (dùng được với draw.io)

Sơ đồ vẽ **đúng những gì đang có trong code hôm nay**, bao gồm toàn bộ topology của `docker-compose.yml` (migrator, Redis/RabbitMQ chưa dùng, OTel Collector đã dùng). Phần dưới (viền đứt màu đỏ) là những gì vẫn chỉ nằm trên giấy.

> **Lưu ý phân biệt:** repo có sẵn 3 sơ đồ khác ở [`docs/system-design.md`](system-design.md) — nhưng chúng vẽ **kiến trúc mục tiêu đầy đủ**, không phải trạng thái hiện tại.

### Cách dùng
1. Mở [app.diagrams.net](https://app.diagrams.net) (draw.io).
2. Vào menu **Extras → Edit Diagram…**
3. Xoá nội dung trống, dán toàn bộ khối XML bên dưới vào, bấm **Save/OK**.

*(Hoặc mở trực tiếp file [`docs/diagrams/current-state-architecture.drawio`](diagrams/current-state-architecture.drawio) đã có sẵn trong repo — nội dung giống hệt khối XML bên dưới.)*

```xml
<mxfile host="app.diagrams.net" modified="2026-08-18T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="current-state-005" name="Trang thai hien tai - sau 005">
    <mxGraphModel dx="1500" dy="1200" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1500" pageHeight="1820" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="title1" value="PHAN 1 -- DA CO TRONG CODE HOM NAY (24 du an .NET + 1 workspace frontend; 1 LENH DUY NHAT chay toan bo stack)" style="text;html=1;fontStyle=1;fontSize=15;fontColor=#2d6a2d;" vertex="1" parent="1">
          <mxGeometry x="30" y="10" width="1400" height="26" as="geometry" />
        </mxCell>

        <mxCell id="client" value="Storefront (frontend/apps/web)&#10;CACH 1 -- 1 lenh, docker (moi tu 005): container nginx, cong 4173,&#10;  tu Dockerfile rieng, CHI goi Gateway qua cong 5300&#10;CACH 2 -- dev thu cong: pnpm dev, Vite, cong 5173&#10;3 man hinh: San pham (/) - Gio hang (/basket) - Xac nhan (/confirmation)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="50" width="460" height="115" as="geometry" />
        </mxCell>

        <mxCell id="sd" value="shared/ServiceDefaults&#10;(thu vien dung chung, KHONG phai service)&#10;- OpenTelemetry: log / trace / metric -&gt; OTel Collector that&#10;- Correlation-Id (X-Correlation-Id): sinh o Gateway,&#10;  ghi vao request de moi hop sau dung chung 1 ma" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="860" y="50" width="290" height="115" as="geometry" />
        </mxCell>

        <mxCell id="tenancy" value="shared/Tenancy&#10;(thu vien dung chung, KHONG phai service)&#10;- TenantContext.RequireTenantId()&#10;- CallerContext.RequireSubjectId()&#10;- Header: X-Tenant-Id + X-Subject-Id&#10;- Chan MOI ket noi CSDL neu thieu 1 trong 2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1170" y="50" width="260" height="115" as="geometry" />
        </mxCell>

        <mxCell id="gw" value="Gateway.Api (services/gateway) -- YARP&#10;Cong 5300 -- DUY NHAT cong backend duoc cong bo ra ngoai&#10;Bang route = 1 dong duy nhat: MOI duong dan -&gt; BFF&#10;StubIdentity: gan co dinh tenant &quot;contoso&quot; + nguoi dung&#10;&quot;phase1-stub-user&quot;, ghi de X-Tenant-Id + X-Subject-Id&#10;Docker: tu khoi dong sau khi BFF bao healthy (/health/ready)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="185" width="460" height="140" as="geometry" />
        </mxCell>

        <mxCell id="gwnote" value="CORS: CHI 2 origin duoc phep goi -- localhost:5173 (dev) va localhost:4173&#10;(storefront docker). StorefrontCorsTests canh dung dieu nay.&#10;&#10;Gateway tu healthcheck bang /health/live (con song), KHONG phai /health/ready&#10;-- de Gateway van dung vung khi 1 service phia sau dang loi, thay vi tu&#10;rut minh ra khoi vong lap va lam ca he thong sap theo." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="510" y="185" width="660" height="140" as="geometry" />
        </mxCell>

        <mxCell id="bff" value="Bff.Api (services/bff) -- Backend For Frontend&#10;Cong 5301 -- CHI mo trong che do debug, khong cong bo mac dinh&#10;GET /bff/products, GET/POST /bff/basket, POST /bff/basket/items,&#10;POST /bff/checkout, GET /bff/orders/{id}&#10;Docker: tu khoi dong sau khi CA 4 service nghiep vu bao healthy" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="345" width="460" height="130" as="geometry" />
        </mxCell>

        <mxCell id="bffnote" value="Ngan sach thoi gian moi loi goi ra ngoai: 1s/lan thu (toi da 2 lan lai) | toi da 3s tong cong | cau dao ngat sau 10s loi lien tuc&#10;502 = downstream khong ket noi duoc    504 = vuot thoi gian cho    500 = loi cua chinh BFF    404 = khong tim thay (KHONG phai loi)&#10;&#10;POST /bff/checkout -- dieu phoi dong bo 3 buoc theo DUNG thu tu (ADR-0011): 1. doc gio hang  2. gio RONG -&gt; 409, dung ngay&#10;3. tao don hang (goi Orders)  4. XOA gio hang -- CHI SAU KHI da co don that. KHONG phai saga/outbox chuan -- sai lech co ghi chep." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="510" y="345" width="890" height="130" as="geometry" />
        </mxCell>

        <mxCell id="svcnote" value="4 service nghiep vu HOAN TOAN khong biet den nhau: khong cai nao goi cai nao, khong cai nao tham chieu code cua cai nao." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="30" y="490" width="1390" height="18" as="geometry" />
        </mxCell>

        <mxCell id="mig1" value="products-migrate&#10;(1 lan, cho sqlserver&#10;healthy, ap migration&#10;+ seed 3 san pham)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" vertex="1" parent="1">
          <mxGeometry x="30" y="515" width="320" height="55" as="geometry" />
        </mxCell>
        <mxCell id="mig2" value="baskets-migrate&#10;(1 lan, cho sqlserver&#10;healthy, ap migration)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" vertex="1" parent="1">
          <mxGeometry x="380" y="515" width="320" height="55" as="geometry" />
        </mxCell>
        <mxCell id="mig3" value="orders-migrate&#10;(1 lan, cho sqlserver&#10;healthy, ap migration)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" vertex="1" parent="1">
          <mxGeometry x="730" y="515" width="320" height="55" as="geometry" />
        </mxCell>
        <mxCell id="mig4" value="parties-migrate&#10;(1 lan, cho sqlserver&#10;healthy, ap migration)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" vertex="1" parent="1">
          <mxGeometry x="1080" y="515" width="320" height="55" as="geometry" />
        </mxCell>
        <mxCell id="mignote" value="4 &quot;migrator&quot; nay chay UNG 1 LAN moi lan len stack, tu Dockerfile cua chinh service (target rieng), roi tu thoat. Service nghiep vu&#10;tuong ung CHI tu khoi dong sau khi migrator cua no bao &quot;hoan tat&quot; (docker depends_on: service_completed_successfully)." style="text;html=1;fontSize=9;fontColor=#888888;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="30" y="573" width="1390" height="28" as="geometry" />
        </mxCell>

        <mxCell id="svc1" value="Products.Api&#10;Cong 5088 (chi mo trong che do debug)&#10;GET /products -- 3 san pham mau that&#10;+ /health/live, /health/ready&#10;Bang: Product (Id, Name, Price)&#10;Chan CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="610" width="320" height="140" as="geometry" />
        </mxCell>
        <mxCell id="svc2" value="Baskets.Api&#10;Cong 5188 (chi mo trong che do debug)&#10;Gio hang cua nguoi goi (CustomerRef)&#10;+ /health/live, /health/ready&#10;Bang: Basket (Total tinh tai cho) + BasketLineItem&#10;Chan CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="380" y="610" width="320" height="140" as="geometry" />
        </mxCell>
        <mxCell id="svc3" value="Orders.Api&#10;Cong 5041 (chi mo trong che do debug)&#10;Tao don hang tu dong gio hang; doc lai theo id&#10;+ /health/live, /health/ready&#10;Bang: Order (Id, PlacedAtUtc, Total)&#10;Chan CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="730" y="610" width="320" height="140" as="geometry" />
        </mxCell>
        <mxCell id="svc4" value="Parties.Api&#10;Cong 5204 (chi mo trong che do debug)&#10;GET /parties/{id}&#10;+ /health/live, /health/ready&#10;Bang: Party (Id, DisplayName)&#10;Chan CSDL neu thieu X-Tenant-Id / X-Subject-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1080" y="610" width="320" height="140" as="geometry" />
        </mxCell>

        <mxCell id="composebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#999999;dashed=1;verticalAlign=top;fontColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="20" y="765" width="1400" height="110" as="geometry" />
        </mxCell>
        <mxCell id="composelbl" value="docker-compose.yml (MOI, project &quot;ecomerce-stack&quot;) -- 1 SQL Server dung chung, moi service 1 database rieng ben trong (khong con 4 container SQL rieng biet o duong dan nay). docker-compose.deps.yml cu (4 container SQL rieng) van con, dung khi chay tung service rieng le de debug." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="40" y="772" width="1360" height="35" as="geometry" />
        </mxCell>

        <mxCell id="db1" value="Database: products&#10;(trong 1 container SQL&#10;Server dung chung)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="30" y="815" width="320" height="55" as="geometry" />
        </mxCell>
        <mxCell id="db2" value="Database: baskets&#10;(trong 1 container SQL&#10;Server dung chung)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="380" y="815" width="320" height="55" as="geometry" />
        </mxCell>
        <mxCell id="db3" value="Database: orders&#10;(trong 1 container SQL&#10;Server dung chung)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="730" y="815" width="320" height="55" as="geometry" />
        </mxCell>
        <mxCell id="db4" value="Database: parties&#10;(trong 1 container SQL&#10;Server dung chung)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="1080" y="815" width="320" height="55" as="geometry" />
        </mxCell>

        <mxCell id="infratitle" value="Ha tang chay kem trong docker-compose.yml -- khong phai service nghiep vu" style="text;html=1;fontStyle=1;fontSize=11;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="885" width="700" height="20" as="geometry" />
        </mxCell>
        <mxCell id="otel" value="OTel Collector&#10;DA duoc dung that: nhan log/trace/metric&#10;tu ca 6 service qua ServiceDefaults.&#10;Khong the healthcheck (image khong co shell)." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="910" width="450" height="80" as="geometry" />
        </mxCell>
        <mxCell id="redis" value="Redis&#10;Chay va co healthcheck, nhung CHUA co&#10;service nao ket noi vao (co chu dich,&#10;xem FR-017 cua tinh nang 005)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#999999;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="500" y="910" width="440" height="80" as="geometry" />
        </mxCell>
        <mxCell id="rabbitmq" value="RabbitMQ&#10;Chay va co healthcheck, nhung CHUA co&#10;service nao ket noi vao -- danh cho&#10;saga/outbox tuong lai (xem Phan 2)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#999999;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="960" y="910" width="440" height="80" as="geometry" />
        </mxCell>

        <mxCell id="guardtitle" value="'Luoi an toan' kien truc tu dong -- chay nhu bai test moi lan build, FAIL build khi bi vi pham (7 loai)" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="1005" width="1100" height="24" as="geometry" />
        </mxCell>

        <mxCell id="guard1" value="tests/CrossServiceIsolation.Tests&#10;- Service A cam chuoi ket noi CSDL cua service B&#10;- Gateway/BFF cam BAT KY chuoi ket noi nao&#10;- Moi service phai co DUNG 1 diem khoi tao&#10;  DbContext, goi RequireTenantId()" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="1040" width="345" height="115" as="geometry" />
        </mxCell>
        <mxCell id="guard2" value="tests/StructureConventionTests&#10;FAIL build neu service co thu muc Controllers/,&#10;Services/, Repositories/... Bat buoc moi service&#10;co it nhat 1 thu muc Features/&lt;TenNangLuc&gt;" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="385" y="1040" width="345" height="115" as="geometry" />
        </mxCell>
        <mxCell id="guard3" value="Gateway.Api Tests&#10;- RouteConfigurationTests: sai chinh ta route,&#10;  hoac route di thang toi service nghiep vu&#10;- ForwardingTimeoutBudgetTests: timeout Gateway&#10;  (10s) &lt; ngan sach 3s cua BFF" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="740" y="1040" width="345" height="115" as="geometry" />
        </mxCell>
        <mxCell id="guard4" value="Bff.Api.IntegrationTests/&#10;GeneratedContractTests&#10;Tai lieu API sinh tu dong phai khai bao DU ca&#10;404/502/504, khong chi thanh cong -- frontend&#10;build LOI ngay neu hop dong lech" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1095" y="1040" width="335" height="115" as="geometry" />
        </mxCell>

        <mxCell id="guard5" value="services/*/tests/*.IntegrationTests -- Testcontainers.MsSql: chay SQL Server THAT trong container, khong dung gia lap.&#10;Test cua BFF con chay ca 4 service THAT trong bo nho de kiem tra that su goi duoc." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="1165" width="460" height="90" as="geometry" />
        </mxCell>
        <mxCell id="guard6" value="*/tests/*.IntegrationTests/TenantEnforcementTests (1 bo / service nghiep vu)&#10;Request KHONG co X-Tenant-Id / X-Subject-Id phai nhan loi 500, khong duoc am tham tra ve du lieu." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="500" y="1165" width="460" height="90" as="geometry" />
        </mxCell>
        <mxCell id="guard7" value="tests/ContainerConventionTests (MOI tu 005)&#10;Moi Dockerfile phai COPY DU cac thu vien shared/* ma .csproj tham chieu -- da bat 5/6 image&#10;khong build duoc (thieu shared/Tenancy trong Dockerfile) truoc khi sua." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="970" y="1165" width="460" height="90" as="geometry" />
        </mxCell>

        <mxCell id="opstitle" value="Van hanh: 3 script + kiem tra dieu kien truoc + &quot;lam am&quot; sau khi len (moi tu 005)" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="1275" width="1000" height="24" as="geometry" />
        </mxCell>
        <mxCell id="opsbox" value="scripts/up.sh | up.ps1 -- kiem tra TRUOC khi khoi dong bat ky container nao: Docker da cai, Docker daemon dang chay, file .env ton tai, RAM Docker &gt;= 6GB.&#10;Chay &quot;docker compose up --build --wait&quot;. Sau khi TAT CA bao healthy, tu goi thu 3 request qua Gateway (/bff/products, /bff/basket, /bff/orders/...) de&#10;&quot;lam am&quot; JIT/EF Core/connection-pool -- neu thieu buoc nay, request THAT dau tien cua khach gap loi 504 (vuot ngan sach 3s cua BFF).&#10;Day la loi THAT da gap va sua trong tinh nang 005, khong phai gia dinh.&#10;&#10;scripts/down.sh | down.ps1 -- dung stack, GIU du lieu (volume khong bi xoa).      scripts/reset.sh | reset.ps1 -- dung stack VA XOA sach du lieu.&#10;Che do debug (up.sh --debug / up.ps1 -PublishInternalPorts, dua tren docker-compose.debug.yml) -- mo them cong noi bo (BFF, tung service, RabbitMQ UI) de debug." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#eef7ee;strokeColor=#2d6a2d;fontSize=10;align=left;spacingLeft=8;" vertex="1" parent="1">
          <mxGeometry x="30" y="1305" width="1390" height="115" as="geometry" />
        </mxCell>

        <mxCell id="e_client_gw" value="HTTP :5300 (CORS: chi 5173 / 4173)" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="client" target="gw">
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

        <mxCell id="e_mig1_svc1" value="docker depends_on: hoan tat" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="mig1" target="svc1">
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
              <mxPoint x="920" y="165" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_tn_gw" value="tham chieu thu vien (ca 6 service)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="tenancy" target="gw">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1300" y="165" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_sd_otel" value="OTLP export (ca 6 service, qua ServiceDefaults)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="otel">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="title2" value="PHAN 2 -- CHUA XAY DUNG, MOI LA KE HOACH (Phase 2-5, xem docs/roadmap.md)" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#a03030;" vertex="1" parent="1">
          <mxGeometry x="30" y="1440" width="1000" height="26" as="geometry" />
        </mxCell>

        <mxCell id="futurebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#fafafa;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="20" y="1475" width="1400" height="130" as="geometry" />
        </mxCell>

        <mxCell id="f1" value="Identity Server that&#10;(Duende)&#10;(SCRUM-23, Giai&#10;doan 3)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="40" y="1500" width="255" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f2" value="Ngan cach VAT LY&#10;theo tenant (da thu,&#10;da chu dong huy --&#10;xem muc 1.3)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="320" y="1500" width="255" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f3" value="Saga + Outbox that cho&#10;thanh toan -- THUC SU&#10;dung RabbitMQ dang&#10;chay ronq (ADR-0011)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="600" y="1500" width="255" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f4" value="Logistics + Invoices&#10;service (se dung Redis/&#10;RabbitMQ dang chay ronq&#10;nhung con trong)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="880" y="1500" width="255" height="80" as="geometry" />
        </mxCell>
        <mxCell id="f5" value="Jenkins CI/CD,&#10;Vault, Unleash,&#10;Pact Broker" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1160" y="1500" width="240" height="80" as="geometry" />
        </mxCell>

        <mxCell id="fnote" value="Redis va RabbitMQ DA CHAY that trong docker-compose.yml tu tinh nang 005 (ha tang san sang), nhung van CHUA co service nao ket noi vao -- nghiep vu dung toi no van la ke hoach.&#10;Chay-toan-bo-bang-1-lenh (SCRUM-15) va Web SPA (SCRUM-14) DA chuyen len Phan 1, khong con la ke hoach nua -- viec con lai cuoi cung cua Giai doan 1 (Walking Skeleton) coi nhu da xong." style="text;html=1;fontSize=10;fontColor=#a03030;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="35" y="1610" width="1360" height="45" as="geometry" />
        </mxCell>

        <mxCell id="lgtitle" value="Chu giai" style="text;html=1;fontStyle=1;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="30" y="1670" width="100" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="30" y="1700" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" value="Service nghiep vu (so huu du lieu rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1700" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;" vertex="1" parent="1">
          <mxGeometry x="30" y="1730" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" value="Service o bien (khong so huu du lieu)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1730" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc7" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;" vertex="1" parent="1">
          <mxGeometry x="390" y="1700" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc7t" value="Giao dien web (frontend, ngoai .slnx)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1700" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="390" y="1730" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" value="Thu vien dung chung / ha tang DA dung that (OTel)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1730" width="340" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc8" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;" vertex="1" parent="1">
          <mxGeometry x="770" y="1700" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc8t" value="Migrator (1 lan) / ha tang chay nhung CHUA dung (Redis, RabbitMQ)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="795" y="1700" width="400" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="770" y="1730" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" value="Luoi an toan kien truc (tests) -- 7 loai" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="795" y="1730" width="330" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="1200" y="1700" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" value="Co so du lieu" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1225" y="1700" width="160" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="1200" y="1730" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6t" value="Chi la ke hoach -- chua co code" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1225" y="1730" width="230" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="30" y="1770" as="sourcePoint" />
            <mxPoint x="80" y="1770" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" value="Goi that luc chay (HTTP / EF Core)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="90" y="1760" width="260" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="400" y="1770" as="sourcePoint" />
            <mxPoint x="450" y="1770" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" value="Tham chieu thu vien / phu thuoc luc build hoac luc khoi dong (docker depends_on)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="460" y="1760" width="480" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

---

## 5. Rủi ro và việc còn treo

**1) [ĐÃ XONG — không còn là rủi ro] Chưa có lệnh chạy toàn bộ hệ thống.** Đây là việc cuối cùng của Giai đoạn 1, nay đã xong (Mục 1.8). Không còn trong danh sách rủi ro.

**2) Chưa có ngăn cách vật lý dữ liệu theo tenant.** Không đổi — kế hoạch "mỗi tenant một schema riêng" đã thử và huỷ giữa chừng khi làm tính năng `003`, vẫn còn treo.

**3) Chưa có cơ chế saga/bù trừ hoặc outbox cho thanh toán.** Không đổi — vẫn là điều phối đồng bộ do BFF tự làm, ghi chép minh bạch trong `docs/adr/0011-checkout-orchestration.md`. **Đáng chú ý thêm:** RabbitMQ giờ đã **chạy sẵn** trong stack (từ tính năng `005`) nhưng **chưa có gì kết nối vào** — hạ tầng đã có trước, nghiệp vụ dùng tới nó vẫn là việc tương lai (SCRUM-18/31).

**4) Chưa có CI/CD.** Không đổi. Mọi kiểm tra (kể cả 7 loại "lưới an toàn kiến trúc") vẫn chạy thủ công.

**5) Chưa có xác thực/phân quyền thật.** Không đổi — toàn bộ định danh vẫn là giá trị giả lập cố định. Thuộc SCRUM-23, Giai đoạn 3.

**6) Vài ghi chú trạng thái trong tài liệu đặc tả bị lỗi thời (không ảnh hưởng chức năng).** `specs/002`, `specs/004`, và giờ cả **`specs/005-one-command-local-run/spec.md`** đều vẫn ghi dòng trạng thái đầu file là "Draft" dù `tasks.md` xác nhận đã hoàn thành 100%. Một khuôn mẫu lặp lại qua các tính năng — đáng để nêu ra như một thói quen tài liệu cần sửa (cập nhật dòng trạng thái khi đóng tính năng), không ảnh hưởng gì tới việc tính năng có chạy được hay không.

---

## Tổng kết ngắn cho quản lý

- **Giai đoạn 1 (Walking Skeleton) coi như đã hoàn tất về mặt kỹ thuật.** Giờ đây, một người chỉ cần cài Docker, gõ đúng 2 lệnh, là có toàn bộ hệ thống — 6 service .NET, giao diện web, cơ sở dữ liệu — chạy thật trên máy mình, không cần biết .NET hay Node là gì. Đây là bước cuối cùng còn thiếu từ các lần cập nhật trước.
- Năm tính năng đã hoàn thành đúng quy trình đặc tả, 100% nhiệm vụ mỗi tính năng: dựng vỏ service, nối Gateway/BFF, định danh + tenant giả lập, giao diện mua hàng đầu-cuối, và chạy toàn bộ bằng một lệnh.
- Codebase = **24 dự án .NET** (build sạch) + **1 workspace frontend độc lập** (build sạch) + **1 file `docker-compose.yml` dựng cả hệ thống**.
- Hai bài học quản lý đáng nhớ nhất từ lần cập nhật này: **"test xanh" không chứng minh "chạy được trong container"** (5/6 image từng không build được mà không ai biết, cho tới khi thực sự thử), và **một lỗi vận hành thật chỉ lộ ra khi chủ động thử lại nhiều lần**, không phải chạy một lần cho có. Cả hai đều đã được đội phát triển tìm ra và biến thành cơ chế phòng ngừa tự động, không giấu diếm.
- Vẫn còn ba khoản nợ kỹ thuật thật, đều đã được ghi chép công khai: chưa ngăn cách vật lý dữ liệu theo tenant, thanh toán chưa dùng saga/outbox dù hạ tầng (RabbitMQ) đã chạy sẵn, và chưa có xác thực thật.
- Việc đọc hiểu tiến độ nên dựa vào [`docs/roadmap.md`](roadmap.md) và trạng thái `[X]`/`[ ]` trong từng `specs/*/tasks.md`, và đối chiếu với Jira SCRUM để có xác nhận chính thức về việc đóng Giai đoạn 1 — bản thân repo không tự tuyên bố điều này ở đâu.
