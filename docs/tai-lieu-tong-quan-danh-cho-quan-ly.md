# Tài liệu kỹ thuật tổng quan — Nền tảng Ecommerce

*Viết cho: quản lý không trực tiếp code .NET. Mục tiêu: hiểu codebase hiện tại đang có gì, các phần liên hệ với nhau ra sao, và vì sao nó được thiết kế như vậy — không cần đọc code.*

*Cập nhật lần cuối: sau khi hoàn thành tính năng `006-e2e-order-demo` (SCRUM-16) — repo giờ có một **tài liệu chính thức đóng Giai đoạn 1**, [`docs/demo-phase-1.md`](demo-phase-1.md), kèm ảnh chụp thật từ một lượt chạy demo. Đây không còn là suy luận của người viết tài liệu này nữa — có nguồn trích dẫn trực tiếp từ repo.*

---

## Điều quan trọng nhất cần biết trước khi đọc tiếp

Đây **không phải** một hệ thống thương mại điện tử đang chạy production. Theo [`docs/roadmap.md`](roadmap.md), đây là **dự án luyện tập cá nhân (solo)** để một người thực hành đầy đủ vòng đời phần mềm.

**Giai đoạn 1 (Walking Skeleton) nay có bằng chứng đóng chính thức, không phải suy luận.** Tài liệu [`docs/demo-phase-1.md`](demo-phase-1.md) — sản phẩm của tính năng `006-e2e-order-demo` — có một bảng đối chiếu từng hạng mục của Giai đoạn 1 (SCRUM-10 đến SCRUM-16) với bằng chứng cụ thể, đều đánh dấu **"Evidenced" (đã có bằng chứng)**:

| Hạng mục Giai đoạn 1 | Trạng thái | Bằng chứng |
|---|---|---|
| SCRUM-10 — Phạm vi lát cắt mỏng | Evidenced | Luồng demo đúng là lát cắt đó |
| SCRUM-11 — Dựng vỏ 4 service | Evidenced | Cả 4 service phục vụ lượt chạy này |
| SCRUM-12 — Định danh giả lập + tenant thật | Evidenced | Gắn tenant vào đơn hàng + từ chối khi thiếu tenant |
| SCRUM-13 — Gateway → BFF | Evidenced | Cả Gateway và BFF phục vụ lượt chạy |
| SCRUM-14 — Giao diện tối thiểu | Evidenced | 4 ảnh chụp màn hình |
| SCRUM-15 — Chạy toàn bộ bằng 1 lệnh | Evidenced | Khởi động sạch từ đầu trong 2 phút 48 giây |
| SCRUM-16 — Demo đặt 1 đơn hàng đầu-cuối | Evidenced | Chính tài liệu này |

Tài liệu này **cũng tự liệt kê rõ những gì KHÔNG được chứng minh** (Mục 5 bên dưới) — một cách làm minh bạch đáng ghi nhận, không phải "vẽ" bức tranh hoàn hảo.

Trong repo có **hai tầng thông tin** dễ nhầm lẫn với nhau:

| Tầng | Là gì | Đã có code chưa? |
|---|---|---|
| **Bản thiết kế mục tiêu** (`docs/system-design.md`, `docs/adr/`, `.specify/memory/constitution.md`) | Kiến trúc đầy đủ dự kiến: Identity Server thật, message queue (saga/outbox), phân vùng dữ liệu vật lý theo tenant, CI/CD | **Một phần nhỏ** |
| **Codebase thực tế hôm nay** | 6 service .NET + 1 giao diện web, dựng bằng một lệnh, **có bằng chứng chạy được đầu-cuối kèm ảnh chụp** | **Có**, và là toàn bộ những gì tài liệu này mô tả |

**Sáu tính năng đã hoàn thành theo đúng quy trình đặc tả**, mỗi tính năng gần như 100% nhiệm vụ: `001` đến `005` (xem các bản cập nhật trước), và mới nhất **`006-e2e-order-demo`** (**41/42** — mục còn lại là một thao tác thủ công của con người: đính kèm video lên Jira, không phải việc kỹ thuật còn thiếu).

---

## 1. Giải thích codebase hiện tại

### 1.1–1.7 (không đổi so với bản trước)

