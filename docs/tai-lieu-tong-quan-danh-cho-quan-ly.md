# Tài liệu kỹ thuật tổng quan — Nền tảng Ecommerce

*Viết cho: quản lý không trực tiếp code .NET. Mục tiêu: hiểu codebase hiện tại đang có gì, các phần liên hệ với nhau ra sao, và vì sao nó được thiết kế như vậy — không cần đọc code.*

*Cập nhật lần cuối: sau khi hoàn thành tính năng `002-gateway-bff-routing` (SCRUM-13).*

---

## Điều quan trọng nhất cần biết trước khi đọc tiếp

Đây **không phải** một hệ thống thương mại điện tử đang chạy production. Theo [`docs/roadmap.md`](roadmap.md), đây là **dự án luyện tập cá nhân (solo)** để một người thực hành đầy đủ vòng đời phần mềm (Product Owner → Dev → QA → DevOps → SRE), đang ở **Giai đoạn 1/5 ("Walking Skeleton" — bộ khung đi được, chưa có thịt)**.

Trong repo có **hai tầng thông tin** dễ nhầm lẫn với nhau:

| Tầng | Là gì | Đã có code chưa? |
|---|---|---|
| **Bản thiết kế mục tiêu** (`docs/system-design.md`, `docs/tech-stack-decisions.md`, `docs/adr/`, `.specify/memory/constitution.md`) | Kiến trúc đầy đủ dự kiến: 8 service, Identity Server, message queue, Redis, web app React... | **Một phần** — xem cột bên dưới |
| **Codebase thực tế hôm nay** (`services/`, `shared/`, `tests/`) | 6 service chạy được: 4 service nghiệp vụ có CSDL riêng + API đọc, cộng 1 API Gateway và 1 BFF nối chúng lại thành **một cửa vào duy nhất** | **Có**, và là toàn bộ những gì tài liệu này mô tả |

**Thay đổi lớn nhất so với bản trước của tài liệu này:** trước đây 4 service là "vỏ rỗng chỉ có health-check", và Gateway/BFF mới nằm trên giấy. Hiện nay **cả hai đã được xây và chạy được**, các service đã có bảng dữ liệu thật và API đọc thật, và một request đi xuyên suốt từ ngoài vào tới cơ sở dữ liệu.

Phần công cụ sinh tự động của Spec-Kit (`.specify/`, `specs/`, các slash-command) không được giải thích ở đây vì đó là công cụ hỗ trợ viết đặc tả, không phải sản phẩm.

---

## 1. Giải thích codebase hiện tại

### 1.1 Đây là gì, về mặt kỹ thuật

- Ngôn ngữ/nền tảng: **C# trên .NET 10**.
- File gốc mở dự án: [`Ecommerce.slnx`](../Ecommerce.slnx) — "solution file", giống một tủ hồ sơ liệt kê toàn bộ **21 dự án con**. Mở file này bằng Visual Studio là thấy toàn bộ codebase.
- 21 dự án con chia làm 3 nhóm thư mục:
  - `services/` — **6 dự án API** (4 nghiệp vụ + 2 ở biên) kèm 12 dự án test đi theo
  - `shared/` — 1 thư viện dùng chung, không phải service
  - `tests/` — 2 dự án kiểm tra kiến trúc ở cấp toàn hệ thống
- Tổng cộng **14 dự án test / 96 bài kiểm thử, hiện tất cả đều xanh**.

### 1.2 Sáu service, chia hai loại

**Bốn service nghiệp vụ** — mỗi cái sở hữu một cơ sở dữ liệu riêng, không ai được đụng vào CSDL của ai:

| Service | Nghiệp vụ | Cổng (máy dev) | API đọc đã có |
|---|---|---|---|
| Products | Danh mục sản phẩm | 5088 | `GET /products` |
| Baskets | Giỏ hàng | 5188 | `GET /baskets/{id}` |
| Orders | Đơn hàng | 5041 | `GET /orders/{id}` |
| Parties | Khách hàng / định danh | 5204 | `GET /parties/{id}` |

**Hai service ở biên** — không sở hữu dữ liệu, chỉ điều phối:

| Service | Vai trò | Cổng | API đã có |
|---|---|---|---|
| Gateway | Cửa vào duy nhất của toàn hệ thống | 5300 | Chuyển tiếp mọi thứ sang BFF |
| BFF | Gộp và định hình dữ liệu cho giao diện | 5301 | `GET /bff/products`, `/bff/baskets/{id}`, `/bff/orders/{id}`, `/bff/parties/{id}` |

