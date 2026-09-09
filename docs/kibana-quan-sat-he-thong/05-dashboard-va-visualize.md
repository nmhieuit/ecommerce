# 05 — Dashboard & Visualize (Lens)

*(Cần đã làm file [00](00-tong-quan-lo-trinh.md) đến [04](04-bao-mat-qua-du-lieu-quan-sat.md) — file
này dùng lại đúng field/tình huống đã học, không giải thích lại từ đầu.)*

Discover (file 01-04) trả lời **1 câu hỏi tại 1 thời điểm** — mỗi lần cần tra cứu lại phải gõ lại query.
Dashboard gộp nhiều câu hỏi đã biết cách trả lời thành **1 màn hình xem liên tục** — mở lên là thấy
ngay, không phải hỏi lại từ đầu mỗi lần. File này dựng 1 dashboard thật, gồm 4 panel, mỗi panel trả
lời đúng 1 trong 4 tình huống đã điều tra thủ công ở file 02.

## Đã kiểm tra trước — không có Maps

`Maps` cần dữ liệu toạ độ địa lý (`geo_point`). Đã tự kiểm tra bằng cách vào thẳng app **Maps** → **Add
layer** → **Documents** → chọn Data View **Traces**: Kibana tự báo lỗi ngay trong UI —
**"Data view does not contain any geospatial fields"**. Kiểm tra qua API xác nhận thêm: cả 3 index
đều có 0 document mang dữ liệu geo thật (dù `logs`/`traces` có khai báo sẵn 1 field `geo_point` trong
mapping, chưa từng được ghi). Hệ thống backend nội bộ này không có nguồn dữ liệu địa lý nào — file này
không có phần Maps.

## Kibana của bạn hiện đang trống — xác nhận qua API

```
GET /api/saved_objects/_find?type=dashboard&type=visualization&type=map&type=lens
→ "total": 0
```

Bạn đang bắt đầu từ con số 0, không có gì viết đè.

## Bước 1 — Tạo dashboard mới

1. Menu ☰ → **Dashboards**
2. Bấm **Create a dashboard**

**Bạn sẽ thấy**: màn hình trống với dòng chữ *"This dashboard is empty. Let's fill it up!"*, 2 nút
**Create visualization** và **Add from library**.

## Bước 2 — Panel 1: xu hướng `dotnet.exceptions` theo thời gian, tách theo service (Line chart)

*(Đã tự dựng và verify trực tiếp panel này trên Kibana của bạn — mọi field/nhãn dưới đây là thật.)*

**Vì sao chọn Line chart**: đúng mô tả chính Kibana ghi trong danh sách chọn kiểu biểu đồ — *"Reveal
variations in data over time"* — khớp chính xác mục đích "xu hướng", không dùng Bar (so sánh nhóm rời
rạc) hay Pie (tỉ lệ 1 thời điểm).

1. Bấm **Create visualization** → Lens mở ra
2. **Data view** (góc trên trái) → đổi từ mặc định sang **Metrics**
3. Đổi chart type (dropdown "Bar" góc trên phải khung Lens) → **Line**
4. Bấm ô **Vertical axis** → chọn field `metrics.dotnet.exceptions` → mặc định Kibana tự chọn hàm
   **Minimum**

   **Đừng dừng ở Minimum** — đổi sang hàm **Counter rate**. Nếu bạn thử bấm Counter rate ngay lúc này,
   Kibana sẽ báo (đã thấy thật): *"Counter rate requires a date histogram to work."* — nghĩa là phải
   cấu hình trục ngang trước.
5. Bấm ô **Horizontal axis** → chọn **Date histogram** → field `@timestamp`
6. Quay lại **Vertical axis**, giờ bấm được **Counter rate** — nhãn trục tự đổi thành
   *"Counter rate of metrics.dotnet.exceptions per second"*

   **Vì sao không dùng Sum/Average cho field này**: `dotnet.exceptions` là Counter tích luỹ (đã học ở
   câu hỏi trước) — cộng dồn các giá trị tích luỹ lại với nhau (Sum) hay lấy trung bình (Average) đều
   cho ra con số vô nghĩa. **Counter rate** tính đúng *tốc độ tăng* giữa 2 lần đo — đúng thứ "xu hướng"
   cần thể hiện.
7. Bấm ô **Breakdown** → **Top values** → field `resource.attributes.service.name`

   **Vì sao bắt buộc phải có bước này**: đã tự verify — bỏ qua Breakdown, đường biểu diễn trộn lẫn
   Counter rate của cả 7 service làm 1, ra hình răng cưa vô nghĩa (lên xuống liên tục dù không phản ánh
   đúng service nào). Tách theo service mới cho ra 7 đường mượt, đọc được đúng service nào đang tăng.
8. Bấm **Save and return** (góc trên phải)

## Bước 3 — Panel 2: phân bố status code theo service (Bar chart, stacked)