Kiến trúc tổng thể (24 dự án .NET, 1 workspace frontend), 6 service .NET + giao diện web, luồng mua hàng, cơ chế tenant/người gọi giả lập, và cách chạy toàn bộ bằng một lệnh — giữ nguyên như lần cập nhật trước (tính năng `005`). Xem lại các mục tương ứng nếu cần chi tiết.

**Một điều chỉnh nhỏ cần cập nhật ở Mục 1.3 (dữ liệu):** bảng `Order` giờ có thêm cột `TenantId` (kiểu chuỗi, cho phép null, thêm bằng migration mở-rộng-không-phá-vỡ — không có dữ liệu cũ nào bị ảnh hưởng). Đây **chỉ là cột ghi nhận/hiển thị** "đơn hàng này thuộc tenant nào", server tự gán từ tenant đã xác định, client không thể tự khai. Chính code còn ghi chú rõ ràng: *"Đây là bằng chứng của việc gán nhãn, không phải cơ chế cách ly."* Cơ chế thực sự ngăn truy cập chéo tenant vẫn là chốt chặn ở `Program.cs` như trước — **cột này không thay đổi gì về việc ngăn cách vật lý dữ liệu theo tenant, khoảng nợ đó vẫn còn nguyên** (xem Mục 5).

### 1.8 Dựng toàn bộ hệ thống bằng một lệnh (không đổi — xem bản cập nhật trước)

### 1.9 Chế độ "demo" — bằng chứng chạy được, có thể lặp lại (mới — tính năng `006`)

Ngoài lệnh chạy bình thường (Mục 1.8), giờ có thêm **một lệnh riêng, tách biệt hoàn toàn**, chỉ để tạo bằng chứng:

```
cp .env.example .env      # nếu chưa có
./scripts/demo.ps1         # hoặc ./scripts/demo.sh
```

Lệnh này tự động: dựng hệ thống ở "chế độ demo", xoá giỏ hàng cho sạch, điều khiển một trình duyệt thật đi qua đúng luồng mua hàng (duyệt → thêm giỏ → thanh toán → xác nhận), đọc lại đơn hàng từ service Orders để đối chiếu, kiểm tra xem cả 5 thành phần (Gateway, BFF, 4 service nghiệp vụ) có thực sự phục vụ lượt chạy hay không (bằng cách soi log theo dõi), rồi in ra một bản tóm tắt kèm việc so sánh với lần chạy trước để chứng minh **có thể lặp lại, không phải may mắn một lần**. Đo được: chạy lại trên hệ thống đã bật sẵn mất **10 giây**; dựng lại từ đầu (xoá sạch dữ liệu) mất **2 phút 48 giây**.

**"Chế độ demo" là một lớp phủ hẹp, chủ động bật, không phải mặc định.** Nó chỉ khác lệnh chạy bình thường ở đúng 2 điều: mở thêm 2 cổng nội bộ (Orders, Baskets) để lệnh demo tự gọi vào kiểm tra, và tăng mức chi tiết log của bộ thu thập theo dõi. **Không đổi bất kỳ hành vi nghiệp vụ, không tắt xác thực, không bỏ qua bước nào** — file cấu hình demo tự ghi chú: *"Thứ được trình diễn phải là hệ thống chạy như bình thường, không phải một hệ thống được cấu hình lại để trình diễn cho đẹp."* Lệnh chạy bình thường (`scripts/up.sh`) hoàn toàn không biết tới file cấu hình demo này.

**Kết quả cụ thể đã ghi lại:** giỏ hàng gồm 2 sổ tay + 1 tạp dề → thanh toán → tổng **$59.25**; gọi thẳng vào service Orders **không kèm** thông tin tenant → bị từ chối (đúng như thiết kế); gọi lại **có** tenant `contoso` → thành công, đơn hàng đọc lại khớp. Bốn ảnh chụp màn hình của lượt chạy này (`docs/demo/01-catalog.png` đến `04-basket-empty.png`) đã được lưu thẳng trong repo làm bằng chứng lâu dài; video đầy đủ **không** lưu trong repo (tránh phình dung lượng theo thời gian) mà đính kèm vào Jira SCRUM-16.

---

## 2. Các dự án/component và cách chúng phụ thuộc lẫn nhau

Không đổi so với bản trước — tính năng `006` **không thêm dự án .NET mới** (vẫn 24 dự án), chỉ mở rộng `Orders.Api` (thêm cột `TenantId`) và thêm bộ script/spec demo nằm ngoài `.slnx`. Bảy "lưới an toàn kiến trúc" tự động (Mục 2.6 bản trước) không đổi.

