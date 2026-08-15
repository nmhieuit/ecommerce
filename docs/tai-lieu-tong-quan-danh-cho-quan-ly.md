# Tài liệu kỹ thuật tổng quan — Nền tảng Ecommerce

*Viết cho: quản lý không trực tiếp code .NET. Mục tiêu: hiểu codebase hiện tại đang có gì, các phần liên hệ với nhau ra sao, và vì sao nó được thiết kế như vậy — không cần đọc code.*

---

## Điều quan trọng nhất cần biết trước khi đọc tiếp

Đây **không phải** một hệ thống thương mại điện tử đang chạy production. Theo [`docs/roadmap.md`](../docs/roadmap.md), đây là **dự án luyện tập cá nhân (solo)** để một người thực hành đầy đủ vòng đời phần mềm (Product Owner → Dev → QA → DevOps → SRE), đang ở **Giai đoạn 1/5 ("Walking Skeleton" — bộ khung đi được, chưa có thịt)**.

Trong repo có **hai tầng thông tin** dễ nhầm lẫn với nhau:

| Tầng | Là gì | Đã có code chưa? |
|---|---|---|
| **Bản thiết kế mục tiêu** (`docs/system-design.md`, `docs/tech-stack-decisions.md`, `docs/adr/`, `.specify/memory/constitution.md`) | Kiến trúc đầy đủ dự kiến: 6 service, API Gateway, BFF, Identity Server, message queue, Redis, web app React... | **Chưa** — đây là bản vẽ/quyết định đã chốt, chờ triển khai dần qua 5 giai đoạn |
| **Codebase thực tế hôm nay** (`services/`, `shared/`, `tests/`) | 4 "vỏ" service rỗng, mỗi cái chỉ có health-check và kết nối CSDL riêng | **Có**, và là toàn bộ những gì tài liệu này mô tả ở Mục 1–3 |

Tài liệu này tập trung vào **tầng thứ hai** (những gì thực sự chạy được hôm nay), và chỉ nhắc tới tầng thứ nhất để bạn không hiểu nhầm là đã xây xong. Phần rác/công cụ sinh tự động của Spec-Kit (`.specify/`, `specs/`, các slash-command) không được giải thích ở đây vì đó là công cụ hỗ trợ viết spec, không phải sản phẩm.

---

## 1. Giải thích codebase hiện tại

### 1.1 Đây là gì, về mặt kỹ thuật

- Ngôn ngữ/nền tảng: **C# trên .NET 10** (bản rất mới, phát hành gần đây).
- File gốc mở dự án: [`Ecommerce.slnx`](../Ecommerce.slnx) — đây là "solution file", giống như một cái tủ hồ sơ liệt kê toàn bộ 15 dự án con bên trong. Mở file này bằng Visual Studio là thấy toàn bộ codebase.
- Toàn bộ 15 dự án con chia làm 3 nhóm thư mục:
  - `services/` — 4 dự án API nghiệp vụ (Mục 1.2)
  - `shared/` — 1 thư viện dùng chung, không phải service (Mục 2.2)
  - `tests/` — 2 dự án kiểm tra kiến trúc ở cấp toàn hệ thống (Mục 2.3)

### 1.2 Đã xây được gì

Có đúng **4 "service"**, và cả 4 đều đang ở dạng **vỏ rỗng giống hệt nhau** — chưa có nghiệp vụ thật:

| Service | Thư mục | Ý nghĩa nghiệp vụ dự kiến |
|---|---|---|
| Baskets | `services/baskets` | Giỏ hàng |
| Orders | `services/orders` | Đơn hàng |
| Parties | `services/parties` | Khách hàng / định danh |
| Products | `services/products` | Danh mục sản phẩm |

Mỗi service, xét theo code thật (ví dụ [`services/baskets/src/Baskets.Api/Program.cs`](../services/baskets/src/Baskets.Api/Program.cs)), hiện chỉ có:

