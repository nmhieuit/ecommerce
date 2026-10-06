# 17 — Gợi ý theo triệu chứng (lỗi bất ngờ)

*(Cần đã làm file [09](09-dich-ket-noi-sai.md) đến [16](16-container-chet-hoac-khoi-dong-lai.md): các file đó dạy từng nhóm lỗi khi bạn **biết trước** nhóm nào đang chạy. File này dành cho dạng
**bất ngờ**: bạn chạy `-Start`, chỉ có `runId` và mã băm, rồi phải tự tìm ra lỗi.)*

> **Đang làm bài mù thì đừng mở file 09–16.** Chúng ghi sẵn nhóm lỗi và đáp án ở tiêu đề. Chỉ mở chúng sau khi đã đọc mức 3 ở đây, hoặc sau khi đã `-Reveal`.

## Cách dùng file này

1. Nhìn dashboard `Xử lý sự cố — 7 service` và tìm **triệu chứng khớp nhất** trong các mục bên dưới. Mục nào cũng gồm đúng ba mức mở dần: mức 1 mô tả triệu chứng và cách kiểm, mức 2 nêu nhóm lỗi, mức 3 mới nêu loại lỗi và cách khôi phục.
2. Đọc **từng mức một**, theo thứ tự, và dừng ngay khi bạn đã tự tìm ra. Đọc mức 3 trước khi mức 1 và 2 làm mất tác dụng của bài mù.
3. Ba mức này cùng nghĩa với `./scripts/incident-drill.ps1 -Hint -RunId <runId> -Level 1|2|3` (1 triệu chứng, 2 nhóm lỗi, 3 đáp án), nhưng mỗi mức ở đây viết thêm **chỉ dẫn cách kiểm** (panel hoặc truy vấn) mà `-Hint` không có.
   Lệnh `-Hint` được ghi lại mỗi lần mở; **đọc file này thì không**, nên hãy tự ghi số mức bạn đã đọc vào bản ghi sự cố
   ([`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md)).
4. Triệu chứng nào cũng có thể do nhiều nguyên nhân; xem mục "Bẫy và nhiễu thường gặp" ở cuối trước khi kết luận.
5. Quy trình triage (SEV, Case, mốc thời gian) và truy vấn xác nhận khôi phục 15 phút: xem
   [`README` diễn tập](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).

## Triệu chứng 1 — 5xx ở một service phía sau, lan lên các service gọi nó

**Mức 1 — Gợi ý triệu chứng**: Bảng SLO có một service với tỷ lệ 5xx rất cao (khoảng một nửa số request), và hai service đứng **trước** nó trong chuỗi gọi (BFF và gateway) cũng 5xx (khoảng 25–32%); mọi service còn lại sạch.
Trên container của service lỗi, `/health/ready` trả 503 và container không lên `healthy`. Cách kiểm: mở panel "Lỗi gọi hạ lưu" và tìm dòng **cuối chuỗi** gọi; chạy truy vấn Q1 (5xx theo service) và Q2 (route nào, mã nào) ở mục [Truy vấn dùng chung](#truy-vấn-dùng-chung);
đọc log của service lỗi bằng Q8 (lọc theo service, vì log của dịch vụ định danh sẽ lấp panel).

**Mức 2 — Nhóm lỗi**: Nhóm 1 (đích kết nối sai) hoặc nhóm 4 (phụ thuộc hạ tầng dừng): service lỗi không kết nối được tới cơ sở dữ liệu của nó. Hai nhóm cho log gần như giống nhau; phân biệt bằng cách so cấu hình kết nối của service với trạng thái của container cơ sở dữ liệu (`docker ps -a`).

**Mức 3 — Đáp án (Loại A)**: Địa chỉ cơ sở dữ liệu trong cấu hình của service bị đổi sang một host không tồn tại (`Server=incident-missing-db` khi chạy lệnh `docker inspect` lấy `Server=`); container cơ sở dữ liệu **vẫn `Up`**.
Lần đo: service lỗi 53,3% 5xx, BFF 24,6%, gateway 32,3%. Khôi phục: `-Restore -RunId <runId>` (khoảng 51 giây). Chi tiết: [09 — Đích kết nối sai](09-dich-ket-noi-sai.md).

**Mức 3 — Đáp án (Loại E)**: Container cơ sở dữ liệu của service lỗi bị dừng: `docker ps -a` báo `Exited (137)`, cấu hình kết nối của service vẫn đúng. Lần đo: service lỗi 51,6% 5xx, container service chuyển `unhealthy` sau vài phút, không có nhiễu khởi động nguội.
Khôi phục: `-Restore -RunId <runId>` (khoảng 48 giây), service tự về `healthy` không cần khởi động lại. Chi tiết: [12 — Hạ tầng dừng](12-ha-tang-dung.md).

## Triệu chứng 2 — 5xx một phần, ổn định, không lan và không có log lỗi

**Mức 1 — Gợi ý triệu chứng**: Một service có tỷ lệ 5xx khoảng một phần tư số request, ổn định từng phút; độ trễ vẫn thấp, `/health/ready` vẫn 200, **không có dòng log lỗi nào**, và không service nào khác (kể cả phía trước) bị 5xx.
Cách kiểm: với **cùng một route**, tách span theo mã trạng thái, `attributes.server.address` và `attributes.user_agent.original`, rồi so request lỗi với request thành công (truy vấn Q6 ở mục
[Truy vấn dùng chung](#truy-vấn-dùng-chung)); dùng Q1 để chắc 5xx không lan sang service khác.

**Mức 2 — Nhóm lỗi**: Nhóm 2 (nghẽn và lỗi theo tỷ lệ): lỗi xuất hiện theo một tỷ lệ cố định ở những request có một đặc điểm riêng, không phải do hạ tầng hay cấu hình của service.

**Mức 3 — Đáp án (Loại C)**: Một tiến trình của script liên tục gửi trực tiếp tới cổng của service request mang header `X-Chaos-Fault`; service trả 500 cho đúng các request đó. Dấu hiệu: span 500 có `server.address = localhost` và user agent PowerShell, span 200 có `server.address` là tên service.
Lần đo: 23,1–25,2% mỗi phút, 0 dòng log lỗi, 5xx không lan. Khôi phục: `-Restore -RunId <runId>` xong gần như tức thì (ngừng gửi). Chi tiết: [10 — Nghẽn và lỗi theo tỷ lệ](10-nghen-va-loi-theo-ty-le.md).

## Triệu chứng 3 — Chậm khoảng 2 giây nhưng không có lỗi

**Mức 1 — Gợi ý triệu chứng**: p95 và p99 của **một** service vọt lên khoảng 2 giây và nằm phẳng ở đó, trong khi 5xx là 0%, `/health/ready` vẫn 200 và không có log lỗi. Các service khác, kể cả BFF và gateway phía trước, **không** chậm theo.
Cách kiểm: truy vấn span chậm so với span thường của service đó (ngưỡng 1,5 giây), tách theo mã, route và địa chỉ gọi (Q4 ở mục [Truy vấn dùng chung](#truy-vấn-dùng-chung));
dùng Q5 để thấy p95 từng phút và Q1 để so với các service khác.

**Mức 2 — Nhóm lỗi**: Nhóm 3 (độ trễ): một độ trễ cố định được tiêm vào một phần request của một service.

**Mức 3 — Đáp án (Loại D)**: Chỉ áp dụng cho service đơn hàng. Script gửi **thẳng** vào service đó request có header `X-Chaos-Latency-Ms: 2000` (kèm token và `X-Tenant-Id`); các span chậm trả 404 sau khoảng 2 giây. Header không đi qua gateway/BFF nên chúng không chậm.
Lần đo: p95 từng phút 2009–2437 ms, 0% 5xx, 311/554 span chậm. Khôi phục: `-Restore -RunId <runId>` xong sau khoảng 1,5 giây. Chi tiết: [11 — Độ trễ Orders](11-do-tre-orders.md).

## Triệu chứng 4 — 401 ở một service, sức khoẻ vẫn xanh, chậm hàng chục giây

**Mức 1 — Gợi ý triệu chứng**: Một service trả **401** cho request mang token hợp lệ, trong khi các service khác nhận cùng token vẫn trả 200. Container của nó vẫn `healthy` và `/health/ready` vẫn 200, nhưng p95 lên hàng chục giây và BFF trả 502 cho các route đi qua nó.
Cách kiểm: truy vấn Q1 có cột đếm 401 theo service (chỉ một service nổi lên); đọc log **mức Warning trở lên** của service đó bằng Q8 (`severity_number >= 13`, vì mức Error rất ít); xem mục
[Truy vấn dùng chung](#truy-vấn-dùng-chung).

**Mức 2 — Nhóm lỗi**: Nhóm 5 (xác thực hỏng): service không tải được cấu hình của máy chủ định danh nên không kiểm tra được token.

**Mức 3 — Đáp án (Loại F)**: Biến `Identity__Authority` của service được đổi sang `http://incident-missing-host:8080`; service gọi `/.well-known/openid-configuration` của địa chỉ sai và thử lại nhiều lần trước khi từ chối.
Lần đo: 110/159 span 401, p95 10581 ms, container `healthy`. Khôi phục: `-Restore -RunId <runId>` (khoảng 57 giây). Chờ 2–3 phút sau khi tạo lại rồi mới kết luận, vì "token hỏng sau khi tạo lại identity" (mục Bẫy) cũng ra 401/502 trong 1–2 phút đầu. Chi tiết: [13 — Xác thực hỏng](13-xac-thuc-hong.md).

## Triệu chứng 5 — Chậm nhẹ gấp nhiều lần, chưa vượt SLO, không có lỗi

**Mức 1 — Gợi ý triệu chứng**: Một service chậm gấp khoảng 10 lần các service cùng loại (p95 chừng 100 ms so với 8–25 ms), nhưng **dưới** ngưỡng 150 ms; 0% 5xx, không có log lỗi, container `healthy`, thường không có cảnh báo nào bắn.
Cách kiểm: so p50/p95/p99 của các service trong cùng một khoảng thời gian (Q1 ở mục [Truy vấn dùng chung](#truy-vấn-dùng-chung)); dùng `docker stats` so cột bộ nhớ của service nghi vấn với service khác.

**Mức 2 — Nhóm lỗi**: Nhóm 6 (thiếu tài nguyên): container của một service bị giới hạn CPU và bộ nhớ.

**Mức 3 — Đáp án (Loại G)**: Container bị đặt `--cpus 0.1` và `--memory 256m` (`docker inspect` báo `NanoCpus=100000000`, `Memory=268435456`); `docker stats` hiện giới hạn 256 MiB thay vì toàn bộ RAM.
Lần đo: p95 244 → 200 → 100 → 89 → 81 ms, 0% 5xx, không OOM. Mức bộ nhớ thấp hơn (96 MB) làm container bị OOM-kill nên không dùng. Khôi phục: `-Restore -RunId <runId>` (khoảng 9 giây). Chi tiết: [14 — Thiếu tài nguyên](14-thieu-tai-nguyen.md).

## Triệu chứng 6 — Một service biến mất khỏi dữ liệu, phía gọi timeout 504

**Mức 1 — Gợi ý triệu chứng**: Một service **không còn span nào**: dòng của nó biến khỏi bảng SLO và đường Request/phút về 0 (không hiện đỏ). Service gọi nó báo span Client lỗi **không có mã HTTP**, `TaskCanceledException` sau khoảng 1 giây, rồi trả 504 cho người dùng; gateway cũng 504.
Cách kiểm: truy vấn Q7 ("service nào có span trong 4 phút gần nhất") và tìm service **thiếu**; truy vấn Q3 (span Client lỗi của service gọi); rồi đối chiếu `docker ps -a` với `docker inspect` mạng của container nghi vấn
(các truy vấn ở mục [Truy vấn dùng chung](#truy-vấn-dùng-chung)).

**Mức 2 — Nhóm lỗi**: Nhóm 7 (mạng đứt) hoặc nhóm 8 (container chết hoặc khởi động lại): service không còn tới được. Phân biệt bằng trạng thái container: còn chạy mà không có mạng, hay đã dừng hẳn.

**Mức 3 — Đáp án (Loại H)**: Container bị tách khỏi mạng chung: `docker inspect` báo `Networks={}`, `docker ps` vẫn `Up` lúc đầu rồi `unhealthy` sau vài phút, cổng publish ra host không kết nối được.
Lần đo: 165 span Client lỗi, p50 1000 ms, gateway 504 ×54. Khôi phục: `-Restore -RunId <runId>` nối lại mạng kèm bí danh gốc (khoảng 48 giây). Chi tiết: [15 — Mạng đứt](15-mang-dut.md).

**Mức 3 — Đáp án (Loại I)**: Container bị `docker kill`: `docker ps -a` báo `Exited (137)`, `running=false`, `OOMKilled=false`, `restartPolicy=no` nên không tự sống lại.
Lần đo: 101 span Client lỗi, p50 1001 ms, `/bff/checkout` 504 ×100. Khôi phục: `-Restore -RunId <runId>` tạo lại container đích (khoảng 9 giây). Chi tiết: [16 — Container chết hoặc khởi động lại](16-container-chet-hoac-khoi-dong-lai.md).

## Triệu chứng 7 — Không thấy gì bất thường

**Mức 1 — Gợi ý triệu chứng**: Mọi service có 0% 5xx và p95 như lúc nền, không cảnh báo nào, kể cả khi bắn nhiều request cùng lúc qua gateway; nhưng bạn biết một lần chạy mù đã được bốc. Cách kiểm: so p95 khi bắn 100 request song song (lệnh Q9 ở mục [Truy vấn dùng chung](#truy-vấn-dùng-chung)) với lúc nền; đọc biến môi trường của từng container (`docker inspect`) tìm giá trị lạ liên quan tới giới hạn kết nối đồng thời.

**Mức 2 — Nhóm lỗi**: Nhóm 2 (nghẽn và lỗi theo tỷ lệ): một giới hạn kết nối đồng thời quá thấp ở tầng vào hệ thống; trong hệ thống này nó **không** gây triệu chứng đo được.

**Mức 3 — Đáp án (Loại B)**: Cấu hình gateway có `ReverseProxy__Clusters__bff-cluster__HttpClient__MaxConnectionsPerServer=1` (giới hạn một kết nối tới BFF). Lần đo: bắn 100 request song song ba vòng cho p95 989 ms khi tiêm so với 889 ms khi đã gỡ, 0 lỗi 5xx — không phân biệt được.
Khôi phục: `-Restore -RunId <runId>` (khoảng 26–31 giây). Chi tiết: [10 — Nghẽn và lỗi theo tỷ lệ](10-nghen-va-loi-theo-ty-le.md).

## Truy vấn dùng chung

Các truy vấn mà phần "Cách kiểm" ở mức 1 nhắc tới, viết ở đây để bạn không phải mở các file nhóm (09–16). Chạy trong Discover ở chế độ ES|QL hoặc gửi tới `http://localhost:9200/_query?format=txt`.
Thay `Orders.Api` bằng service nghi vấn ở Q4, Q5, Q6, Q8.

**Q1 — 5xx, 401 và độ trễ theo service trong 5 phút gần nhất**:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND kind == "Server"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0), is_401 = CASE(attributes.http.response.status_code == 401, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), n401 = SUM(is_401), p50_ms = ROUND(PERCENTILE(duration, 50) / 1000000.0), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0), p99_ms = ROUND(PERCENTILE(duration, 99) / 1000000.0) BY service = resource.attributes.service.name
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 1)
| SORT service
```

**Q2 — Route nào, mã nào lỗi**:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND kind == "Server" AND attributes.http.response.status_code >= 400
| STATS n = COUNT(*) BY service = resource.attributes.service.name, route = attributes.http.route, code = attributes.http.response.status_code
| SORT n DESC
| LIMIT 12
```

**Q3 — Ai gọi ai và gọi hỏng thế nào** (span Client lỗi, kể cả không có mã HTTP):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND kind == "Client" AND (status.code == "Error" OR attributes.http.response.status_code >= 500)
| STATS n = COUNT(*), p50_ms = ROUND(PERCENTILE(duration, 50) / 1000000.0) BY caller = resource.attributes.service.name, target = attributes.server.address, code = attributes.http.response.status_code, err = attributes.error.type
| SORT n DESC
| LIMIT 10
```

**Q4 — Span chậm so với span thường của một service** (ngưỡng 1,5 giây):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND resource.attributes.service.name == "Orders.Api" AND kind == "Server"
| EVAL slow = CASE(duration > 1500000000, "chậm >1,5 s", "bình thường")
| STATS n = COUNT(*), p50_ms = ROUND(PERCENTILE(duration, 50) / 1000000.0), max_ms = ROUND(MAX(duration) / 1000000.0) BY slow, code = attributes.http.response.status_code, route = attributes.http.route, caller_host = attributes.server.address
| SORT n DESC
| LIMIT 10
```

**Q5 — p95 và 5xx từng phút của một service** (truy vấn "Xác nhận khôi phục 15 phút" của [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút), thêm ngưỡng thì đọc ở đó):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 12 minutes AND resource.attributes.service.name == "Orders.Api"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), p95_ns = PERCENTILE(duration, 95), p99_ns = PERCENTILE(duration, 99) BY minute = BUCKET(@timestamp, 1 minute)
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 2), p95_ms = ROUND(p95_ns / 1000000.0), p99_ms = ROUND(p99_ns / 1000000.0)
| KEEP minute, total, err_pct, p95_ms, p99_ms
| SORT minute
```

**Q6 — Request lỗi khác request thường ở điểm nào** (cùng service, tách theo mã, địa chỉ và user agent):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 6 minutes AND resource.attributes.service.name == "Orders.Api" AND kind == "Server"
| STATS n = COUNT(*) BY code = attributes.http.response.status_code, route = attributes.http.route, caller_host = attributes.server.address, ua = attributes.user_agent.original
| SORT n DESC
| LIMIT 10
```

**Q7 — Service nào có span trong 4 phút gần nhất** (service bị sập hoặc mất mạng là service **thiếu** trong kết quả):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes AND kind == "Server"
| STATS total = COUNT(*) BY service = resource.attributes.service.name
| SORT service
```

**Q8 — Log mức Warning trở lên của một service**:

```esql
FROM logs-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND resource.attributes.service.name == "Orders.Api" AND severity_number >= 13
| STATS n = COUNT(*) BY severity_text
```

**Q9 — Bắn 100 request song song qua gateway, ba vòng** (cần file token do `-Load` sinh ra; in phân bố mã và p50/p95/max):

```powershell
$token = ((Get-Content .incident-drill/load/environment-with-token.json -Raw | ConvertFrom-Json).values | Where-Object key -eq 'accessToken').value
$n = 100
$out = 1..3 | ForEach-Object { curl.exe -s -Z --parallel-max $n -o NUL -w "%{http_code} %{time_total}\n" -H "Authorization: Bearer $token" "http://localhost:5300/bff/products?n=[1-$n]" } | Where-Object { $_ }
$out | ForEach-Object { $_.Split(' ')[0] } | Group-Object | Select-Object Name, Count
$ms = $out | ForEach-Object { [double]($_.Split(' ')[1]) * 1000 } | Sort-Object
"p50={0:N0} p95={1:N0} max={2:N0} ms" -f $ms[[int]($ms.Count*0.5)], $ms[[int]($ms.Count*0.95)], $ms[-1]
```

## Bẫy và nhiễu thường gặp

Các tình huống thật đã gặp trong lúc diễn tập, có thể khiến bạn kết luận sai:

- **Lỗi lan theo chuỗi gọi**: 5xx ở gateway/BFF thường chỉ là hệ quả của service phía sau; tìm dòng cuối chuỗi ở "Lỗi gọi hạ lưu" trước khi nghi gateway hay BFF (QA_Debt mục 002, 020; số đo xem ở file 09 sau khi xong bài).
- **Token hỏng sau khi tạo lại identity**: khi cả 7 container được tạo lại, service vừa tạo lại từ chối token cũ của tải nền (401) và BFF trả 502/504 trong 1–2 phút tới khi tải nền lấy token mới; trông giống lỗi xác thực nhưng tự hết (QA_Debt mục 028, 031).
- **Nhiễu khởi động nguội 5–7 phút**: sau khi tạo lại container, mọi service chậm một lúc và có thể bắn cảnh báo dù không bị lỗi; lần đo ở file 09 báo cả 7 service `active` (QA_Debt mục 028).
- **Môi trường chậm từng đợt**: có lúc service không bị tiêm có p95 320–560 ms không rõ nguyên nhân, và rule bắn lại sau hơn 15 phút (QA_Debt mục 028, technical-debt mục 028).
- **Circuit breaker không mở**: lỗi hạ lưu với request đến tuần tự không làm breaker mở mạch (cần đủ lưu lượng); đừng chờ breaker mở làm tín hiệu (QA_Debt mục 020, 025; technical-debt mục 025).
- **Log lỗi bị lấp**: panel "Log lỗi gần nhất" bị dịch vụ định danh lấp bằng dòng `Error unprotecting the IdentityServer signing key` sau mỗi lần tạo lại container; luôn lọc theo service (QA_Debt mục 032).
- **Dòng 100% giả**: bảng "Lỗi gọi hạ lưu" luôn có dòng BFF → Parties toàn 404 do tải nền; đó là nền, không phải lỗi (QA_Debt mục 032).
- **Cảnh báo kéo dài do tiêm nối tiếp**: nếu hai lần chạy cách nhau chưa tới 15–20 phút sạch thì alert của lần trước còn `active`, không đo được thời gian báo của lần sau (QA_Debt mục 032).