---

## 3. Mục đích từng phần + các tình huống thực tế được giải quyết

### 3.1 Mục đích từng dự án/thành phần (không đổi — xem bản cập nhật trước)

Bảng mục đích từng dự án giữ nguyên như bản cập nhật cho tính năng `005`; tính năng `006` không thêm dự án nào.

### 3.2 Mười hai tình huống thật mà giải pháp này giải quyết

*Mười tình huống đầu đã có từ các bản cập nhật trước, nay chép lại đầy đủ để đọc được một mạch mà không phải lần theo lịch sử tài liệu. Hai tình huống cuối là mới, đến từ tính năng `006`.*

**1) Ngăn rò rỉ/đè dữ liệu chéo giữa các bộ phận nghiệp vụ, trước khi nó xảy ra.**
Một lập trình viên copy-paste nhầm file cấu hình khiến service Baskets trỏ sang CSDL của Orders. Ở đây build **thất bại ngay**, trước khi code được merge — bắt lỗi ở khâu *sở hữu*, không đợi đến khâu *sử dụng*.

**2) Phát hiện sự cố hạ tầng tự động, không cần con người theo dõi 24/7.**
`/health/ready` thật sự thử mở kết nối tới CSDL; `/health/live` **cố tình không** kiểm tra CSDL, để một sự cố CSDL 30 giây không biến thành sự cố ứng dụng 5 phút do bị khởi động lại hàng loạt.

**3) Giữ chất lượng kiến trúc ổn định khi thời gian trôi qua, không phụ thuộc trí nhớ một người.**
Các bài test kiến trúc biến quy ước tổ chức code thành điều kiện bắt buộc để build thành công.

**4) Một service chết không kéo sập cả hệ thống.**
Không có ngân sách thời gian, mặc định .NET chờ tới 100 giây mỗi lời gọi — một service treo có thể làm BFF cạn luồng xử lý và ngừng trả lời **mọi thứ**, kể cả phần không liên quan. Với ngân sách 3 giây hiện tại, người gọi luôn nhận lỗi rõ ràng trong vài giây, các phần khác vẫn chạy bình thường.

**5) Lỗi có mã tra cứu, thay vì "hệ thống đang bận, vui lòng thử lại".**
Một mã theo dõi (`X-Correlation-Id`) lần được toàn bộ hành trình của request qua cả ba tầng trong hệ thống ghi log — không có nó, điều tra một khiếu nại cụ thể gần như bất khả thi trong hệ phân tán.

**6) Đội frontend không thể vô tình bỏ sót trường hợp lỗi.**
Tài liệu API sinh tự động từng chỉ khai báo trường hợp thành công; đã sửa để bắt buộc khai báo cả 404/502/504, và có test canh để không lệch trở lại — quan trọng vì mã nguồn giao diện dự kiến sẽ được sinh tự động từ chính tài liệu này.

**7) "Quên xác định đang phục vụ ai" biến thành lỗi ồn ào ngay lập tức, không phải rò rỉ dữ liệu âm thầm.**
Nếu một script nội bộ, một lần debug thủ công, hay một tính năng tương lai gọi thẳng vào service nghiệp vụ mà bỏ qua Gateway (nên thiếu header `X-Tenant-Id`), hệ thống **dừng cứng với lỗi 500** thay vì âm thầm trả lời bằng dữ liệu của tenant mặc định nào đó. Vì toàn hệ thống chỉ có đúng một điểm khởi tạo kết nối CSDL cho mỗi service, và điểm đó bắt buộc phải xác định tenant trước — không có đường vòng nào để tính năng mới lỡ quên bước này mà vẫn chạy được. *Từ tính năng `004`, điều này áp dụng cho cả tenant lẫn người gọi cụ thể (`X-Subject-Id`), không chỉ riêng tenant.*

**8) Thanh toán trùng lặp do bấm nhầm/mạng chậm không tạo ra hai đơn hàng.**
Một tình huống rất thật trong thương mại điện tử: khách hàng bấm "Thanh toán", mạng chậm, khách sốt ruột bấm thêm lần nữa. Vì bước đầu tiên của thanh toán luôn kiểm tra giỏ hàng có còn hàng không, và giỏ đã bị xoá ngay sau lần thanh toán thành công đầu tiên, lần bấm thứ hai tự động nhận lỗi rõ ràng (409 — "giỏ hàng trống") thay vì tạo ra đơn hàng thứ hai và tính tiền hai lần. Không cần viết thêm cơ chế "chống trùng lặp" riêng — tác dụng phụ tự nhiên của đúng thứ tự các bước.