- **2 API duy nhất**, giống nhau ở cả 4 service: `GET /health/live` (báo "tiến trình còn sống", không đụng CSDL) và `GET /health/ready` (thực sự thử kết nối CSDL của chính nó, trả lỗi 503 nếu CSDL không kết nối được). Chưa có bất kỳ API nghiệp vụ nào (thêm sản phẩm, tạo giỏ hàng, đặt đơn hàng...).
- **Một CSDL SQL Server riêng cho từng service**, nhưng bảng dữ liệu (entity) **chưa được tạo** — file kết nối CSDL (`BasketsDbContext`, `OrdersDbContext`...) hiện đang trống, với ghi chú thẳng trong code là "chờ câu chuyện nghiệp vụ đầu tiên".

Nói cách khác: **hạ tầng khung sườn đã xong, nhưng chưa có một tính năng nghiệp vụ nào được code.** Đây đúng như tên gọi của tính năng duy nhất đã hoàn thành: [`specs/001-scaffold-service-shells`](../specs/001-scaffold-service-shells) — "dựng vỏ service".

### 1.3 Cách tổ chức code trong mỗi service

Thay vì chia theo "tầng kỹ thuật" kiểu cổ điển (Controller / Service / Repository — cách nhiều hệ thống .NET cũ hay dùng), dự án này **chủ động chọn chia theo tính năng** ("vertical-slice"): mỗi năng lực nghiệp vụ có 1 thư mục `Features/<TênNănglực>/` chứa mọi thứ liên quan (route, logic, model) trong cùng một chỗ, thay vì rải ra 3 tầng khác nhau. Quy tắc này được ghi rõ và **ép buộc bằng máy** (xem Mục 3, phần StructureConventionTests) chứ không chỉ là quy ước bằng lời.

---

## 2. Các dự án/component và cách chúng phụ thuộc lẫn nhau

### 2.1 Sơ đồ quan hệ bằng lời trước khi xem hình ở Mục 4

- **4 service (Baskets/Orders/Parties/Products) hoàn toàn không biết đến nhau.** Không có service nào gọi service khác, không service nào tham chiếu code của service khác. Đây là chủ đích, không phải thiếu sót — xem Mục 3.
- **Cả 4 service đều dùng chung 1 thư viện: `shared/ServiceDefaults`.** Đây là tham chiếu duy nhất trong file cấu hình dự án (`.csproj`) của mỗi service.
- **2 dự án `tests/CrossServiceIsolation.Tests` và `tests/StructureConventionTests`** không nằm trong bất kỳ service nào và cũng không được service nào tham chiếu ngược lại — chúng đứng ngoài, tự đọc file cấu hình/cấu trúc thư mục của cả 4 service để kiểm tra.
- Ngoài ra mỗi service có 2 dự án test riêng (`*.UnitTests`, `*.IntegrationTests`) chỉ kiểm tra chính service đó.
- **Không có** API Gateway, không có tầng "BFF" (backend tổng hợp cho frontend), không có hàng đợi tin nhắn (message queue) nối các service với nhau. Những thứ này mới chỉ tồn tại trên giấy (`docs/system-design.md`).

### 2.2 `shared/ServiceDefaults` — thư viện dùng chung

Không phải một service, mà là một gói cấu hình chung để 4 service không mỗi cái tự cấu hình một kiểu khác nhau. Nó lo 2 việc:

1. Bật ghi log/theo dõi hiệu năng chuẩn hoá (OpenTelemetry) — để sau này có thể quan sát hệ thống từ một chỗ.
2. Gắn một "mã theo dõi" (Correlation-Id) vào mỗi request, để nếu sau này có sự cố, có thể lần theo một request đi qua nhiều service bằng đúng 1 mã số.

### 2.3 Hai "dự án kiểm tra kiến trúc" — cơ chế đặc biệt đáng chú ý

Đây là phần kỹ thuật thú vị nhất trong codebase hiện tại, vì nó không phải là tính năng cho người dùng cuối, mà là **cơ chế tự động ngăn lỗi kiến trúc xảy ra**, chạy như một bài kiểm thử (test) mỗi lần build:

