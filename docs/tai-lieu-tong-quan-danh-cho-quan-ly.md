# Tài liệu kỹ thuật tổng quan — Nền tảng Ecommerce

*Viết cho: quản lý không trực tiếp code .NET. Mục tiêu: hiểu codebase hiện tại đang có gì, các phần liên hệ với nhau ra sao, và vì sao nó được thiết kế như vậy — không cần đọc code.*

*Cập nhật lần cuối: sau khi hoàn thành 6 tính năng thuộc **Giai đoạn 2 — Kỷ luật hợp đồng & kiểm thử** (SCRUM-17 đến SCRUM-22). **5/6 hạng mục đã xong thật**; hạng mục cuối (CI/CD + SonarQube) đã viết xong toàn bộ code/script nhưng **chưa thực sự chạy** — còn thiếu vài bước bật hệ thống bên ngoài (không phải thiếu code). Xem Mục 1.10 và Mục 5.*

---

## Điều quan trọng nhất cần biết trước khi đọc tiếp

Đây **không phải** một hệ thống thương mại điện tử đang chạy production. Theo [`docs/roadmap.md`](roadmap.md), đây là **dự án luyện tập cá nhân (solo)**, đang triển khai theo 5 giai đoạn.

**Giai đoạn 1 (Walking Skeleton)** đã có bằng chứng đóng chính thức từ lần cập nhật trước ([`docs/demo-phase-1.md`](demo-phase-1.md)). **Giai đoạn 2 (Contract & Test Discipline, SCRUM-17→22)** nay đã **gần như xong** — 5/6 hạng mục hoàn thành thật, 1 hạng mục còn dang dở nhưng đã "sẵn sàng bấm nút":

| Hạng mục Giai đoạn 2 | Trạng thái |
|---|---|
| SCRUM-17 — Hợp đồng OpenAPI cho BFF | **Xong** (đã có sẵn từ trước, tính năng này chỉ xác nhận + gia cố) |
| SCRUM-18 — Schema sự kiện có phiên bản | **Xong phần định nghĩa** — chưa có ai thực sự publish/consume |
| SCRUM-19 — Retrofit TDD cho giỏ hàng/đơn hàng | **Xong** — kiểm chứng lại logic cũ, không sửa gì, không thấy lỗi |
| SCRUM-20 — Test tích hợp qua Testcontainers (SQL/Redis/RabbitMQ) | **Xong phần hạ tầng test** — Redis/RabbitMQ vẫn chưa có code sản phẩm nào gọi tới |
| SCRUM-21 — Contract test theo hướng người tiêu dùng | **Xong** — dùng Pact thật, không dùng Pact Broker |
| SCRUM-22 — Cổng chất lượng SonarQube trong CI | **Viết xong code/script (12/25 nhiệm vụ)** — 13 nhiệm vụ còn lại đều là thao tác của quản trị viên trên hệ thống ngoài repo (bật Jenkins, bật SonarQube server, bật branch protection trên GitHub), **không phải thiếu code** |

Không có dòng nào trong repo tự tuyên bố "Giai đoạn 2 đã xong" — bảng trên là do tôi tổng hợp lại từ `tasks.md` của từng tính năng, không phải trích dẫn nguyên văn như bảng "Evidenced" của Giai đoạn 1.

---

## 1. Giải thích codebase hiện tại

### 1.1–1.9 (không đổi so với bản trước)

Kiến trúc tổng thể, 6 service .NET + giao diện web, luồng mua hàng, cơ chế tenant/người gọi giả lập, cách chạy toàn bộ bằng một lệnh, và chế độ demo — giữ nguyên như các lần cập nhật trước.

**Điều chỉnh số liệu:** [`Ecommerce.slnx`](../Ecommerce.slnx) giờ liệt kê **33 dự án con** (tăng từ 24), do 6 tính năng mới thêm 9 dự án: 1 thư viện định nghĩa sự kiện (`shared/EventContracts` + `EventContracts.UnitTests`), 1 thư viện hạ tầng test cho Redis/RabbitMQ (`shared/IntegrationTestSupport` + `.Tests`), 4 dự án contract-test theo Pact (`Bff/Products/Baskets/Orders.Api.ContractTests`), và 1 dự án kiểm tra độ phủ hợp đồng (`tests/ContractCoverageTests`). `dotnet build` toàn bộ: **vẫn sạch, 0 lỗi, 0 cảnh báo** (33/33 dự án).

### 1.10 Sáu tính năng Giai đoạn 2 — phần lớn là "kiểm chứng/chuẩn bị", không phải "thêm tính năng chạy được"

Đây là điều quan trọng cần hiểu đúng: khác với Giai đoạn 1 (mỗi tính năng đều làm hệ thống *chạy được thêm một việc mới*), phần lớn 6 tính năng Giai đoạn 2 **không thay đổi hành vi hệ thống khi chạy thật** — chúng chứng minh code cũ đã đủ tốt, hoặc chuẩn bị sẵn nền móng cho việc tương lai. Đây là việc làm đúng đắn và cần thiết, chỉ là không nên hiểu nhầm thành "hệ thống làm được nhiều việc hơn".

- **SCRUM-17 (`007-bff-openapi-contracts`)**: mã gọi API tự sinh cho frontend (qua Orval) **đã có sẵn từ tính năng `004`/`005`**. Tính năng này chỉ xác nhận lại toàn bộ chuỗi đó vẫn đúng, và thêm 3 bài test frontend mới kiểm tra "người đọc khoan dung" — tức là khi BFF trả về thêm một trường lạ, giao diện vẫn hiển thị đúng, không bị vỡ. **Không có code sản phẩm mới nào.**

- **SCRUM-18 (`008-versioned-event-schemas`)**: định nghĩa chính thức 2 schema sự kiện `OrderPlaced` và `BasketCheckedOut` (theo chuẩn JSON Schema, đúng như ADR-0005 đã chọn), đặt tại một vị trí dùng chung mới: `shared/EventContracts`. Có cơ chế tự động **chặn build nếu ai đó sửa trực tiếp một schema đã công bố** thay vì tạo phiên bản mới (`SchemaImmutabilityTests` — so khớp mã băm SHA-256 của file schema với một giá trị đã chốt). Nhưng chính tài liệu của tính năng này ghi rõ: *"Chưa có gì tham chiếu tới các schema này. Chưa có broker nào tồn tại... việc nối dây RabbitMQ + MassTransit và publish qua outbox là việc của SCRUM-31."* — nói cách khác, đây là **bản thiết kế hợp đồng cho tương lai**, chưa có ai gửi hay nhận sự kiện thật.