**9) "Test đều xanh" không có nghĩa là "chạy được" — cho tới khi có ai thực sự thử đóng gói.**
Trong nhiều tháng, toàn bộ 96+ bài test của hệ thống đều báo xanh, `dotnet build` luôn sạch — nhưng **5 trên 6 Dockerfile thực ra không build được**, vì thiếu một dòng copy thư viện dùng chung. Không có bài test .NET nào từng phát hiện ra, đơn giản vì không có bài test .NET nào từng thử build container. Chỉ khi tính năng `005` thực sự cần chạy container thật, lỗi mới lộ ra — và ngay khi lộ ra, đã được biến thành một bài test tự động (`ContainerConventionTests`) để không bao giờ tái diễn âm thầm. Bài học quản lý: "test xanh" chỉ chứng minh những gì test đó *có kiểm tra*, không hơn.

**10) Một lỗi chỉ xuất hiện khi thử lại nhiều lần, không xuất hiện ở lần chạy đầu tiên — và cách duy nhất tìm ra nó là chủ động thử lại nhiều lần.**
Cách kiểm tra "CSDL đã sẵn sàng chưa" tưởng như đơn giản (hỏi server có phản hồi không) thực ra **sai trong khoảng 40% trường hợp** khi khởi động lại: server phản hồi trước khi từng database bên trong phục hồi xong, dẫn tới lỗi ngẫu nhiên "database đã tồn tại". Nếu chỉ thử một lần rồi kết luận "chạy được", lỗi này sẽ ngủ yên và một ngày nào đó xuất hiện ngẫu nhiên trong tay người dùng thật, không cách nào lặp lại để điều tra. Chỉ vì tính năng `005` chủ động yêu cầu thử "10 lần dừng-bật liên tiếp" thay vì "chạy một lần cho có", lỗi mới lộ diện đủ để sửa tận gốc.

**11) Muốn có bằng chứng "hệ thống chạy được", đừng chỉ nói — hãy tự động hoá việc chứng minh, và chứng minh nhiều lần chứ không phải một lần.**
Rất nhiều dự án phần mềm tuyên bố "đã xong giai đoạn 1" chỉ bằng lời hoặc một buổi demo trực tiếp không ai ghi lại — khi cần xác minh lại 3 tháng sau thì không còn gì để kiểm chứng. Ở đây, "bằng chứng" được biến thành một lệnh chạy lại được bất cứ lúc nào, tự so sánh với lần chạy trước để chứng minh không phải một lần trùng hợp may mắn, và để lại dấu vết cụ thể (ảnh chụp) trong chính repo — bất kỳ ai, kể cả người không kỹ thuật, đều xem lại được `docs/demo-phase-1.md` mà không cần chạy gì.

**12) Công cụ đo lường không được làm thay đổi thứ đang được đo.**
Khi cần "chế độ demo" để mở thêm cổng và tăng log phục vụ việc kiểm chứng, có một cám dỗ tự nhiên là tiện thể "dọn dẹp" luôn — tắt bớt kiểm tra, nới lỏng vài thứ để demo chạy mượt hơn. Đội phát triển chủ động tránh cám dỗ này: chế độ demo chỉ được phép khác đúng 2 điều nhỏ so với hệ thống chạy thật, và có ghi chú thẳng trong code lý do tại sao — vì nếu demo chạy trên một hệ thống đã bị "làm màu", bằng chứng thu được sẽ không còn đáng tin cho bất kỳ ai đọc lại sau này.

---

## 4. Sơ đồ kiến trúc hiện tại (dùng được với draw.io)

Sơ đồ vẽ **đúng những gì đang có trong code hôm nay**. Phần dưới (viền đứt màu đỏ) là những gì vẫn chỉ nằm trên giấy — nay được đối chiếu trực tiếp với danh sách "chưa chứng minh" chính thức trong `docs/demo-phase-1.md` (Mục 5 bên dưới), không còn là suy đoán riêng của tài liệu này.