- **`tests/CrossServiceIsolation.Tests`**: quét toàn bộ file cấu hình (`appsettings*.json`) của cả 4 service. Nếu service A vô tình có một dòng cấu hình trỏ tới CSDL của service B (dù chỉ là tên khoá hay tên server/database), bài test này **làm build thất bại**. Đây là bằng chứng bằng máy cho nguyên tắc "mỗi service sở hữu dữ liệu riêng, không ai được đụng vào CSDL của ai" (nguyên tắc số I trong [`constitution.md`](../.specify/memory/constitution.md)).
- **`tests/StructureConventionTests`**: quét cấu trúc thư mục của cả 4 service. Nếu ai đó (kể cả tương lai chính người viết) lỡ tạo thư mục `Controllers/`, `Services/`, `Repositories/` — tức đi ngược lại quy ước "vertical-slice" đã chọn — build sẽ thất bại.

### 2.4 Hạ tầng cục bộ (local dev)

[`docker-compose.deps.yml`](../docker-compose.deps.yml) chỉ khởi động **4 container SQL Server độc lập** (một cho mỗi service, cổng 14330–14333) để chạy thử trên máy cá nhân — cố tình **không** có lệnh chạy "tất cả 4 service cùng lúc", để tiếp tục chứng minh 4 service độc lập với nhau. Chưa có Dockerfile cho chính các service, chưa có pipeline CI/CD (không có thư mục `.github/workflows`, cũng chưa thấy cấu hình Jenkins dù đó là công cụ được chốt trong bản thiết kế).

---

## 3. Mục đích từng dự án/component + 3 tình huống thực tế mà giải pháp này giải quyết

### 3.1 Mục đích từng phần (tóm tắt lại có kèm lý do thiết kế)

| Dự án | Mục đích |
|---|---|
| `Baskets.Api` | Sẽ sở hữu dữ liệu giỏ hàng của khách. Hiện là vỏ health-check. |
| `Orders.Api` | Sẽ sở hữu dữ liệu đơn hàng. Hiện là vỏ health-check. |
| `Parties.Api` | Sẽ sở hữu dữ liệu định danh khách hàng/đối tác. Hiện là vỏ health-check. |
| `Products.Api` | Sẽ sở hữu danh mục sản phẩm. Hiện là vỏ health-check. |
| `shared/ServiceDefaults` | Chuẩn hoá logging/theo dõi cho mọi service, tránh 4 service cấu hình 4 kiểu khác nhau. |
| `tests/CrossServiceIsolation.Tests` | Tự động chặn việc 1 service vô tình đọc/ghi nhầm CSDL của service khác. |
| `tests/StructureConventionTests` | Tự động chặn việc phá vỡ quy ước tổ chức code đã thống nhất. |
| `*.UnitTests` / `*.IntegrationTests` (mỗi service) | Kiểm tra logic riêng và kiểm tra health-check chạy đúng với CSDL SQL Server thật (không phải giả lập). |

### 3.2 Ba tình huống thật mà giải pháp kỹ thuật này giải quyết (dựa trên code đang thực sự chạy, không phải kế hoạch)

**1) Ngăn rò rỉ/đè dữ liệu chéo giữa các bộ phận nghiệp vụ, trước khi nó xảy ra chứ không phải sau khi phát hiện sự cố.**
Giả sử một lập trình viên (kể cả người rất cẩn thận) copy-paste nhầm file cấu hình và khiến service Baskets trỏ sang CSDL của Orders. Trong nhiều hệ thống, lỗi này chỉ lộ ra khi dữ liệu thật bị sai lệch trong production — rất tốn kém để dò lại nguyên nhân. Ở đây, `CrossServiceIsolation.Tests` bắt lỗi này **ngay khi build**, tức là trước khi merge code, không để nó có cơ hội chạm vào dữ liệu thật.