- **SCRUM-19 (`009-retrofit-tdd-basket-order`)**: không sửa bất kỳ dòng code sản phẩm nào. Cách làm: với từng "chốt an toàn" đã có sẵn trong logic tính giá giỏ hàng/tạo đơn hàng (ví dụ chặn số lượng âm, chặn đơn hàng rỗng), **cố tình phá vỡ nó tạm thời**, xác nhận bài test tương ứng báo đỏ, rồi khôi phục lại nguyên trạng và xác nhận báo xanh trở lại. Kết quả: **không tìm thấy lỗi nào** — một cách kiểm chứng khắt khe rằng logic cũ thực sự được test bảo vệ, không phải "trông có vẻ đúng".

- **SCRUM-20 (`010-testcontainers-integration-tests`)**: thêm hạ tầng test dùng container **Redis và RabbitMQ thật** (trước đây chỉ có SQL Server thật trong test), để sẵn sàng cho lúc nào đó có code thật cần dùng tới. Nhưng — đúng như chính tài liệu tính năng ghi lại — **không có code sản phẩm nào gọi tới Redis/RabbitMQ**, y hệt tình trạng đã ghi nhận từ tính năng `005`. Container Redis/RabbitMQ trong `docker-compose.yml` vẫn đang "chạy không" theo đúng nghĩa đen.

- **SCRUM-21 (`011-consumer-contract-tests`)**: đây là tính năng có ý nghĩa thực chất nhất trong 6 tính năng — dùng công cụ **Pact thật** (`PactNet`, đúng công cụ đã chọn từ ADR-0006) để kiểm tra 4 "ranh giới hợp đồng": BFF↔Products, BFF↔Baskets, BFF↔Orders, và một ranh giới thử nghiệm cho sự kiện Orders↔Baskets (`BasketCheckedOut`, dù chưa ai publish/consume thật — chỉ là "hợp đồng đã có sẵn để việc nối dây sau này không phải định nghĩa lại từ đầu"). Điểm khác so với bản thiết kế mục tiêu: ADR-0006 chọn dùng thêm một **Pact Broker** riêng để lưu trữ và theo dõi hợp đồng tập trung — **hiện chưa có Broker nào chạy**, các file hợp đồng (`pacts/*.json`) chỉ được lưu thẳng trong repo.

- **SCRUM-22 (`012-sonarqube-quality-gate`)**: xem chi tiết riêng ở Mục 1.11 vì đây là hạng mục lớn nhất và chưa hoàn tất.

### 1.11 CI/CD + cổng chất lượng SonarQube — đã viết xong, chưa thực sự bật (mới, chưa hoàn tất)

Đây là lần đầu tiên repo có **cấu hình CI/CD thật** — trước đây hoàn toàn không có gì (không `Jenkinsfile`, không `.github/workflows`). Giờ đã có:

- **`Jenkinsfile`** (184 dòng, ở gốc repo) — một pipeline khai báo thật, đủ 5 bước theo đúng thứ tự: bắt đầu phân tích SonarQube → build (cả .NET lẫn frontend) → unit test → integration test (dùng Testcontainers) → contract test → **cổng chất lượng SonarQube** (chờ tối đa 15 phút, **chặn cứng nếu kết quả không phải "OK"**).
- Các script hỗ trợ (`scripts/ci/*.sh`), công cụ đã ghim phiên bản (`dotnet-sonarscanner`, `dotnet-coverage`), và file cấu hình `sonar-project.properties` — đều đã tồn tại và dùng được.
- Một quyết định kiến trúc mới, [`docs/adr/0012-ci-quality-gate-enforcement.md`](adr/0012-ci-quality-gate-enforcement.md), ghi lại rõ ràng lý do và cách làm.

**Nhưng — và đây là điểm quan trọng nhất — chưa có gì THỰC SỰ chạy.** `tasks.md` của tính năng này có hệ thống đánh dấu riêng: `[X]` = đã xong và tự kiểm chứng được trên máy; **`[ ] ⛔` = bị chặn vì cần quyền truy cập vào một hệ thống bên ngoài repo**. Cả **13 nhiệm vụ còn lại đều mang dấu ⛔** — không phải vì thiếu ai viết code, mà vì cần một quản trị viên thực hiện các bước ngoài repo: cài đặt Jenkins + kết nối tới SonarQube server thật, tạo pipeline job trên Jenkins, và **bật quy tắc bảo vệ nhánh (branch protection) trên GitHub** để pipeline thực sự trở thành điều kiện bắt buộc trước khi merge.

**Nói thẳng: hôm nay, chưa có gì ngăn được việc merge code mà không qua kiểm tra nào cả** — pipeline đã sẵn sàng "cắm điện là chạy", nhưng chưa ai cắm điện.

---

## 2. Các dự án/component và cách chúng phụ thuộc lẫn nhau

Không đổi về cấu trúc lõi (6 service, Gateway, BFF, 2 thư viện tenant/log cũ) so với các bản trước. Bổ sung 3 nhóm dự án mới từ Giai đoạn 2 (Mục 1.10–1.11): `shared/EventContracts` (định nghĩa sự kiện, chưa ai dùng), `shared/IntegrationTestSupport` (hạ tầng test Redis/RabbitMQ, chưa service nào gọi), và 5 dự án contract-test theo Pact (`*.ContractTests` + `ContractCoverageTests`) — tất cả đều là dự án **kiểm thử/định nghĩa**, không phải service chạy thật, và không được service nào tham chiếu ngược lại.

---

## 3. Mục đích từng phần + các tình huống thực tế được giải quyết

### 3.1–3.2 (không đổi — thêm 2 tình huống mới)

Giữ nguyên 12 tình huống đã liệt kê ở các bản cập nhật trước, bổ sung:

**13) "Không tìm thấy lỗi" cũng là một kết quả có giá trị, nếu cách kiểm chứng đủ khắt khe.**
Tính năng `009` không sửa một dòng code sản phẩm nào — nhưng không phải vì làm qua loa. Cách làm (cố tình phá vỡ từng chốt an toàn, xác nhận test báo đỏ, rồi khôi phục) chứng minh được rằng logic tính giá giỏ hàng và tạo đơn hàng **thực sự** được test bảo vệ, chứ không chỉ "nhìn có vẻ ổn". Đây là sự khác biệt giữa "không ai tìm ra lỗi vì không ai kiểm tra kỹ" và "đã kiểm tra kỹ và không có lỗi" — quản lý nên phân biệt được hai điều này khi nghe báo cáo "test đều pass".

**14) Một hệ thống CI/CD "đã viết xong" và một hệ thống CI/CD "đang bảo vệ nhánh chính" là hai việc khác nhau — và khoảng cách giữa chúng thường là quyền quản trị, không phải kỹ thuật.**
`Jenkinsfile` và toàn bộ script hỗ trợ đã sẵn sàng, đã được xác minh chạy đúng trên máy cá nhân. Nhưng để nó thực sự **chặn được merge**, cần ba hành động của quản trị viên nằm ngoài phạm vi một lập trình viên tự làm được: cấp quyền kết nối Jenkins↔SonarQube, tạo pipeline job, và bật branch protection trên GitHub. Đây là một điểm nghẽn kiểu tổ chức (ai có quyền bấm nút), không phải điểm nghẽn kỹ thuật — quản lý nên lưu ý loại điểm nghẽn này thường bị bỏ sót khi ước tính "còn bao lâu nữa xong".

---

## 4. Sơ đồ kiến trúc hiện tại (dùng được với draw.io)

Sơ đồ vẽ **đúng những gì đang có trong code hôm nay**. Phần dưới đã được **đổi tên** để tránh nhầm với "Giai đoạn 2" thật của roadmap (vốn nay đã gần xong) — nay gọi là "chưa xây dựng, thuộc Giai đoạn 3-5".

> **Lưu ý phân biệt:** repo có sẵn 3 sơ đồ khác ở [`docs/system-design.md`](system-design.md) — nhưng chúng vẽ **kiến trúc mục tiêu đầy đủ**, không phải trạng thái hiện tại.