Ngoài ra **cả 6 service** đều có `GET /health/live` (báo tiến trình còn sống) và `GET /health/ready` (báo sẵn sàng nhận việc). Với 4 service nghiệp vụ, `/health/ready` thật sự mở kết nối tới CSDL của chính nó.

### 1.3 Dữ liệu đã có thật

Khác với bản trước của tài liệu này, các file kết nối CSDL không còn trống:

- Mỗi service nghiệp vụ có **một bảng dữ liệu thật** (`Product`, `Basket`, `Order`, `Party`), cố tình để tối giản — chỉ đúng những trường mà BFF cần đọc.
- Mỗi service có **script tạo bảng (EF Core migration)** được lưu trong repo, đọc được bằng mắt và review được như code. Đây là bản ghi có phiên bản cho mọi thay đổi cấu trúc CSDL, kèm sẵn kịch bản lùi lại.
- Ranh giới trách nhiệm được ghi rõ: **hạ tầng tạo database rỗng, migration tạo bảng bên trong**.

### 1.4 Một request đi như thế nào

Đây là điều tính năng `002` vừa hoàn thành:

```
Người dùng / curl
      │  chỉ biết DUY NHẤT địa chỉ cổng 5300
      ▼
  Gateway (5300)          ── không mở gói, chuyển tiếp nguyên vẹn
      ▼
  BFF (5301)              ── giải mã, gọi service cần thiết, cắt gọt lại
      ▼
  Products (5088)         ── đọc CSDL của riêng nó
      ▼
  SQL Server "products"
```

Người gọi **không cần biết** có bao nhiêu service, chúng nằm ở đâu, hay tên gì. Đó là toàn bộ giá trị của hai tầng biên.

### 1.5 Cách tổ chức code trong mỗi service

Thay vì chia theo "tầng kỹ thuật" kiểu cổ điển (Controller / Service / Repository), dự án **chủ động chọn chia theo tính năng** ("vertical-slice"): mỗi năng lực nghiệp vụ có một thư mục `Features/<TênNăngLực>/` chứa mọi thứ liên quan trong cùng một chỗ. Quy tắc này được **ép buộc bằng máy** (Mục 2.5) chứ không chỉ là quy ước bằng lời, và áp dụng cho cả Gateway lẫn BFF chứ không riêng service nghiệp vụ.

---

## 2. Các dự án/component và cách chúng phụ thuộc lẫn nhau

### 2.1 Bản đồ quan hệ

- **4 service nghiệp vụ vẫn hoàn toàn không biết đến nhau.** Không service nào gọi service khác, không service nào tham chiếu code của service khác. Nguyên tắc này không bị nới lỏng khi thêm BFF.
- **BFF gọi 4 service nghiệp vụ, nhưng chỉ qua HTTP** — đúng như một khách hàng bên ngoài. Code của BFF không tham chiếu code của bất kỳ service nào. (Riêng *dự án test* của BFF có tham chiếu, để chạy service thật trong bộ nhớ khi kiểm thử — đây là chuyện của test, không phải của sản phẩm.)
- **Gateway chỉ biết duy nhất địa chỉ của BFF.** Nó cố tình **không** có đường đi thẳng tới service nghiệp vụ nào, và có một bài test canh đúng điều đó.
- **Cả 6 service dùng chung 1 thư viện: `shared/ServiceDefaults`.**
- **2 dự án `tests/CrossServiceIsolation.Tests` và `tests/StructureConventionTests`** đứng ngoài, không tham chiếu code của service nào, tự đọc file cấu hình và cấu trúc thư mục để kiểm tra.

### 2.2 `shared/ServiceDefaults` — thư viện dùng chung

Không phải một service, mà là gói cấu hình chung để 6 service không mỗi cái tự cấu hình một kiểu. Nó lo 2 việc:

1. Bật ghi log/theo dõi hiệu năng chuẩn hoá (OpenTelemetry).
2. Gắn một **mã theo dõi (Correlation-Id)** vào mỗi request, để lần theo một request đi qua nhiều service bằng đúng một mã số.

**Một lỗi thật đã được phát hiện và sửa ở tính năng này:** mã theo dõi do Gateway sinh ra trước đây **không đi kèm sang BFF** — BFF tự sinh một mã thứ hai. Hậu quả: khách hàng gặp lỗi, chụp màn hình gửi mã tra cứu, nhưng mã đó chỉ tìm thấy log của Gateway, không thấy gì của BFF — đúng lúc cần nó nhất thì nó vô dụng. Lỗi chỉ lộ ra khi **chạy thật cả chuỗi**, không test đơn lẻ nào thấy được. Sửa bằng một dòng trong thư viện dùng chung, nên **cả 6 service cùng được vá**.