**2) Phát hiện sự cố hạ tầng tự động, không cần con người theo dõi 24/7.**
`/health/ready` không phải một API "cho vui" — nó thật sự thử mở kết nối tới CSDL của chính service đó. Trong vận hành thật (ví dụ máy chủ CSDL bị restart, mất kết nối mạng tạm thời), một hệ thống điều phối (như Kubernetes) có thể tự động ngừng đưa traffic vào service đang gặp sự cố, dựa hoàn toàn vào tín hiệu này — không cần chờ người trực phát hiện qua báo cáo lỗi của khách hàng.

**3) Giữ chất lượng kiến trúc ổn định khi đội ngũ hoặc thời gian trôi qua, không phụ thuộc vào trí nhớ của một người.**
Quy ước "chia theo tính năng, không chia theo tầng kỹ thuật" rất dễ bị phá vỡ dần theo thời gian nếu chỉ dựa vào việc nhắc nhau trong buổi review code. `StructureConventionTests` biến quy ước đó thành một điều kiện bắt buộc để build thành công — kể cả nếu 6 tháng sau có người mới (hoặc chính người cũ đã quên) vô tình tạo lại kiểu cấu trúc cũ, hệ thống sẽ tự chặn lại ngay lập tức thay vì âm thầm để kiến trúc "mục nát" dần.

---

## 4. Sơ đồ kiến trúc hiện tại (dùng được với draw.io)

Sơ đồ dưới đây vẽ **đúng những gì đang có trong code hôm nay** (phần trên, viền liền, tô màu) và đối chiếu với **phần chưa xây, mới là kế hoạch** (phần dưới, viền đứt màu đỏ nhạt) để tránh nhầm lẫn hai tầng đã nói ở đầu tài liệu.

> **Lưu ý phân biệt:** repo đã có sẵn 3 sơ đồ khác ở [`docs/system-design.md`](../docs/system-design.md) và [`docs/Designs.drawio`](../docs/Designs.drawio) — nhưng 3 sơ đồ đó vẽ **kiến trúc mục tiêu** (6 service, Gateway, BFF, message queue...), **không phải** trạng thái hiện tại. Sơ đồ mới dưới đây (lưu tại [`docs/diagrams/current-state-architecture.drawio`](../docs/diagrams/current-state-architecture.drawio)) mới là sơ đồ mô tả đúng những gì thực sự chạy được hôm nay.