### Cách dùng
1. Mở [app.diagrams.net](https://app.diagrams.net) (draw.io).
2. Vào menu **Extras → Edit Diagram…**
3. Xoá nội dung trống, dán toàn bộ khối XML bên dưới vào, bấm **Save/OK**.

*(Hoặc mở trực tiếp file [`docs/diagrams/current-state-architecture.drawio`](diagrams/current-state-architecture.drawio) đã có sẵn trong repo — nội dung giống hệt khối XML bên dưới.)*

```xml
<mxfile host="Electron" agent="5.0">
  <diagram id="current-state-005" name="Trạng thái hiện tại — sau 005">
    <mxGraphModel dx="3279" dy="737" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1500" pageHeight="2060" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <mxCell id="title1" parent="1" style="text;html=1;fontStyle=1;fontSize=15;fontColor=#2d6a2d;" value="PHẦN 1 — ĐÃ CÓ TRONG CODE HÔM NAY (33 dự án .NET + 1 workspace frontend; 1 LỆNH DUY NHẤT chạy toàn bộ stack; xem docs/demo-phase-1.md để có bằng chứng Giai đoạn 1 đã xong)" vertex="1">
          <mxGeometry height="26" width="1450" x="30" as="geometry" />
        </mxCell>
        <mxCell id="client" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=11;" value="Storefront (frontend/apps/web)&#xa;CÁCH 1 — 1 lệnh, docker (mới từ 005): container nginx, cổng 4173,&#xa;  từ Dockerfile riêng, CHỈ gọi Gateway qua cổng 5300&#xa;CÁCH 2 — dev thủ công: pnpm dev, Vite, cổng 5173&#xa;3 màn hình: Sản phẩm (/) - Giỏ hàng (/basket) - Xác nhận (/confirmation)" vertex="1">
          <mxGeometry height="115" width="460" x="30" y="50" as="geometry" />
        </mxCell>
        <mxCell id="sd" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" value="shared/ServiceDefaults&#xa;(thư viện dùng chung, KHÔNG phải service)&#xa;- OpenTelemetry: log / trace / metric -&gt; OTel Collector thật&#xa;- Correlation-Id (X-Correlation-Id): sinh ở Gateway,&#xa;  ghi vào request để mọi hop sau dùng chung 1 mã" vertex="1">
          <mxGeometry height="115" width="290" x="860" y="50" as="geometry" />
        </mxCell>
        <mxCell id="tenancy" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" value="shared/Tenancy&#xa;(thư viện dùng chung, KHÔNG phải service)&#xa;- TenantContext.RequireTenantId()&#xa;- CallerContext.RequireSubjectId()&#xa;- Header: X-Tenant-Id + X-Subject-Id&#xa;- Chặn MỌI kết nối CSDL nếu thiếu 1 trong 2" vertex="1">
          <mxGeometry height="115" width="260" x="1170" y="50" as="geometry" />
        </mxCell>
        <mxCell id="gw" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" value="Gateway.Api (services/gateway) — YARP&#xa;Cổng 5300 — DUY NHẤT cổng backend được công bố ra ngoài&#xa;Bảng route = 1 dòng duy nhất: MỌI đường dẫn -&gt; BFF&#xa;StubIdentity: gán cố định tenant &quot;contoso&quot; + người dùng&#xa;&quot;phase1-stub-user&quot;, ghi đè X-Tenant-Id + X-Subject-Id&#xa;Docker: tự khởi động sau khi BFF báo healthy (/health/ready)" vertex="1">
          <mxGeometry height="140" width="460" x="30" y="185" as="geometry" />
        </mxCell>
        <mxCell id="gwnote" parent="1" style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" value="CORS: CHỈ 2 origin được phép gọi — localhost:5173 (dev) và localhost:4173&#xa;(storefront docker). StorefrontCorsTests canh đúng điều này.&#xa;&#xa;Gateway tự healthcheck bằng /health/live (còn sống), KHÔNG phải /health/ready&#xa;— để Gateway vẫn đứng vững khi 1 service phía sau đang lỗi, thay vì tự&#xa;rút mình ra khỏi vòng lặp và làm cả hệ thống sập theo." vertex="1">
          <mxGeometry height="115" width="660" x="510" y="165" as="geometry" />
        </mxCell>
        <mxCell id="bff" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" value="Bff.Api (services/bff) — Backend For Frontend&#xa;Cổng 5301 — CHỈ mở trong chế độ debug, không công bố mặc định&#xa;GET /bff/products, GET/POST /bff/basket, POST /bff/basket/items,&#xa;POST /bff/checkout, GET /bff/orders/{id}&#xa;Docker: tự khởi động sau khi CẢ 4 service nghiệp vụ báo healthy" vertex="1">
          <mxGeometry height="130" width="460" x="30" y="345" as="geometry" />
        </mxCell>
        <mxCell id="bffnote" parent="1" style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" value="Ngân sách thời gian mỗi lời gọi ra ngoài: 1s/lần thử (tối đa 2 lần thử lại) | tối đa 3s tổng cộng | cầu dao ngắt sau 10s lỗi liên tục&#xa;502 = downstream không kết nối được    504 = vượt thời gian chờ    500 = lỗi của chính BFF    404 = không tìm thấy (KHÔNG phải lỗi)&#xa;&#xa;POST /bff/checkout — điều phối đồng bộ 3 bước theo ĐÚNG thứ tự (ADR-0011): 1. đọc giỏ hàng  2. giỏ RỖNG -&gt; 409, dừng ngay&#xa;3. tạo đơn hàng (gọi Orders)  4. XOÁ giỏ hàng — CHỈ SAU KHI đã có đơn thật. KHÔNG phải saga/outbox chuẩn — sai lệch có ghi chép." vertex="1">
          <mxGeometry height="100" width="890" x="500" y="325" as="geometry" />
        </mxCell>
        <mxCell id="svcnote" parent="1" style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;" value="4 service nghiệp vụ HOÀN TOÀN không biết đến nhau: không cái nào gọi cái nào, không cái nào tham chiếu code của cái nào." vertex="1">
          <mxGeometry height="18" width="1390" x="30" y="490" as="geometry" />
        </mxCell>
        <mxCell id="mig1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" value="products-migrate&#xa;(1 lần, chờ sqlserver&#xa;healthy, áp migration&#xa;+ seed 3 sản phẩm)" vertex="1">
          <mxGeometry height="55" width="320" x="30" y="515" as="geometry" />
        </mxCell>
        <mxCell id="mig2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" value="baskets-migrate&#xa;(1 lần, chờ sqlserver&#xa;healthy, áp migration)" vertex="1">
          <mxGeometry height="55" width="320" x="380" y="515" as="geometry" />
        </mxCell>
        <mxCell id="mig3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" value="orders-migrate&#xa;(1 lần, chờ sqlserver&#xa;healthy, áp migration)" vertex="1">
          <mxGeometry height="55" width="320" x="730" y="515" as="geometry" />
        </mxCell>
        <mxCell id="mig4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" value="parties-migrate&#xa;(1 lần, chờ sqlserver&#xa;healthy, áp migration)" vertex="1">
          <mxGeometry height="55" width="320" x="1080" y="515" as="geometry" />
        </mxCell>
        <mxCell id="mignote" parent="1" style="text;html=1;fontSize=9;fontColor=#888888;whiteSpace=wrap;" value="4 &quot;migrator&quot; này chạy ĐÚNG 1 LẦN mỗi lần lên stack, từ Dockerfile của chính service (target riêng), rồi tự thoát. Service nghiệp vụ&#xa;tương ứng CHỈ tự khởi động sau khi migrator của nó báo &quot;hoàn tất&quot; (docker depends_on: service_completed_successfully)." vertex="1">
          <mxGeometry height="28" width="1150" x="270" y="573" as="geometry" />
        </mxCell>
        <mxCell id="svc1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" value="Products.Api&#xa;Cổng 5088 (chỉ mở trong chế độ debug)&#xa;GET /products — 3 sản phẩm mẫu thật&#xa;+ /health/live, /health/ready&#xa;Bảng: Product (Id, Name, Price)&#xa;Chặn CSDL nếu thiếu X-Tenant-Id / X-Subject-Id" vertex="1">
          <mxGeometry height="140" width="320" x="30" y="610" as="geometry" />
        </mxCell>
        <mxCell id="svc2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" value="Baskets.Api&#xa;Cổng 5188 (chỉ mở trong chế độ debug)&#xa;Giỏ hàng của người gọi (CustomerRef)&#xa;+ /health/live, /health/ready&#xa;Bảng: Basket (Total tính tại chỗ) + BasketLineItem&#xa;Chặn CSDL nếu thiếu X-Tenant-Id / X-Subject-Id" vertex="1">
          <mxGeometry height="140" width="320" x="380" y="610" as="geometry" />
        </mxCell>
        <mxCell id="svc3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" value="Orders.Api&#xa;Cổng 5041 (chỉ mở trong chế độ debug, hoặc chế độ demo)&#xa;Tạo đơn hàng từ dòng giỏ hàng; đọc lại theo id&#xa;+ /health/live, /health/ready&#xa;Bảng: Order (Id, PlacedAtUtc, Total, TenantId — chỉ là&#xa;NHÃN, không phải ngăn cách vật lý, xem mục 1.3)&#xa;Chặn CSDL nếu thiếu X-Tenant-Id / X-Subject-Id" vertex="1">
          <mxGeometry height="140" width="320" x="730" y="610" as="geometry" />
        </mxCell>
        <mxCell id="svc4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" value="Parties.Api&#xa;Cổng 5204 (chỉ mở trong chế độ debug)&#xa;GET /parties/{id}&#xa;+ /health/live, /health/ready&#xa;Bảng: Party (Id, DisplayName)&#xa;Chặn CSDL nếu thiếu X-Tenant-Id / X-Subject-Id" vertex="1">
          <mxGeometry height="140" width="320" x="1080" y="610" as="geometry" />
        </mxCell>
        <mxCell id="composebg" parent="1" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#999999;dashed=1;verticalAlign=top;fontColor=#666666;fontSize=11;" value="" vertex="1">
          <mxGeometry height="110" width="1400" x="20" y="765" as="geometry" />
        </mxCell>
        <mxCell id="composelbl" parent="1" style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;" value="docker-compose.yml (MỚI, project &quot;ecomerce-stack&quot;) — 1 SQL Server dùng chung, mỗi service 1 database riêng bên trong (không còn 4 container SQL riêng biệt ở đường dẫn này). docker-compose.deps.yml cũ (4 container SQL riêng) vẫn còn, dùng khi chạy từng service riêng lẻ để debug." vertex="1">
          <mxGeometry height="35" width="1360" x="40" y="772" as="geometry" />
        </mxCell>
        <mxCell id="db1" parent="1" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" value="Database: products&#xa;(trong 1 container SQL&#xa;Server dùng chung)" vertex="1">
          <mxGeometry height="55" width="320" x="30" y="815" as="geometry" />
        </mxCell>
        <mxCell id="db2" parent="1" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" value="Database: baskets&#xa;(trong 1 container SQL&#xa;Server dùng chung)" vertex="1">
          <mxGeometry height="55" width="320" x="380" y="815" as="geometry" />
        </mxCell>
        <mxCell id="db3" parent="1" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" value="Database: orders&#xa;(trong 1 container SQL&#xa;Server dùng chung)" vertex="1">
          <mxGeometry height="55" width="320" x="730" y="815" as="geometry" />
        </mxCell>
        <mxCell id="db4" parent="1" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" value="Database: parties&#xa;(trong 1 container SQL&#xa;Server dùng chung)" vertex="1">
          <mxGeometry height="55" width="320" x="1080" y="815" as="geometry" />
        </mxCell>
        <mxCell id="infratitle" parent="1" style="text;html=1;fontStyle=1;fontSize=11;fontColor=#666666;" value="Hạ tầng chạy kèm trong docker-compose.yml — không phải service nghiệp vụ" vertex="1">
          <mxGeometry height="20" width="700" x="30" y="885" as="geometry" />
        </mxCell>
        <mxCell id="otel" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=10;" value="OTel Collector&#xa;ĐÃ được dùng thật: nhận log/trace/metric&#xa;từ cả 6 service qua ServiceDefaults.&#xa;Không thể healthcheck (image không có shell)." vertex="1">
          <mxGeometry height="80" width="450" x="30" y="910" as="geometry" />
        </mxCell>
        <mxCell id="redis" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#999999;fontSize=10;" value="Redis&#xa;Chạy và có healthcheck, nhưng CHƯA có&#xa;service nào kết nối vào (có chủ đích,&#xa;xem FR-017 của tính năng 005)" vertex="1">
          <mxGeometry height="80" width="440" x="500" y="910" as="geometry" />
        </mxCell>
        <mxCell id="rabbitmq" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#999999;fontSize=10;" value="RabbitMQ&#xa;Chạy và có healthcheck, nhưng CHƯA có&#xa;service nào kết nối vào — có hạ tầng&#xa;test thật từ 010, vẫn chưa có code&#xa;sản phẩm nào gọi tới" vertex="1">
          <mxGeometry height="80" width="440" x="960" y="910" as="geometry" />
        </mxCell>
        <mxCell id="guardtitle" parent="1" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" value="&#39;Lưới an toàn&#39; kiến trúc tự động — chạy như bài test mỗi lần build, FAIL build khi bị vi phạm (7 loại)" vertex="1">
          <mxGeometry height="24" width="1100" x="30" y="1005" as="geometry" />
        </mxCell>
        <mxCell id="guard1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" value="tests/CrossServiceIsolation.Tests&#xa;- Service A cấm chuỗi kết nối CSDL của service B&#xa;- Gateway/BFF cấm BẤT KỲ chuỗi kết nối nào&#xa;- Mỗi service phải có ĐÚNG 1 điểm khởi tạo&#xa;  DbContext, gọi RequireTenantId()" vertex="1">
          <mxGeometry height="115" width="345" x="30" y="1040" as="geometry" />
        </mxCell>
        <mxCell id="guard2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" value="tests/StructureConventionTests&#xa;FAIL build nếu service có thư mục Controllers/,&#xa;Services/, Repositories/... Bắt buộc mỗi service&#xa;có ít nhất 1 thư mục Features/&lt;TênNăngLực&gt;" vertex="1">
          <mxGeometry height="115" width="345" x="385" y="1040" as="geometry" />
        </mxCell>
        <mxCell id="guard3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" value="Gateway.Api Tests&#xa;- RouteConfigurationTests: sai chính tả route,&#xa;  hoặc route đi thẳng tới service nghiệp vụ&#xa;- ForwardingTimeoutBudgetTests: timeout Gateway&#xa;  (10s) &lt; ngân sách 3s của BFF" vertex="1">
          <mxGeometry height="115" width="345" x="740" y="1040" as="geometry" />
        </mxCell>
        <mxCell id="guard4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" value="Bff.Api.IntegrationTests/&#xa;GeneratedContractTests&#xa;Tài liệu API sinh tự động phải khai báo ĐỦ cả&#xa;404/502/504, không chỉ thành công — frontend&#xa;build LỖI ngay nếu hợp đồng lệch" vertex="1">
          <mxGeometry height="115" width="335" x="1095" y="1040" as="geometry" />
        </mxCell>
        <mxCell id="guard5" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" value="services/*/tests/*.IntegrationTests — Testcontainers.MsSql: chạy SQL Server THẬT trong container, không dùng giả lập.&#xa;Test của BFF còn chạy cả 4 service THẬT trong bộ nhớ để kiểm tra thật sự gọi được." vertex="1">
          <mxGeometry height="90" width="460" x="30" y="1165" as="geometry" />
        </mxCell>
        <mxCell id="guard6" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" value="*/tests/*.IntegrationTests/TenantEnforcementTests (1 bộ / service nghiệp vụ)&#xa;Request KHÔNG có X-Tenant-Id / X-Subject-Id phải nhận lỗi 500, không được âm thầm trả về dữ liệu." vertex="1">
          <mxGeometry height="90" width="460" x="500" y="1165" as="geometry" />
        </mxCell>
        <mxCell id="guard7" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" value="tests/ContainerConventionTests (MỚI từ 005)&#xa;Mọi Dockerfile phải COPY ĐỦ các thư viện shared/* mà .csproj tham chiếu — đã bắt 5/6 image&#xa;không build được (thiếu shared/Tenancy trong Dockerfile) trước khi sửa." vertex="1">
          <mxGeometry height="90" width="460" x="970" y="1165" as="geometry" />
        </mxCell>
        <mxCell id="opstitle" parent="1" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" value="Vận hành: 3 script + kiểm tra điều kiện trước + &quot;làm ấm&quot; sau khi lên (mới từ 005)" vertex="1">
          <mxGeometry height="24" width="1000" x="30" y="1275" as="geometry" />
        </mxCell>
        <mxCell id="opsbox" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#eef7ee;strokeColor=#2d6a2d;fontSize=10;align=left;spacingLeft=8;" value="scripts/up.sh | up.ps1 — kiểm tra TRƯỚC khi khởi động bất kỳ container nào: Docker đã cài, Docker daemon đang chạy, file .env tồn tại, RAM Docker &gt;= 6GB.&#xa;Chạy &quot;docker compose up --build --wait&quot;. Sau khi TẤT CẢ báo healthy, tự gọi thử 3 request qua Gateway (/bff/products, /bff/basket, /bff/orders/...) để&#xa;&quot;làm ấm&quot; JIT/EF Core/connection-pool — nếu thiếu bước này, request THẬT đầu tiên của khách gặp lỗi 504 (vượt ngân sách 3s của BFF).&#xa;Đây là lỗi THẬT đã gặp và sửa trong tính năng 005, không phải giả định.&#xa;&#xa;scripts/down.sh | down.ps1 — dừng stack, GIỮ dữ liệu (volume không bị xoá).      scripts/reset.sh | reset.ps1 — dừng stack VÀ XOÁ sạch dữ liệu.&#xa;Chế độ debug (up.sh --debug / up.ps1 -PublishInternalPorts, dựa trên docker-compose.debug.yml) — mở thêm cổng nội bộ (BFF, từng service, RabbitMQ UI) để debug." vertex="1">
          <mxGeometry height="115" width="1390" x="30" y="1305" as="geometry" />
        </mxCell>
        <mxCell id="e_client_gw" edge="1" parent="1" source="client" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" target="gw" value="HTTP :5300 (CORS: chỉ 5173 / 4173)">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_gw_bff" edge="1" parent="1" source="gw" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" target="bff" value="YARP chuyển tiếp TẤT CẢ + X-Tenant-Id + X-Subject-Id + X-Correlation-Id">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_1" edge="1" parent="1" source="bff" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" target="svc2" value="1: đọc giỏ hàng / 4: xoá giỏ hàng">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="260.03" y="490" />
              <mxPoint x="710.03" y="490" />
              <mxPoint x="710.03" y="680" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_bff_3" edge="1" parent="1" source="bff" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" target="svc3" value="3: tạo đơn hàng">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1060" y="440" />
              <mxPoint x="1060" y="680" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_bff_1p" edge="1" parent="1" source="bff" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" target="svc1" value="GET /bff/products">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="20" y="410" />
              <mxPoint x="20" y="680" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_bff_4" edge="1" parent="1" source="bff" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" target="svc4">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1420" y="410" />
              <mxPoint x="1420" y="680" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_mig1_svc1" edge="1" parent="1" source="mig1" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" target="svc1" value="docker depends_on: hoàn tất">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db1" edge="1" parent="1" source="svc1" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" target="db1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db2" edge="1" parent="1" source="svc2" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" target="db2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db3" edge="1" parent="1" source="svc3" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" target="db3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db4" edge="1" parent="1" source="svc4" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" target="db4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_sd_gw" edge="1" parent="1" source="sd" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" target="gw" value="tham chiếu thư viện (cả 6 service)">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="920" y="165" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_tn_gw" edge="1" parent="1" source="tenancy" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" target="gw" value="tham chiếu thư viện (cả 6 service)">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1300" y="280" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_sd_otel" edge="1" parent="1" source="sd" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" target="otel" value="OTLP export (cả 6 service, qua ServiceDefaults)">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1005" y="40" />
              <mxPoint x="10" y="40" />
              <mxPoint x="10" y="950" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="title2" parent="1" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#2d6a2d;" value="GIAI ĐOẠN 2 CỦA ROADMAP — KỶ LUẬT HỢP ĐỒNG &amp; KIỂM THỬ (SCRUM-17..22) — 5/6 ĐÃ XONG THẬT, 1 (SCRUM-22) ĐÃ VIẾT XONG CODE NHƯNG CHƯA BẬT" vertex="1">
          <mxGeometry height="26" width="1400" x="30" y="1440" as="geometry" />
        </mxCell>
        <mxCell id="p2s1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=9;" value="SCRUM-17 (007)&#xa;Hợp đồng OpenAPI cho BFF&#xa;ĐÃ XONG — đã có sẵn từ trước (Orval),&#xa;007 chỉ xác nhận + thêm 3 test&#xa;&quot;người đọc khoan dung&quot; ở frontend" vertex="1">
          <mxGeometry height="140" width="220" x="30" y="1475" as="geometry" />
        </mxCell>
        <mxCell id="p2s2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" value="SCRUM-18 (008)&#xa;Schema sự kiện có phiên bản&#xa;(OrderPlaced, BasketCheckedOut)&#xa;ĐÃ ĐỊNH NGHĨA xong (shared/&#xa;EventContracts), NHƯNG chưa có&#xa;ai publish/consume thật" vertex="1">
          <mxGeometry height="140" width="220" x="256" y="1475" as="geometry" />
        </mxCell>
        <mxCell id="p2s3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=9;" value="SCRUM-19 (009)&#xa;Retrofit TDD cho giá giỏ hàng&#xa;+ tạo đơn hàng&#xa;ĐÃ KIỂM CHỨNG bằng cách phá rồi&#xa;phục hồi code — KHÔNG sửa gì,&#xa;không tìm thấy lỗi nào" vertex="1">
          <mxGeometry height="140" width="220" x="482" y="1475" as="geometry" />
        </mxCell>
        <mxCell id="p2s4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;fontSize=9;" value="SCRUM-20 (010)&#xa;Test tích hợp qua Testcontainers&#xa;(SQL Server + Redis + RabbitMQ)&#xa;ĐÃ CÓ hạ tầng test THẬT cho Redis/&#xa;RabbitMQ, nhưng KHÔNG có code sản&#xa;phẩm nào gọi tới (vẫn nhàn rỗi)" vertex="1">
          <mxGeometry height="140" width="220" x="708" y="1475" as="geometry" />
        </mxCell>
        <mxCell id="p2s5" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=9;" value="SCRUM-21 (011)&#xa;Contract test theo hướng người tiêu dùng&#xa;Dùng PactNet thật, 4 ranh giới&#xa;(BFF↔3 service, Orders↔Baskets&#xa;qua event) — file .json lưu ngay&#xa;trong repo, KHÔNG có Pact Broker" vertex="1">
          <mxGeometry height="140" width="220" x="934" y="1475" as="geometry" />
        </mxCell>
        <mxCell id="p2s6" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6b3;strokeColor=#bf9000;dashed=1;fontColor=#7f6000;fontSize=9;" value="SCRUM-22 (012)&#xa;Jenkins + SonarQube quality gate&#xa;ĐÃ VIẾT XONG code/script (Jenkinsfile,&#xa;12/25 nhiệm vụ) — 13 nhiệm vụ còn lại&#xa;CẦN quản trị viên bật Jenkins/SonarQube&#xa;thật + bật branch protection GitHub" vertex="1">
          <mxGeometry height="140" width="240" x="1160" y="1475" as="geometry" />
        </mxCell>
        <mxCell id="p2note" parent="1" style="text;html=1;fontSize=10;fontColor=#2d6a2d;whiteSpace=wrap;" value="Khác với Giai đoạn 1: phần lớn 6 hạng mục này KHÔNG làm hệ thống chạy được thêm việc gì mới — chúng chứng minh code cũ đủ vững (009), hoặc chuẩn bị sẵn hợp đồng cho tương lai (008, 010, 011) mà chưa nối dây thật. Hôm nay CHƯA có gì ngăn được việc merge code mà không qua kiểm tra nào — Jenkinsfile đã sẵn sàng &quot;cắm điện là chạy&quot;, nhưng chưa ai cắm điện (SCRUM-22)." vertex="1">
          <mxGeometry height="45" width="1390" x="30" y="1622" as="geometry" />
        </mxCell>
        <mxCell id="title3" parent="1" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#a03030;" value="CHƯA XÂY DỰNG — thuộc Giai đoạn 3-5 của roadmap (xem docs/roadmap.md)" vertex="1">
          <mxGeometry height="26" width="1000" x="30" y="1685" as="geometry" />
        </mxCell>
        <mxCell id="futurebg" parent="1" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#fafafa;strokeColor=#cc6666;dashed=1;" value="" vertex="1">
          <mxGeometry height="130" width="1400" x="20" y="1720" as="geometry" />
        </mxCell>
        <mxCell id="f1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Identity Server thật&#xa;(Duende)&#xa;(SCRUM-23, Giai&#xa;đoạn 3)" vertex="1">
          <mxGeometry height="80" width="255" x="40" y="1745" as="geometry" />
        </mxCell>
        <mxCell id="f2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Ngăn cách VẬT LÝ&#xa;theo tenant (đã thử,&#xa;đã chủ động huỷ —&#xa;xem mục 1.3)" vertex="1">
          <mxGeometry height="80" width="255" x="320" y="1745" as="geometry" />
        </mxCell>
        <mxCell id="f3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Nối dây Saga+Outbox&#xa;thật (SCRUM-31) — đã&#xa;có sẵn schema (008) +&#xa;hợp đồng Pact (011)" vertex="1">
          <mxGeometry height="80" width="255" x="600" y="1745" as="geometry" />
        </mxCell>
        <mxCell id="f4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Logistics + Invoices&#xa;service (sẽ dùng Redis/&#xa;RabbitMQ đang chạy&#xa;nhưng còn trống)" vertex="1">
          <mxGeometry height="80" width="255" x="880" y="1745" as="geometry" />
        </mxCell>
        <mxCell id="f5" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Bật Jenkins/SonarQube&#xa;thật + branch protection;&#xa;Vault, Unleash,&#xa;Pact Broker" vertex="1">
          <mxGeometry height="80" width="240" x="1160" y="1745" as="geometry" />
        </mxCell>
        <mxCell id="fnote" parent="1" style="text;html=1;fontSize=10;fontColor=#a03030;whiteSpace=wrap;" value="Web SPA (SCRUM-14), chạy-toàn-bộ-bằng-1-lệnh (SCRUM-15), và toàn bộ Giai đoạn 2 (SCRUM-17..21) ĐÃ chuyển lên các phần trên, không còn là kế hoạch nữa. SCRUM-22 nằm ở trạng thái trung gian — xem hộp màu vàng bên trên." vertex="1">
          <mxGeometry height="35" width="1360" x="35" y="1855" as="geometry" />
        </mxCell>
        <mxCell id="lgtitle" parent="1" style="text;html=1;fontStyle=1;fontSize=12;" value="Chú giải" vertex="1">
          <mxGeometry height="20" width="100" x="30" y="1905" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1">
          <mxGeometry height="20" width="20" x="30" y="1935" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" parent="1" style="text;html=1;fontSize=11;" value="Service nghiệp vụ / hạng mục Giai đoạn 2 đã xong thật" vertex="1">
          <mxGeometry height="20" width="360" x="55" y="1935" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;" vertex="1">
          <mxGeometry height="20" width="20" x="30" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" parent="1" style="text;html=1;fontSize=11;" value="Service ở biên (không sở hữu dữ liệu)" vertex="1">
          <mxGeometry height="20" width="300" x="55" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc7" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;" vertex="1">
          <mxGeometry height="20" width="20" x="430" y="1935" as="geometry" />
        </mxCell>
        <mxCell id="lgc7t" parent="1" style="text;html=1;fontSize=11;" value="Giao diện web (frontend, ngoài .slnx)" vertex="1">
          <mxGeometry height="20" width="300" x="455" y="1935" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1">
          <mxGeometry height="20" width="20" x="430" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" parent="1" style="text;html=1;fontSize=11;" value="Thư viện dùng chung / hạ tầng ĐÃ dùng thật (OTel)" vertex="1">
          <mxGeometry height="20" width="340" x="455" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc8" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;" vertex="1">
          <mxGeometry height="20" width="20" x="810" y="1935" as="geometry" />
        </mxCell>
        <mxCell id="lgc8t" parent="1" style="text;html=1;fontSize=11;" value="Đã tồn tại (migrator, schema, test-fixture) nhưng CHƯA nối dây/CHƯA dùng thật" vertex="1">
          <mxGeometry height="20" width="420" x="835" y="1935" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1">
          <mxGeometry height="20" width="20" x="810" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" parent="1" style="text;html=1;fontSize=11;" value="Lưới an toàn kiến trúc (tests) — 7 loại" vertex="1">
          <mxGeometry height="20" width="330" x="835" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc9" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6b3;strokeColor=#bf9000;dashed=1;" vertex="1">
          <mxGeometry height="20" width="20" x="1210" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc9t" parent="1" style="text;html=1;fontSize=11;" value="Code/script đã viết, CHƯA bật hạ tầng ngoài" vertex="1">
          <mxGeometry height="20" width="260" x="1235" y="1965" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1">
          <mxGeometry height="20" width="20" x="30" y="1995" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" parent="1" style="text;html=1;fontSize=11;" value="Cơ sở dữ liệu" vertex="1">
          <mxGeometry height="20" width="160" x="55" y="1995" as="geometry" />
        </mxCell>
        <mxCell id="lgc6" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;" vertex="1">
          <mxGeometry height="20" width="20" x="260" y="1995" as="geometry" />
        </mxCell>
        <mxCell id="lgc6t" parent="1" style="text;html=1;fontSize=11;" value="Chỉ là kế hoạch — chưa có code (Giai đoạn 3-5)" vertex="1">
          <mxGeometry height="20" width="330" x="285" y="1995" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" edge="1" parent="1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="650" y="2005" as="sourcePoint" />
            <mxPoint x="700" y="2005" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" parent="1" style="text;html=1;fontSize=11;" value="Gọi thật lúc chạy (HTTP / EF Core)" vertex="1">
          <mxGeometry height="20" width="260" x="710" y="1995" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" edge="1" parent="1" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="1000" y="2005" as="sourcePoint" />
            <mxPoint x="1050" y="2005" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" parent="1" style="text;html=1;fontSize=11;" value="Tham chiếu thư viện / phụ thuộc lúc build hoặc lúc khởi động (docker depends_on)" vertex="1">
          <mxGeometry height="20" width="440" x="1060" y="1995" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

---

## 5. Rủi ro và việc còn treo

**1) [MỚI] CI/CD đã viết xong nhưng chưa bảo vệ được gì.** Xem chi tiết Mục 1.11. Đây là rủi ro **quy trình**, không phải rủi ro code: bất kỳ ai cũng có thể merge code hôm nay mà không qua bất kỳ kiểm tra tự động nào, dù toàn bộ công cụ đã sẵn sàng. Cần 3 hành động của quản trị viên (không phải lập trình viên) để kích hoạt thật.

**2) Ngăn cách vật lý dữ liệu theo tenant — vẫn "chưa ai nhận trách nhiệm".** Không đổi từ các bản trước, không có tính năng nào trong 6 tính năng vừa xong chạm tới việc này.

**3) Chưa có cơ chế saga/bù trừ hoặc outbox cho thanh toán — nhưng nay đã có "bản vẽ hợp đồng" sẵn sàng.** Trước đây hoàn toàn chưa có gì; nay tính năng `008` đã định nghĩa sẵn schema sự kiện `OrderPlaced`/`BasketCheckedOut`, và tính năng `011` đã có sẵn hợp đồng Pact cho luồng này — **nhưng vẫn chưa có RabbitMQ/MassTransit nào thực sự publish hay consume**. Khoảng cách để hoàn thành việc này (SCRUM-31) đã ngắn lại đáng kể so với trước, vì phần "định nghĩa hợp đồng" khó nhất đã xong.

**4) Redis và RabbitMQ vẫn chạy không, dù nay có thêm hạ tầng test cho chúng.** Tính năng `010` thêm được khả năng viết test thật với Redis/RabbitMQ thật trong container — nhưng bản thân service vẫn chưa gọi tới chúng ở đâu cả.

**5) Chưa có Pact Broker.** Hợp đồng Pact hiện lưu file trực tiếp trong repo (`pacts/*.json`), không có nơi lưu trữ/theo dõi tập trung như ADR-0006 đã chọn ban đầu.

**6) Chưa có xác thực/phân quyền thật, chưa có ngăn cách vật lý dữ liệu.** Không đổi — thuộc Giai đoạn 3.

**7) Vài ghi chú trạng thái trong tài liệu đặc tả bị lỗi thời (không ảnh hưởng chức năng).** Khuôn mẫu lặp lại: cả 6 `spec.md` của các tính năng vừa xong vẫn ghi "Draft" dù `tasks.md` đã hoàn thành gần như 100%.

---

## Tổng kết ngắn cho quản lý

- **Giai đoạn 2 của roadmap (Kỷ luật hợp đồng & kiểm thử) gần như hoàn tất: 5/6 hạng mục xong thật, 1 hạng mục (CI/CD) đã viết xong toàn bộ code nhưng chưa được bật lên vì cần quyền quản trị bên ngoài repo.**
- Điều quan trọng cần quản lý hiểu đúng: phần lớn công sức của Giai đoạn 2 **không làm hệ thống chạy được thêm việc gì mới** — mà là chứng minh phần đã có đủ vững chắc (retrofit TDD không tìm thấy lỗi), và chuẩn bị sẵn nền móng hợp đồng cho việc tương lai (schema sự kiện, contract test) mà chưa nối dây thật.
- Codebase = **33 dự án .NET** (build sạch) + **1 workspace frontend độc lập**, tăng từ 24 dự án do 6 tính năng mới.
- **Việc cần ưu tiên nhất lúc này không phải là code, mà là quyền quản trị**: cần ai đó có quyền trên Jenkins/SonarQube/GitHub thực hiện 3 bước bật CI/CD thật — nếu không, mọi công sức viết pipeline sẽ chỉ nằm im.
- Ba khoản nợ kỹ thuật lớn từ các bản trước vẫn còn nguyên: ngăn cách vật lý dữ liệu theo tenant, saga/outbox cho thanh toán (nay đã có bản thiết kế hợp đồng, chỉ còn thiếu nối dây), và xác thực thật.
- Việc đọc hiểu tiến độ nên dựa vào [`docs/roadmap.md`](roadmap.md) và trạng thái `[X]`/`[ ]`/`[ ] ⛔` trong từng `specs/*/tasks.md`.