> **Lưu ý phân biệt:** repo có sẵn 3 sơ đồ khác ở [`docs/system-design.md`](system-design.md) — nhưng chúng vẽ **kiến trúc mục tiêu đầy đủ**, không phải trạng thái hiện tại.

### Cách dùng
1. Mở [app.diagrams.net](https://app.diagrams.net) (draw.io).
2. Vào menu **Extras → Edit Diagram…**
3. Xoá nội dung trống, dán toàn bộ khối XML bên dưới vào, bấm **Save/OK**.

*(Hoặc mở trực tiếp file [`docs/diagrams/current-state-architecture.drawio`](diagrams/current-state-architecture.drawio) đã có sẵn trong repo — nội dung giống hệt khối XML bên dưới.)*

```xml
<mxfile host="Electron" agent="5.0">
  <diagram id="current-state-005" name="Trạng thái hiện tại — sau 005">
    <mxGraphModel dx="3279" dy="737" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1500" pageHeight="1820" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <mxCell id="title1" parent="1" style="text;html=1;fontStyle=1;fontSize=15;fontColor=#2d6a2d;" value="PHẦN 1 — ĐÃ CÓ TRONG CODE HÔM NAY (24 dự án .NET + 1 workspace frontend; 1 LỆNH DUY NHẤT chạy toàn bộ stack; xem docs/demo-phase-1.md để có bằng chứng Giai đoạn 1 đã xong)" vertex="1">
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
        <mxCell id="svc3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=10;" value="Orders.Api&#xa;Cổng 5041 (chỉ mở trong chế độ debug, hoặc chế độ demo)&#xa;Tạo đơn hàng từ dòng giỏ hàng; đọc lại theo id&#xa;+ /health/live, /health/ready&#xa;Bảng: Order (Id, PlacedAtUtc, Total, MỚI: TenantId — chỉ là&#xa;NHÃN, không phải ngăn cách vật lý, xem Phần 2)&#xa;Chặn CSDL nếu thiếu X-Tenant-Id / X-Subject-Id" vertex="1">
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
        <mxCell id="rabbitmq" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#999999;fontSize=10;" value="RabbitMQ&#xa;Chạy và có healthcheck, nhưng CHƯA có&#xa;service nào kết nối vào — dành cho&#xa;saga/outbox tương lai (xem Phần 2)" vertex="1">
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
        <mxCell id="title2" parent="1" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#a03030;" value="PHẦN 2 — CHƯA XÂY DỰNG, MỚI LÀ KẾ HOẠCH (Phase 2-5, xem docs/roadmap.md)" vertex="1">
          <mxGeometry height="26" width="1000" x="30" y="1440" as="geometry" />
        </mxCell>
        <mxCell id="futurebg" parent="1" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#fafafa;strokeColor=#cc6666;dashed=1;" value="" vertex="1">
          <mxGeometry height="130" width="1400" x="20" y="1475" as="geometry" />
        </mxCell>
        <mxCell id="f1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Identity Server thật&#xa;(Duende)&#xa;(SCRUM-23, Giai&#xa;đoạn 3)" vertex="1">
          <mxGeometry height="80" width="255" x="40" y="1500" as="geometry" />
        </mxCell>
        <mxCell id="f2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Ngăn cách VẬT LÝ&#xa;theo tenant (đã thử,&#xa;đã chủ động huỷ —&#xa;xem mục 1.3)" vertex="1">
          <mxGeometry height="80" width="255" x="320" y="1500" as="geometry" />
        </mxCell>
        <mxCell id="f3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Saga + Outbox thật cho&#xa;thanh toán — THỰC SỰ&#xa;dùng RabbitMQ đang&#xa;chạy rỗng (ADR-0011)" vertex="1">
          <mxGeometry height="80" width="255" x="600" y="1500" as="geometry" />
        </mxCell>
        <mxCell id="f4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Logistics + Invoices&#xa;service (sẽ dùng Redis/&#xa;RabbitMQ đang chạy rỗng&#xa;nhưng còn trống)" vertex="1">
          <mxGeometry height="80" width="255" x="880" y="1500" as="geometry" />
        </mxCell>
        <mxCell id="f5" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" value="Jenkins CI/CD,&#xa;Vault, Unleash,&#xa;Pact Broker" vertex="1">
          <mxGeometry height="80" width="240" x="1160" y="1500" as="geometry" />
        </mxCell>
        <mxCell id="fnote" parent="1" style="text;html=1;fontSize=10;fontColor=#a03030;whiteSpace=wrap;" value="Redis và RabbitMQ ĐÃ CHẠY thật trong docker-compose.yml từ tính năng 005 (hạ tầng sẵn sàng), nhưng vẫn CHƯA có service nào kết nối vào — nghiệp vụ dùng tới nó vẫn là kế hoạch.&#xa;Chạy-toàn-bộ-bằng-1-lệnh (SCRUM-15) và Web SPA (SCRUM-14) ĐÃ chuyển lên Phần 1, không còn là kế hoạch nữa — việc còn lại cuối cùng của Giai đoạn 1 (Walking Skeleton) coi như đã xong." vertex="1">
          <mxGeometry height="45" width="1360" x="35" y="1610" as="geometry" />
        </mxCell>
        <mxCell id="lgtitle" parent="1" style="text;html=1;fontStyle=1;fontSize=12;" value="Chú giải" vertex="1">
          <mxGeometry height="20" width="100" x="30" y="1670" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1">
          <mxGeometry height="20" width="20" x="30" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" parent="1" style="text;html=1;fontSize=11;" value="Service nghiệp vụ (sở hữu dữ liệu riêng)" vertex="1">
          <mxGeometry height="20" width="300" x="55" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;" vertex="1">
          <mxGeometry height="20" width="20" x="30" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" parent="1" style="text;html=1;fontSize=11;" value="Service ở biên (không sở hữu dữ liệu)" vertex="1">
          <mxGeometry height="20" width="300" x="55" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgc7" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;" vertex="1">
          <mxGeometry height="20" width="20" x="390" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc7t" parent="1" style="text;html=1;fontSize=11;" value="Giao diện web (frontend, ngoài .slnx)" vertex="1">
          <mxGeometry height="20" width="300" x="415" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1">
          <mxGeometry height="20" width="20" x="390" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" parent="1" style="text;html=1;fontSize=11;" value="Thư viện dùng chung / hạ tầng ĐÃ dùng thật (OTel)" vertex="1">
          <mxGeometry height="20" width="340" x="415" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgc8" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f0f0f0;strokeColor=#999999;" vertex="1">
          <mxGeometry height="20" width="20" x="770" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc8t" parent="1" style="text;html=1;fontSize=11;" value="Migrator (1 lần) / hạ tầng chạy nhưng CHƯA dùng (Redis, RabbitMQ)" vertex="1">
          <mxGeometry height="20" width="400" x="795" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1">
          <mxGeometry height="20" width="20" x="770" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" parent="1" style="text;html=1;fontSize=11;" value="Lưới an toàn kiến trúc (tests) — 7 loại" vertex="1">
          <mxGeometry height="20" width="330" x="795" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1">
          <mxGeometry height="20" width="20" x="1200" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" parent="1" style="text;html=1;fontSize=11;" value="Cơ sở dữ liệu" vertex="1">
          <mxGeometry height="20" width="160" x="1225" y="1700" as="geometry" />
        </mxCell>
        <mxCell id="lgc6" parent="1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;" vertex="1">
          <mxGeometry height="20" width="20" x="1200" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgc6t" parent="1" style="text;html=1;fontSize=11;" value="Chỉ là kế hoạch — chưa có code" vertex="1">
          <mxGeometry height="20" width="230" x="1225" y="1730" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" edge="1" parent="1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="30" y="1770" as="sourcePoint" />
            <mxPoint x="80" y="1770" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" parent="1" style="text;html=1;fontSize=11;" value="Gọi thật lúc chạy (HTTP / EF Core)" vertex="1">
          <mxGeometry height="20" width="260" x="90" y="1760" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" edge="1" parent="1" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="400" y="1770" as="sourcePoint" />
            <mxPoint x="450" y="1770" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" parent="1" style="text;html=1;fontSize=11;" value="Tham chiếu thư viện / phụ thuộc lúc build hoặc lúc khởi động (docker depends_on)" vertex="1">
          <mxGeometry height="20" width="480" x="460" y="1760" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

---

## 5. Rủi ro và việc còn treo — nay trích dẫn trực tiếp từ `docs/demo-phase-1.md`

Điểm khác biệt lớn nhất của lần cập nhật này: danh sách dưới đây không còn là đánh giá riêng của tài liệu quản lý này nữa — nó **trích trực tiếp** từ mục "những gì demo KHÔNG chứng minh" mà chính đội phát triển tự viết ra và công khai trong repo.

**1) Ngăn cách vật lý dữ liệu theo tenant — "khoản nợ không ai nhận."** Nguyên văn từ `docs/demo-phase-1.md`: *"Đây là khoản nợ chưa ai nhận trách nhiệm. Việc ngăn cách theo schema-per-tenant được yêu cầu bởi hiến chương dự án (Nguyên tắc V) và chưa tồn tại. Đã được ghi nhận là một khoảng trống bởi tính năng `004` và `005`, và ghi nhận lại lần nữa ở đây... Nó cần một hạng mục công việc riêng."* Cột `TenantId` mới thêm vào bảng Order (Mục 1.3) **không** giải quyết việc này — chỉ là ghi nhãn, không phải cách ly.

**2) Chưa có cơ chế sự kiện/outbox/saga.** Nguyên văn: *"Chưa có gì publish sự kiện nào cả; thanh toán vẫn là điều phối đồng bộ."* Gắn với SCRUM-18, SCRUM-31.

**3) Hợp đồng API được viết ra nhưng chưa phải "luật" của quá trình build.** Nguyên văn: *"Hợp đồng đã được viết nhưng chưa phải là thẩm quyền của build."* Gắn với SCRUM-17, SCRUM-21.

**4) Chưa có xác thực/định danh thật, chưa có hạ tầng theo dõi (telemetry) đích thực, chưa có ngân sách hiệu năng được đo đạc, và bản thân việc "chạy demo được" chưa được biến thành một cổng kiểm soát chất lượng tự động (quality gate). Cũng chưa triển khai lên Kubernetes.** Đây đều là các hạng mục demo tự liệt kê rõ là "cố tình không chứng minh", không phải bị bỏ sót do quên.

**5) Chưa có CI/CD.** Không đổi từ các bản trước — mọi kiểm tra (kể cả lệnh demo) vẫn chạy thủ công.

**6) Vài ghi chú trạng thái trong tài liệu đặc tả bị lỗi thời (không ảnh hưởng chức năng).** Một khuôn mẫu lặp lại: `spec.md` của tính năng `006` (và các tính năng trước) vẫn ghi dòng trạng thái đầu file là "Draft" dù `tasks.md` xác nhận gần như hoàn thành 100%.

---

## Tổng kết ngắn cho quản lý

- **Giai đoạn 1 (Walking Skeleton) giờ có bằng chứng đóng chính thức, có thể xem lại bất cứ lúc nào** — không còn là lời khẳng định suông. `docs/demo-phase-1.md` đối chiếu từng hạng mục SCRUM-10 đến SCRUM-16 với bằng chứng cụ thể (ảnh chụp, số liệu đo được, log thật), và tự công khai luôn cả những gì **chưa** chứng minh được — một cách làm minh bạch nên ghi nhận.
- Sáu tính năng đã hoàn thành đúng quy trình đặc tả: dựng vỏ service, nối Gateway/BFF, định danh + tenant giả lập, giao diện mua hàng đầu-cuối, chạy toàn bộ bằng một lệnh, và giờ là bằng chứng demo đầu-cuối chính thức.
- Codebase vẫn = **24 dự án .NET** (build sạch) + **1 workspace frontend độc lập**. Tính năng `006` không thêm dự án mới, chỉ thêm 1 cột ghi nhãn tenant vào bảng Order và một bộ script demo tách biệt, an toàn (không đổi hành vi hệ thống mặc định).
- **Ba khoản nợ kỹ thuật lớn nhất vẫn còn nguyên, và nay được chính đội phát triển gọi thẳng là "chưa ai nhận trách nhiệm"**: ngăn cách vật lý dữ liệu theo tenant, cơ chế saga/outbox cho thanh toán, và hợp đồng API chưa là điều kiện bắt buộc của build. Đây là các mục nên đưa vào thảo luận ưu tiên cho Giai đoạn 2.
- Việc đọc hiểu tiến độ nên dựa trực tiếp vào [`docs/demo-phase-1.md`](demo-phase-1.md) (bằng chứng cụ thể nhất hiện có) và [`docs/roadmap.md`](roadmap.md), hơn là dựa vào độ dày tài liệu thiết kế trong `docs/`.