### Cách dùng
1. Mở [app.diagrams.net](https://app.diagrams.net) (draw.io).
2. Vào menu **Extras → Edit Diagram…**
3. Xoá nội dung trống, dán toàn bộ khối XML bên dưới vào, bấm **Save/OK**.
4. Sơ đồ sẽ hiện ra với đầy đủ chú thích.

*(Hoặc đơn giản hơn: mở trực tiếp file [`docs/diagrams/current-state-architecture.drawio`](../docs/diagrams/current-state-architecture.drawio) đã có sẵn trong repo bằng draw.io / VS Code extension "Draw.io Integration".)*

```xml
<mxfile host="app.diagrams.net" modified="2026-08-15T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="current-state-diagram" name="Trang thai hien tai - Codebase">
    <mxGraphModel dx="1400" dy="1000" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1400" pageHeight="1250" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="title1" value="PHAN 1 -- DA CO TRONG CODE HOM NAY (services/, shared/, tests/)" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#2d6a2d;" vertex="1" parent="1">
          <mxGeometry x="30" y="10" width="800" height="26" as="geometry" />
        </mxCell>

        <mxCell id="sd" value="shared/ServiceDefaults&#10;(thu vien dung chung, KHONG phai microservice)&#10;- OpenTelemetry: log/trace/metric&#10;- Correlation-Id middleware (gan ma theo doi request)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="560" y="50" width="300" height="90" as="geometry" />
        </mxCell>

        <mxCell id="svc1" value="Baskets.Api&#10;(services/baskets)&#10;ASP.NET Core Web API - vertical-slice&#10;Endpoint: GET /health/live, GET /health/ready&#10;DbContext: BasketsDbContext (chua co bang du lieu)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="200" width="300" height="110" as="geometry" />
        </mxCell>
        <mxCell id="svc2" value="Orders.Api&#10;(services/orders)&#10;ASP.NET Core Web API - vertical-slice&#10;Endpoint: GET /health/live, GET /health/ready&#10;DbContext: OrdersDbContext (chua co bang du lieu)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="360" y="200" width="300" height="110" as="geometry" />
        </mxCell>
        <mxCell id="svc3" value="Parties.Api&#10;(services/parties)&#10;ASP.NET Core Web API - vertical-slice&#10;Endpoint: GET /health/live, GET /health/ready&#10;DbContext: PartiesDbContext (chua co bang du lieu)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="690" y="200" width="300" height="110" as="geometry" />
        </mxCell>
        <mxCell id="svc4" value="Products.Api&#10;(services/products)&#10;ASP.NET Core Web API - vertical-slice&#10;Endpoint: GET /health/live, GET /health/ready&#10;DbContext: ProductsDbContext (chua co bang du lieu)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1020" y="200" width="300" height="110" as="geometry" />
        </mxCell>

        <mxCell id="composebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#999999;dashed=1;verticalAlign=top;fontColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="20" y="400" width="1300" height="140" as="geometry" />
        </mxCell>
        <mxCell id="composelbl" value="docker-compose.deps.yml -- chi khoi dong 4 container SQL Server rieng biet cho local dev (chua chay service nao)" style="text;html=1;fontSize=11;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="40" y="405" width="900" height="20" as="geometry" />
        </mxCell>

        <mxCell id="db1" value="SQL Server (container rieng)&#10;Database: baskets&#10;ConnectionStrings:BasketsDb&#10;Khong service nao khac duoc phep cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="30" y="440" width="300" height="80" as="geometry" />
        </mxCell>
        <mxCell id="db2" value="SQL Server (container rieng)&#10;Database: orders&#10;ConnectionStrings:OrdersDb&#10;Khong service nao khac duoc phep cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="360" y="440" width="300" height="80" as="geometry" />
        </mxCell>
        <mxCell id="db3" value="SQL Server (container rieng)&#10;Database: parties&#10;ConnectionStrings:PartiesDb&#10;Khong service nao khac duoc phep cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="690" y="440" width="300" height="80" as="geometry" />
        </mxCell>
        <mxCell id="db4" value="SQL Server (container rieng)&#10;Database: products&#10;ConnectionStrings:ProductsDb&#10;Khong service nao khac duoc phep cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="1020" y="440" width="300" height="80" as="geometry" />
        </mxCell>

        <mxCell id="guardtitle" value="'Luoi an toan' kien truc tu dong (tests/ o cap solution, khong thuoc service nao)" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="580" width="900" height="24" as="geometry" />
        </mxCell>

        <mxCell id="guard1" value="tests/CrossServiceIsolation.Tests&#10;Quet toan bo appsettings*.json cua 4 service&#10;FAIL build neu 1 service co key/connection-string&#10;tro sang database cua service khac" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="620" width="420" height="100" as="geometry" />
        </mxCell>
        <mxCell id="guard2" value="tests/StructureConventionTests&#10;FAIL build neu service co thu muc&#10;Controllers/, Services/, Repositories/...&#10;Bat buoc moi service co it nhat 1 thu muc Features/&lt;TenNangLuc&gt;" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="480" y="620" width="420" height="100" as="geometry" />
        </mxCell>
        <mxCell id="guard3" value="services/*/tests/*.UnitTests + *.IntegrationTests&#10;IntegrationTests dung Testcontainers.MsSql:&#10;chay SQL Server that trong container de kiem tra&#10;/health/ready tra ve 200/503 dung thuc te" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="930" y="620" width="390" height="100" as="geometry" />
        </mxCell>

        <mxCell id="note1" value="Ghi chu: 2 du an ben trai KHONG tham chieu code cua service nao ca -- chung tu doc file cau hinh / cau truc thu muc tren dia va chay nhu 1 bai test binh thuong trong CI. Day la co che tu dong ngan chan vi pham kien truc truoc khi merge, khong phai kiem tra thu cong." style="text;html=1;fontSize=11;fontColor=#666666;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="30" y="730" width="1290" height="40" as="geometry" />
        </mxCell>

        <mxCell id="e_sd_1" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="svc1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_sd_2" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="svc2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_sd_3" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="svc3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_sd_4" value="project reference (dung chung)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="svc4">
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

        <mxCell id="title2" value="PHAN 2 -- CHUA XAY DUNG, MOI LA KE HOACH (Phase 2-5, xem docs/roadmap.md va docs/system-design.md)" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#a03030;" vertex="1" parent="1">
          <mxGeometry x="30" y="800" width="1000" height="26" as="geometry" />
        </mxCell>

        <mxCell id="futurebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#fafafa;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="20" y="835" width="1300" height="170" as="geometry" />
        </mxCell>

        <mxCell id="f1" value="API Gateway&#10;(YARP)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="40" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f2" value="BFF&#10;(Minimal API)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="200" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f3" value="Identity Server&#10;(Duende)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="360" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f4" value="RabbitMQ +&#10;MassTransit" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="520" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f5" value="Redis&#10;(basket cache)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="680" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f6" value="Logistics&#10;service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="840" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f7" value="Invoices&#10;service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1000" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="f8" value="React SPA&#10;Web + Mobile-web" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1160" y="860" width="140" height="60" as="geometry" />
        </mxCell>
        <mxCell id="fnote" value="Cac khoi nay chi la ban ve trong docs/system-design.md -- CHUA co 1 dong code nao cho chung. Jenkins/CI, Vault, Unleash, Pact Broker cung tuong tu: moi la quyet dinh trong ADR, chua trien khai." style="text;html=1;fontSize=10;fontColor=#a03030;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="40" y="925" width="1260" height="60" as="geometry" />
        </mxCell>

        <mxCell id="lgtitle" value="Chu giai" style="text;html=1;fontStyle=1;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="30" y="1020" width="100" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="30" y="1050" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" value="Service da co code that (4 vo API rong)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1050" width="320" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="30" y="1080" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" value="Thu vien dung chung (khong phai service)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1080" width="320" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="1110" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" value="Du an kiem tra / lam ro kien truc (tests)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1110" width="320" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="450" y="1050" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" value="Chi la ke hoach -- chua co code" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="475" y="1050" width="320" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="450" y="1080" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" value="Co so du lieu (moi service 1 CSDL rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="475" y="1080" width="320" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="450" y="1120" as="sourcePoint" />
            <mxPoint x="500" y="1120" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" value="Quan he that trong code (goi truc tiep)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="510" y="1110" width="320" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="850" y="1120" as="sourcePoint" />
            <mxPoint x="900" y="1120" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" value="Tham chieu thu vien dung chung / kiem tra tinh" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="910" y="1110" width="380" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

---

## Tổng kết ngắn cho quản lý

- Codebase hiện tại = **4 vỏ API rỗng** (chỉ có health-check), **1 thư viện dùng chung**, và **2 cơ chế kiểm tra kiến trúc tự động** rất được đầu tư kỹ dù chưa có tính năng nghiệp vụ nào.
- Đây là lựa chọn có chủ đích của giai đoạn 1 ("chứng minh nền móng độc lập trước, thêm nghiệp vụ sau"), không phải dự án bị chậm tiến độ hay thiếu code.
- Rủi ro lớn nhất đang được người thực hiện tự ghi nhận trong roadmap: việc "giả lập" 1 tenant duy nhất ở giai đoạn này có thể cần làm lại một phần khi tới giai đoạn 3 (bảo mật/đa tenant thật).
- Việc đọc hiểu tiến độ nên dựa vào `docs/roadmap.md` (5 giai đoạn, đang ở giai đoạn 1) hơn là dựa vào độ dày của tài liệu thiết kế trong `docs/` — tài liệu thiết kế đã rất đầy đủ nhưng code mới chỉ bắt đầu.