### 2.3 Gateway (`services/gateway`) — cửa vào duy nhất

Dùng YARP (thư viện reverse-proxy của Microsoft). Toàn bộ "bộ não" của nó nằm trong **file cấu hình JSON**, không có một dòng logic định tuyến nào trong code C#.

Lợi ích thực tế: muốn chuyển hướng lưu lượng (ví dụ thử nghiệm phiên bản BFF mới), chỉ cần đổi file cấu hình — không cần build lại, không cần deploy image mới. Kỹ sư vận hành làm được, không phải chờ lập trình viên.

Bảng route hiện tại có đúng **một dòng**: "mọi đường dẫn → BFF". Sự đơn giản này là có chủ đích — nếu Gateway phải liệt kê từng đường dẫn của BFF, thì mỗi lần BFF có tính năng mới lại phải sửa và deploy Gateway. Đó chính là sự ràng buộc mà tính năng này sinh ra để xoá bỏ.

### 2.4 BFF (`services/bff`) — gộp và định hình dữ liệu

BFF = "Backend For Frontend". Khác Gateway ở chỗ: **Gateway chuyển tiếp, BFF mở gói ra đọc và ghép lại**.

Ba cơ chế đáng chú ý:

**a) Ngân sách thời gian rõ ràng.** Mỗi lời gọi ra ngoài đều bị giới hạn: 1 giây cho một lần thử, tối đa 3 giây kể cả thử lại, và có "cầu dao" (circuit breaker) tự ngắt khi service kia đã chết. Không có chỗ nào chờ vô hạn.

**b) Hai loại lỗi được phân biệt.** Khi service phía sau có vấn đề, BFF trả về mã lỗi khác nhau tuỳ nguyên nhân:

| Mã | Nghĩa | Người trực nên làm gì |
|---|---|---|
| 502 | Service kia **biến mất** | Gọi đội phụ trách service đó |
| 504 | Service kia **đang đuối** | Xem tải, CPU, cơ sở dữ liệu |
| 500 | Lỗi của **chính BFF** | Đội BFF tự xử lý |
| 404 | Không tìm thấy bản ghi — **không phải lỗi** | Không làm gì, đây là câu trả lời đúng |

Phân biệt 404 với 502 quan trọng hơn vẻ ngoài: "giỏ hàng này không tồn tại" là câu trả lời đúng cho một link cũ, không phải sự cố hệ thống. Gộp chung lại thì giao diện sẽ báo "hệ thống đang lỗi" cho một tình huống hoàn toàn bình thường.

**c) Dữ liệu được cắt gọt trước khi ra ngoài.** BFF không chuyển tiếp nguyên vẹn dữ liệu từ service phía sau, mà dịch sang một hình dạng riêng dành cho giao diện. Khi service Products sau này thêm trường "giá vốn" vào dữ liệu nội bộ, trường đó **không tự động** lọt ra trình duyệt của khách hàng — muốn lộ ra thì phải sửa code có chủ đích, và thay đổi đó nằm trong bản review.

### 2.5 Bốn "lưới an toàn" kiến trúc tự động

Đây là phần đáng chú ý nhất của codebase: những cơ chế **tự động ngăn lỗi kiến trúc**, chạy như bài kiểm thử mỗi lần build.

| Cơ chế | Chặn điều gì |
|---|---|
| `tests/CrossServiceIsolation.Tests` | Service A cầm chuỗi kết nối tới CSDL của service B. Đồng thời chặn cả chiều ngược lại: Gateway/BFF **không được** cầm bất kỳ chuỗi kết nối CSDL nào, vì chúng không sở hữu dữ liệu |
| `tests/StructureConventionTests` | Ai đó tạo lại thư mục `Controllers/`, `Services/`, `Repositories/` — đi ngược quy ước đã chọn |
| `Gateway.Api.UnitTests/RouteConfigurationTests` | Cấu hình định tuyến sai chính tả, hoặc có route đi thẳng tới service nghiệp vụ (bỏ qua BFF) |
| `Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests` | Timeout của Gateway bị đặt **thấp hơn** ngân sách của BFF — nếu vậy Gateway sẽ cắt ngang khi BFF còn đang soạn thông báo lỗi có ích |
| `Bff.Api.IntegrationTests/GeneratedContractTests` | Tài liệu API sinh tự động thiếu mất các trường hợp lỗi (xem Mục 3.2, tình huống 6) |

Ràng buộc timeout ở dòng thứ tư đáng chú ý vì nó **trải qua hai service** — không service nào tự canh được một mình. Để nó dưới dạng một ghi chú trong tài liệu thì chỉ có tác dụng cho tới khi ai đó chỉnh một trong hai con số.