*(Cũng đã tự dựng và verify trực tiếp.)*

**Vì sao chọn Bar/Stacked**: so sánh 1 tổng số (request) chia theo 2 chiều cùng lúc — theo service (so
sánh nhóm) VÀ theo status code (tỉ lệ thành phần bên trong từng nhóm) — đúng mô tả *"Compare categories
or groups of data with bars"*, không dùng Line (không có ý nghĩa "theo thời gian" ở đây).

1. **Add panel** (thanh trên) → **Visualization**
2. **Data view** → **Traces**, chart type giữ **Bar** (mặc định đã là "Stacked")
3. **Horizontal axis** → **Top values** → `resource.attributes.service.name`
4. **Vertical axis** → hàm **Count** (đếm số document/span — không cần chọn field cụ thể)
5. **Breakdown** → **Top values** → `attributes.http.response.status_code`

**Lưu ý thật đã gặp**: mặc định "Last 15 minutes" có thể chỉ ra 1 màu (toàn `200`) nếu gần đây không có
request lỗi nào — đừng tưởng breakdown bị lỗi. Đổi time picker sang "Last 24 hours" để thấy đủ màu nếu
bạn đã tạo 401/403 thật ở file 04.

6. **Save and return**

## Bước 4 — Panel 3: top endpoint chậm nhất (Table)

**Vì sao chọn Table thay vì Bar**: cần đọc **con số chính xác** của nhiều endpoint cùng lúc để so sánh
(vd endpoint A trung bình 45ms, endpoint B 230ms) — đúng mô tả *"Organize data in structured rows and
columns"*. Bar chart chỉ giúp so sánh "cao thấp" trực quan, không đọc được số chính xác nhanh bằng bảng.

1. **Add panel** → **Visualization**, Data view **Traces**, đổi chart type → **Table**
2. Cấu hình đúng logic đã dùng ở Panel 1/2 — tự quan sát tên vùng thả field trên màn hình bạn (Table có
   vùng nhóm theo hàng và vùng giá trị số, tương đương "Horizontal axis"/"Vertical axis" nhưng đổi tên
   theo ngữ cảnh bảng):
   - Nhóm theo `attributes.http.route` (Top values) — mỗi hàng là 1 endpoint
   - Giá trị: hàm **Average** trên field `duration`
   - Tìm tuỳ chọn sắp xếp/rank theo giá trị vừa thêm, chọn giảm dần — endpoint chậm nhất lên đầu bảng

**Tự kiểm tra**: endpoint đứng đầu bảng có khớp với endpoint bạn đã tự tìm bằng tay ở file 02 Tình
huống 1 (sort `duration` giảm dần trong Discover) không — nếu tăng số hàng hiển thị (Top values →
Number of values), phải khớp cùng thứ tự.

3. **Save and return**

## Bước 5 — Panel 4: đếm nhanh tổng 401 + 403 (Metric)

**Vì sao chọn Metric**: cần 1 con số thật lớn, nhìn thấy ngay khi mở dashboard, không cần đọc biểu đồ —
đúng mô tả *"Present individual key metrics or KPIs"*. Đây là panel đóng vai trò "cảnh báo nhanh" —
nhìn 1 giây biết ngay có vấn đề hay không, chưa cần điều tra sâu.

1. **Add panel** → **Visualization**, Data view **Traces**, đổi chart type → **Metric**
2. Giá trị: hàm **Count**
3. Ở thanh KQL phía trên (áp dụng cho toàn panel hoặc cả dashboard, tự kiểm tra vùng nào bạn đang gõ),
   gõ:
   ```
   attributes.http.response.status_code >= 401 and attributes.http.response.status_code <= 403
   ```
4. **Save and return**

## Bước 6 — Lưu cả dashboard

Bấm **Save** (góc trên phải, không phải "Save and return" — đó là lưu từng panel). Đặt tên gợi nhớ, vd
`Tổng quan bảo trì — 7 service`.

## Bài tập tự làm

1. Thêm 1 filter áp dụng cho **cả dashboard** (không phải 1 panel riêng) lọc theo
   `attributes.TenantId : "contoso"` (thanh KQL trên cùng, ngoài mọi panel) — quan sát cả 4 panel có
   cùng lọc lại không, hay chỉ panel nào dùng field đó mới đổi.
2. Thêm 1 panel Table mới liệt kê `attributes.exception.type` theo `resource.attributes.service.name`
   (Data view **Logs**) — nối tiếp đúng Tình huống 2 ở file 02, giờ xem dưới dạng tổng hợp thay vì tra
   từng document.
3. Đổi Panel 1 từ Line sang Area, so sánh: kiểu nào đọc xu hướng của 7 service chồng lên nhau dễ hơn —
   tự đánh giá, không có đáp án đúng/sai tuyệt đối, chỉ có phù hợp/không phù hợp với mắt bạn.