### 2.6 Hạ tầng cục bộ và triển khai

- [`docker-compose.deps.yml`](../docker-compose.deps.yml) khởi động **4 container SQL Server độc lập** (cổng 14330–14333), mỗi service một cái, kèm 4 job tạo database rỗng.
- **Cả 6 service đều đã có `Dockerfile`** — build được thành container, chạy bằng tài khoản không phải root.
- Công cụ tạo migration được **ghim phiên bản trong repo** (`.config/dotnet-tools.json`), để mọi người và cả CI dùng đúng một phiên bản, tránh việc hai máy sinh ra hai kết quả khác nhau.
- **Vẫn chưa có pipeline CI/CD** (không có `.github/workflows`, chưa có cấu hình Jenkins dù đó là công cụ đã chốt trong thiết kế). Cũng **chưa có lệnh duy nhất chạy toàn bộ hệ thống** — hiện phải mở 6 cửa sổ terminal. Việc này là SCRUM-15, chưa làm.

---

## 3. Mục đích từng phần + các tình huống thực tế được giải quyết

### 3.1 Mục đích từng dự án

| Dự án | Mục đích |
|---|---|
| `Products.Api` | Sở hữu danh mục sản phẩm. Có bảng dữ liệu và 1 API đọc. |
| `Baskets.Api` | Sở hữu dữ liệu giỏ hàng. Có bảng dữ liệu và 1 API đọc. |
| `Orders.Api` | Sở hữu dữ liệu đơn hàng. Có bảng dữ liệu và 1 API đọc. |
| `Parties.Api` | Sở hữu định danh khách hàng/đối tác. Có bảng dữ liệu và 1 API đọc. |
| `Gateway.Api` | Cửa vào duy nhất; che giấu toàn bộ cấu trúc bên trong khỏi người gọi. |
| `Bff.Api` | Gộp dữ liệu từ nhiều service thành một lần gọi cho giao diện; cắt gọt dữ liệu; xử lý lỗi có kiểm soát. |
| `shared/ServiceDefaults` | Chuẩn hoá log/theo dõi và mã tra cứu cho mọi service. |
| `tests/CrossServiceIsolation.Tests` | Tự động chặn việc đọc/ghi nhầm CSDL của service khác. |
| `tests/StructureConventionTests` | Tự động chặn việc phá vỡ quy ước tổ chức code. |
| `*.UnitTests` / `*.IntegrationTests` | Kiểm tra logic riêng và kiểm tra với SQL Server thật trong container (không dùng giả lập). |

### 3.2 Sáu tình huống thật mà giải pháp này giải quyết

**1) Ngăn rò rỉ/đè dữ liệu chéo giữa các bộ phận nghiệp vụ, trước khi nó xảy ra.**
Một lập trình viên copy-paste nhầm file cấu hình khiến service Baskets trỏ sang CSDL của Orders. Trong nhiều hệ thống, lỗi này chỉ lộ ra khi dữ liệu thật đã sai lệch trong production. Ở đây build **thất bại ngay**, trước khi code được merge. Đáng chú ý: cơ chế bắt lỗi ở khâu *sở hữu* chứ không phải khâu *sử dụng* — chỉ cần cầm chuỗi kết nối của service khác là đã vi phạm, dù chưa hề dùng tới.

**2) Phát hiện sự cố hạ tầng tự động, không cần con người theo dõi 24/7.**
`/health/ready` thật sự thử mở kết nối tới CSDL. Khi CSDL gặp sự cố, hệ thống điều phối (Kubernetes) tự động ngừng đưa lưu lượng vào service đó. Quan trọng không kém: `/health/live` **cố tình không** kiểm tra CSDL — nếu kiểm tra, một sự cố CSDL 30 giây sẽ khiến toàn bộ pod bị khởi động lại và biến thành sự cố ứng dụng 5 phút.

**3) Giữ chất lượng kiến trúc ổn định khi thời gian trôi qua, không phụ thuộc trí nhớ một người.**
Quy ước tổ chức code rất dễ bị phá vỡ dần nếu chỉ dựa vào nhắc nhau trong buổi review. Các bài test kiến trúc biến quy ước thành điều kiện bắt buộc để build thành công.

**4) Một service chết không kéo sập cả hệ thống.**
Nếu không có giới hạn thời gian, thư viện gọi HTTP của .NET mặc định chờ **100 giây**. Khi service Products treo: mỗi request mới lại chiếm một luồng xử lý, sau khoảng 30 giây BFF cạn luồng và **ngừng trả lời mọi thứ** — kể cả những trang không liên quan tới sản phẩm. Một service hỏng thành cả website hỏng.
Với ngân sách 3 giây hiện tại: đã đo thật, khi tắt service Products, người gọi nhận về mã lỗi rõ ràng sau **3,14 giây**, và các trang khác vẫn chạy bình thường.

**5) Lỗi có mã tra cứu, thay vì "hệ thống đang bận, vui lòng thử lại".**
Khi có sự cố, người dùng nhận về một mã tra cứu, và mã đó lần được toàn bộ hành trình của chính request đó qua cả ba tầng trong hệ thống ghi log. Không có nó, việc điều tra một khiếu nại cụ thể gần như bất khả thi trong hệ phân tán.
Thông báo lỗi cũng chỉ nêu **tên logic** của service gặp vấn đề (`ProductsApi`), không bao giờ lộ địa chỉ máy chủ nội bộ — vì lộ ra là tặng thông tin trinh sát cho kẻ tấn công.

**6) Đội frontend không thể vô tình bỏ sót trường hợp lỗi.**
BFF tự sinh ra tài liệu mô tả API, và mã nguồn của giao diện sẽ được **sinh tự động** từ tài liệu đó (kế hoạch SCRUM-14). Trong quá trình làm tính năng này đã phát hiện tài liệu sinh ra **chỉ khai báo trường hợp thành công** — nghĩa là đội frontend sẽ nhận được một thư viện gọi API mà hệ kiểu khẳng định "không thể lỗi". Lập trình viên frontend khi đó không cố tình bỏ qua lỗi, họ **không biết** lỗi tồn tại; đến khi service phía sau chết thì trang trắng và không có thông báo nào.
Đã sửa bằng cách khai báo rõ trong code, và thêm một bài test canh để lệch không quay lại.

---

## 4. Sơ đồ kiến trúc hiện tại (dùng được với draw.io)

Sơ đồ vẽ **đúng những gì đang có trong code hôm nay** (phần trên, viền liền, tô màu) và đối chiếu với **phần chưa xây** (phần dưới, viền đứt màu đỏ nhạt).

> **Lưu ý phân biệt:** repo có sẵn 3 sơ đồ khác ở [`docs/system-design.md`](system-design.md) — nhưng chúng vẽ **kiến trúc mục tiêu đầy đủ**, không phải trạng thái hiện tại.

### Cách dùng
1. Mở [app.diagrams.net](https://app.diagrams.net) (draw.io).
2. Vào menu **Extras → Edit Diagram…**
3. Xoá nội dung trống, dán toàn bộ khối XML bên dưới vào, bấm **Save/OK**.

*(Hoặc đơn giản hơn: mở trực tiếp file [`docs/diagrams/current-state-architecture.drawio`](diagrams/current-state-architecture.drawio) đã có sẵn trong repo — nội dung giống hệt khối XML bên dưới, đã được cập nhật cùng lúc.)*

```xml
<mxfile host="app.diagrams.net" modified="2026-08-15T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="current-state-002" name="Trang thai hien tai - sau 002">
    <mxGraphModel dx="1400" dy="1000" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1420" pageHeight="1400" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="title1" value="PHAN 1 -- DA CO TRONG CODE HOM NAY (21 du an, 96 test xanh)" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#2d6a2d;" vertex="1" parent="1">
          <mxGeometry x="30" y="10" width="900" height="26" as="geometry" />
        </mxCell>

        <mxCell id="client" value="Nguoi goi (curl / Postman)&#10;SPA React se den o SCRUM-14&#10;CHI biet DUY NHAT dia chi cong 5300" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="45" width="330" height="70" as="geometry" />
        </mxCell>

        <mxCell id="sd" value="shared/ServiceDefaults&#10;(thu vien dung chung, KHONG phai service)&#10;- OpenTelemetry: log / trace / metric&#10;- Correlation-Id: ma tra cuu 1 request&#10;  di xuyen ca 3 tang" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1030" y="45" width="360" height="110" as="geometry" />
        </mxCell>

        <mxCell id="gw" value="Gateway.Api (services/gateway) -- YARP&#10;Cong 5300&#10;Bang route = 1 dong duy nhat: MOI duong dan -&gt; BFF&#10;Toan bo dinh tuyen nam trong file cau hinh JSON,&#10;khong co logic trong code C#" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="150" width="430" height="105" as="geometry" />
        </mxCell>

        <mxCell id="gwnote" value="Vi sao 1 route catch-all: neu Gateway phai liet ke tung duong dan cua BFF thi&#10;moi lan BFF them tinh nang lai phai sua + deploy Gateway. Do chinh la su rang buoc&#10;ma tinh nang nay sinh ra de xoa bo.&#10;&#10;Gateway CO TINH khong co duong di thang toi service nghiep vu nao&#10;(co bai test canh dieu nay)." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="490" y="150" width="500" height="105" as="geometry" />
        </mxCell>

        <mxCell id="bff" value="Bff.Api (services/bff) -- Backend For Frontend&#10;Cong 5301  |  KHONG co co so du lieu&#10;GET /bff/products, /bff/baskets/{id},&#10;      /bff/orders/{id}, /bff/parties/{id}&#10;Sinh tu dong tai lieu API tai /openapi/v1.json" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="290" width="430" height="120" as="geometry" />
        </mxCell>

        <mxCell id="bffnote" value="Ngan sach thoi gian cho MOI loi goi ra ngoai (khong cho vo han):&#10;   1 giay / lan thu   |   toi da 3 giay ke ca thu lai   |   cau dao ngat khi service kia da chet&#10;&#10;Phan biet loai loi tra ve nguoi dung:&#10;   502 = service kia BIEN MAT      504 = service kia DANG DUOI&#10;   500 = loi cua chinh BFF          404 = khong tim thay ban ghi (KHONG phai loi)&#10;&#10;Du lieu duoc CAT GOT lai truoc khi ra ngoai: truong noi bo (vd gia von) khong tu dong&#10;lot ra trinh duyet khi service phia sau them truong moi." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="490" y="290" width="900" height="120" as="geometry" />
        </mxCell>

        <mxCell id="svc1" value="Products.Api&#10;Cong 5088&#10;GET /products&#10;+ /health/live, /health/ready&#10;Bang: Product (Id, Name, Price)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="470" width="320" height="115" as="geometry" />
        </mxCell>
        <mxCell id="svc2" value="Baskets.Api&#10;Cong 5188&#10;GET /baskets/{id}&#10;+ /health/live, /health/ready&#10;Bang: Basket (Id, CustomerId)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="380" y="470" width="320" height="115" as="geometry" />
        </mxCell>
        <mxCell id="svc3" value="Orders.Api&#10;Cong 5041&#10;GET /orders/{id}&#10;+ /health/live, /health/ready&#10;Bang: Order (Id, PlacedAtUtc, Total)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="730" y="470" width="320" height="115" as="geometry" />
        </mxCell>
        <mxCell id="svc4" value="Parties.Api&#10;Cong 5204&#10;GET /parties/{id}&#10;+ /health/live, /health/ready&#10;Bang: Party (Id, DisplayName)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1080" y="470" width="310" height="115" as="geometry" />
        </mxCell>

        <mxCell id="svcnote" value="4 service nghiep vu HOAN TOAN khong biet den nhau: khong cai nao goi cai nao, khong cai nao tham chieu code cua cai nao." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="30" y="435" width="1360" height="20" as="geometry" />
        </mxCell>

        <mxCell id="composebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#999999;dashed=1;verticalAlign=top;fontColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="20" y="605" width="1380" height="150" as="geometry" />
        </mxCell>
        <mxCell id="composelbl" value="docker-compose.deps.yml -- 4 container SQL Server rieng biet + 4 job tao database rong. Bang du lieu ben trong do EF Core migration tao ra (da co trong repo)." style="text;html=1;fontSize=11;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="40" y="612" width="1330" height="20" as="geometry" />
        </mxCell>

        <mxCell id="db1" value="SQL Server (container rieng)&#10;Database: products -- cong 14331&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="30" y="645" width="320" height="90" as="geometry" />
        </mxCell>
        <mxCell id="db2" value="SQL Server (container rieng)&#10;Database: baskets -- cong 14332&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="380" y="645" width="320" height="90" as="geometry" />
        </mxCell>
        <mxCell id="db3" value="SQL Server (container rieng)&#10;Database: orders -- cong 14333&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="730" y="645" width="320" height="90" as="geometry" />
        </mxCell>
        <mxCell id="db4" value="SQL Server (container rieng)&#10;Database: parties -- cong 14330&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="1080" y="645" width="310" height="90" as="geometry" />
        </mxCell>

        <mxCell id="guardtitle" value="'Luoi an toan' kien truc tu dong -- chay nhu bai test moi lan build, FAIL build khi bi vi pham" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="785" width="1000" height="24" as="geometry" />
        </mxCell>

        <mxCell id="guard1" value="tests/CrossServiceIsolation.Tests&#10;- Service A cam chuoi ket noi CSDL cua service B&#10;- Gateway/BFF cam BAT KY chuoi ket noi nao&#10;  (chung khong so huu du lieu)&#10;Bat o khau SO HUU, khong doi den khi SU DUNG" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="820" width="440" height="110" as="geometry" />
        </mxCell>
        <mxCell id="guard2" value="tests/StructureConventionTests&#10;FAIL build neu service co thu muc&#10;Controllers/, Services/, Repositories/&#10;Bat buoc moi service co it nhat 1 thu muc&#10;Features/&lt;TenNangLuc&gt; -- ap dung ca Gateway va BFF" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="490" y="820" width="440" height="110" as="geometry" />
        </mxCell>
        <mxCell id="guard3" value="Gateway.Api.UnitTests&#10;- RouteConfigurationTests: cau hinh dinh tuyen sai&#10;  chinh ta, hoac co route di thang toi service nghiep vu&#10;- ForwardingTimeoutBudgetTests: timeout Gateway&#10;  bi dat THAP HON ngan sach cua BFF" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="950" y="820" width="440" height="110" as="geometry" />
        </mxCell>

        <mxCell id="guard4" value="Bff.Api.IntegrationTests/GeneratedContractTests&#10;Tai lieu API sinh tu dong phai khai bao DU ca truong hop loi&#10;(404 / 502 / 504), khong chi truong hop thanh cong -- vi ma nguon&#10;giao dien se duoc SINH TU DONG tu tai lieu nay (SCRUM-14)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="945" width="660" height="80" as="geometry" />
        </mxCell>
        <mxCell id="guard5" value="services/*/tests/*.IntegrationTests -- dung Testcontainers.MsSql:&#10;chay SQL Server THAT trong container, khong dung gia lap.&#10;Test cua BFF con chay ca 4 service THAT trong bo nho de kiem tra&#10;that su goi duoc, thay vi gia lap cau tra loi." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="720" y="945" width="670" height="80" as="geometry" />
        </mxCell>

        <mxCell id="e_client_gw" value="HTTP :5300" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="client" target="gw">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_gw_bff" value="YARP chuyen tiep TAT CA (khong mo goi)" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="gw" target="bff">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_bff_1" value="HTTP + ngan sach thoi gian" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_2" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_3" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_4" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_db1" value="EF Core" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc1" target="db1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db2" value="EF Core" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc2" target="db2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db3" value="EF Core" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc3" target="db3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db4" value="EF Core" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc4" target="db4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_sd_gw" value="tham chieu thu vien (ca 6 service)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="gw">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_sd_svc4" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="svc4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="title2" value="PHAN 2 -- CHUA XAY DUNG, MOI LA KE HOACH (Phase 2-5, xem docs/roadmap.md)" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#a03030;" vertex="1" parent="1">
          <mxGeometry x="30" y="1060" width="1000" height="26" as="geometry" />
        </mxCell>

        <mxCell id="futurebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#fafafa;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="20" y="1095" width="1380" height="165" as="geometry" />
        </mxCell>

        <mxCell id="f1" value="React SPA&#10;Web + Mobile-web&#10;(SCRUM-14)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="45" y="1120" width="170" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f2" value="Chay toan bo bang&#10;1 lenh duy nhat&#10;(SCRUM-15)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="230" y="1120" width="170" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f3" value="Identity Server&#10;(Duende)&#10;(SCRUM-23)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="415" y="1120" width="170" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f4" value="RabbitMQ +&#10;MassTransit" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="600" y="1120" width="170" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f5" value="Redis&#10;(basket cache)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="785" y="1120" width="170" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f6" value="Logistics +&#10;Invoices service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="970" y="1120" width="170" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f7" value="Jenkins CI/CD&#10;Vault, Unleash,&#10;Pact Broker" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1155" y="1120" width="170" height="60" as="geometry" />
        </mxCell>
        <mxCell id="fnote" value="Cac khoi nay moi la quyet dinh trong ADR / ban ve trong docs/system-design.md -- chua co dong code nao. Luu y: API Gateway va BFF DA CHUYEN LEN PHAN 1 -- chung khong con la ke hoach nua." style="text;html=1;fontSize=10;fontColor=#a03030;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="45" y="1190" width="1290" height="50" as="geometry" />
        </mxCell>

        <mxCell id="lgtitle" value="Chu giai" style="text;html=1;fontStyle=1;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="30" y="1275" width="100" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="30" y="1305" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" value="Service nghiep vu (so huu du lieu rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1305" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;" vertex="1" parent="1">
          <mxGeometry x="30" y="1335" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" value="Service o bien (khong so huu du lieu)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1335" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="390" y="1305" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" value="Thu vien dung chung (khong phai service)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1305" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="390" y="1335" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" value="Luoi an toan kien truc (tests)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1335" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="750" y="1305" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" value="Co so du lieu (moi service 1 CSDL rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="775" y="1305" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="750" y="1335" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6t" value="Chi la ke hoach -- chua co code" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="775" y="1335" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="1110" y="1315" as="sourcePoint" />
            <mxPoint x="1160" y="1315" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" value="Goi that luc chay (HTTP / EF Core)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1170" y="1305" width="240" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="1110" y="1345" as="sourcePoint" />
            <mxPoint x="1160" y="1345" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" value="Tham chieu thu vien luc build" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1170" y="1335" width="240" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

---

## 5. Rủi ro và việc còn treo

**Một lỗi cấu hình đã xác minh, chưa sửa.** Cấu hình của BFF trên máy dev đang trỏ **nhầm cổng** giữa hai service Baskets và Orders: BFF tìm Baskets ở cổng 5041 (thực tế là cổng của Orders) và ngược lại. Hậu quả: gọi `/bff/baskets/{id}` sẽ luôn báo "không tìm thấy" kể cả với giỏ hàng có thật.

Điều đáng lưu ý về mặt quy trình: **không bài test nào phát hiện được lỗi này**, vì test thay thế toàn bộ phần kết nối mạng nên không bao giờ dùng tới giá trị cấu hình thật. Lỗi bắt nguồn từ chính bản mô tả công việc (ghi sai thứ tự cổng), rồi được cài đặt đúng theo bản mô tả sai đó, và gần đây đã lan sang tài liệu hướng dẫn chạy thử. Đây là ví dụ điển hình cho việc **test xanh không đồng nghĩa với hệ thống chạy đúng** khi cấu hình nằm ngoài phạm vi test.

**Chưa có CI/CD.** Toàn bộ 96 bài kiểm thử hiện phải chạy thủ công trên máy cá nhân. Mọi "lưới an toàn kiến trúc" mô tả ở Mục 2.5 chỉ phát huy tác dụng đầy đủ khi có pipeline tự động chạy chúng trên mỗi thay đổi.

**Chạy thử cục bộ còn nặng.** Hiện phải mở 6 cửa sổ terminal, khởi động 4 container CSDL, chạy 4 lệnh tạo bảng. SCRUM-15 sẽ gom lại thành một lệnh.

**Chưa có xác thực/phân quyền.** Mọi API hiện đều mở, không cần đăng nhập. Đây là khoản nợ đã được ghi nhận có chủ đích, thuộc SCRUM-23 (Identity Server) ở Giai đoạn 3.

**Rủi ro đã được ghi trong roadmap:** việc "giả lập" một tenant duy nhất ở giai đoạn này có thể cần làm lại một phần khi tới Giai đoạn 3 (bảo mật/đa tenant thật).

---

## Tổng kết ngắn cho quản lý

- Codebase hiện tại = **6 service chạy được** (4 nghiệp vụ có CSDL riêng + Gateway + BFF), **1 thư viện dùng chung**, **14 dự án test với 96 bài kiểm thử đều xanh**.
- So với lần cập nhật trước, hệ thống đã đi từ "4 vỏ rỗng chỉ có health-check" sang **một chuỗi hoàn chỉnh chạy được từ ngoài vào tới cơ sở dữ liệu**: người gọi chỉ cần biết một địa chỉ duy nhất, và nhận về dữ liệu thật.
- Phần được đầu tư kỹ nhất vẫn không phải là tính năng, mà là **các cơ chế tự động ngăn lỗi kiến trúc** — nay đã có 5 loại, canh từ ranh giới dữ liệu, cấu trúc thư mục, cấu hình định tuyến, ngân sách thời gian giữa hai service, cho tới tính đầy đủ của tài liệu API.
- Đây vẫn là lựa chọn có chủ đích của Giai đoạn 1 ("chứng minh nền móng đi được trước, thêm nghiệp vụ sau"), không phải dự án chậm tiến độ. Mỗi service mới chỉ có **một API đọc duy nhất**, vừa đủ để chứng minh chuỗi hoạt động.
- Hai việc còn lại của Giai đoạn 1: giao diện React (SCRUM-14) và chạy toàn bộ bằng một lệnh (SCRUM-15).
- Việc đọc hiểu tiến độ nên dựa vào [`docs/roadmap.md`](roadmap.md) (5 giai đoạn, đang ở giai đoạn 1) hơn là dựa vào độ dày tài liệu thiết kế trong `docs/`.
