# Quan sát hệ thống với Kibana — Tổng quan & Lộ trình học

Bộ 17 file (01–17) trong thư mục này dạy bạn thao tác thật với Kibana/Elasticsearch trên chính dữ liệu
telemetry mà nền tảng ecommerce này đang tự sinh ra — không phải ví dụ Kibana chung chung tải từ đâu đó.
Mọi lệnh, URL, tên field trong các file đều đã được xác minh trực tiếp trên hệ thống đang chạy tại thời
điểm viết tài liệu (không suy đoán từ tài liệu Elastic). Chia làm ba chặng: file 01–05 học đọc dữ liệu
và dựng dashboard, file 06–08 dùng dashboard và cảnh báo SLO để phát hiện sự cố, file 09–17 luyện
troubleshoot trên các nhóm lỗi tiêm có chủ đích hoặc bất ngờ.

## Bạn là ai để đọc bộ này

Bạn chưa có kinh nghiệm với Kibana, đã dùng Postman gọi API và hiện đã thấy log xuất hiện trong Kibana
— tức là phần hạ tầng (`docker-compose.local.yml`) đã chạy đúng, việc còn lại là học cách *đọc* dữ liệu
đó.

## Lộ trình đọc — đi đúng thứ tự, mỗi file cần cái trước

1. **[01-lam-quen-kibana-discover.md](01-lam-quen-kibana-discover.md)** — mở Kibana lần đầu, tạo Data
   View, dùng Discover để lọc dữ liệu bằng tay. Không cần biết Elasticsearch là gì trước.
2. **[02-elasticsearch-rest-api.md](02-elasticsearch-rest-api.md)** — bỏ qua giao diện Kibana, gọi thẳng
   REST API của Elasticsearch bằng `curl`, hiểu cấu trúc document/index/Query DSL đằng sau những gì
   Discover vừa hiển thị ở file 01.
3. **[03-observability-traces-metrics-logs.md](03-observability-traces-metrics-logs.md)** — nối traces,
   metrics, logs lại với nhau qua `correlation.id`/`trace_id`, dùng app Observability của Kibana thay vì
   Discover thô. Cần đã làm quen Discover ở file 01 và biết đọc document ở file 02.
4. **[04-bao-mat-qua-du-lieu-quan-sat.md](04-bao-mat-qua-du-lieu-quan-sat.md)** — dùng đúng traces/logs đã
   học để soi sự kiện liên quan bảo mật (401/403 thật, vi phạm cô lập tenant). Cần trọn vẹn 3 file trước.
5. **[05-dashboard-va-visualize.md](05-dashboard-va-visualize.md)** — gộp 4 tình huống điều tra thủ công
   ở file 02 thành 1 dashboard xem liên tục, dùng Lens dựng Line/Bar/Table/Metric. Không có phần Maps —
   đã xác nhận hệ thống này không có dữ liệu địa lý. Cần trọn vẹn 4 file trước.
6. **[06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md](06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md)** — hai dashboard SLO
   thật: `Xử lý sự cố — 7 service` (mọi panel theo thanh thời gian) và `Ngân sách lỗi tuần — 7 service` (cố định tuần lịch giờ Việt Nam), đo các chỉ số đã khai báo trong `service-manifest.yaml` cho 7 service. Cần file 05.
7. **[07-canh-bao-ngan-sach-loi.md](07-canh-bao-ngan-sach-loi.md)** — ngân sách lỗi theo tuần lịch giờ Việt Nam, 4 rule
   cảnh báo ES|QL (mốc 50/75/100 và trạng thái "cạn — ưu tiên độ tin cậy") hiện ngay trên dashboard của
   file 06. Cần file 06.
8. **[08-phat-hien-nhanh-va-xu-ly-su-co.md](08-phat-hien-nhanh-va-xu-ly-su-co.md)** — rule ES|QL
   `incident-fast-detection` báo service vượt SLO trong 5 phút gần nhất (bảng trên dashboard của file
   06), và truy vấn xác nhận khôi phục 15 phút cho buổi diễn tập sự cố on-call (SCRUM-36). Cần file 07.

9. **[09-dich-ket-noi-sai.md](09-dich-ket-noi-sai.md)** — nhóm 1, lỗi có kịch bản: một service trỏ tới đích
   không tồn tại; theo dõi lỗi lan từ cuối chuỗi gọi lên BFF và gateway. Cần file 06 và 08.
10. **[10-nghen-va-loi-theo-ty-le.md](10-nghen-va-loi-theo-ty-le.md)** — nhóm 2: cạn pool kết nối của gateway
    và 5xx theo tỷ lệ.
11. **[11-do-tre-orders.md](11-do-tre-orders.md)** — nhóm 3: độ trễ tiêm vào `Orders.Api`.
12. **[12-ha-tang-dung.md](12-ha-tang-dung.md)** — nhóm 4: một cơ sở dữ liệu dừng.
13. **[13-xac-thuc-hong.md](13-xac-thuc-hong.md)** — nhóm 5: địa chỉ máy chủ định danh sai.
14. **[14-thieu-tai-nguyen.md](14-thieu-tai-nguyen.md)** — nhóm 6: giới hạn CPU và bộ nhớ.
15. **[15-mang-dut.md](15-mang-dut.md)** — nhóm 7: một service bị tách khỏi mạng chung.
16. **[16-container-chet-hoac-khoi-dong-lai.md](16-container-chet-hoac-khoi-dong-lai.md)** — nhóm 8: container
    bị buộc dừng.
17. **[17-goi-y-theo-trieu-chung.md](17-goi-y-theo-trieu-chung.md)** — luyện **lỗi bất ngờ**: tra theo triệu
    chứng, mở dần gợi ý từ nhẹ đến mạnh rồi đáp án. Làm sau khi đã qua file 09–16.

Mỗi file 09–16 có bài tập tự làm và checklist "đã đạt". Quy trình triage, mức SEV và mẫu bản ghi sự cố dùng
chung cho cả chặng này nằm ở
[`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36)
và [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md) (không chép lại ở đây).

## Chuẩn bị chung cho cả 17 file

- Stack phải chạy qua **`docker-compose.local.yml`** (không phải `docker-compose.yml` mặc định) — chỉ
  file này mới publish Kibana (`5601`) và Elasticsearch (`9200`) ra host:
  ```bash
  ./scripts/local-up.ps1        # hoặc ./scripts/local-up.sh
  ```
- Import **collection Postman v2**: `postman/ecommerce.postman_collection.v2.json` +
  `postman/local.postman_environment.v2.json`. Đây là bản có sẵn folder lấy token thật, dùng ở file 04.
  Sau khi import, chọn environment **Ecommerce - Local** ở góc trên phải Postman.
- Không cần đăng nhập Kibana — `xpack.security.enabled: false` trong `docker-compose.local.yml`.
- Riêng file 06–17: import hai dashboard và rule cảnh báo theo
  [`dashboards/README.md`](dashboards/README.md) và [`alerts/README.md`](alerts/README.md). Riêng file 09–17: thêm
  `CHAOS_ALLOW_FAULT_INJECTION=true` vào `.env` (nhóm 3 còn cần `CHAOS_ALLOW_LATENCY_INJECTION=true`), chỉ
  trong lúc luyện, và chạy tải nền `./scripts/incident-drill.ps1 -Load` ở một terminal riêng để dashboard có
  số liệu. Lỗi luyện tính vào ngân sách lỗi tuần như sự cố thật.

## Dữ liệu của bạn đi đâu — sơ đồ 1 câu

```
Service .NET (7 service) --OTLP--> otel-collector --xuất song song--> [debug log] + [Elasticsearch]
                                                                              |
                                                                          Kibana đọc trực tiếp
```

`docker/otel-collector-config.yaml` xuất telemetry ra 2 nơi cùng lúc: exporter `debug` (in ra
`docker compose logs otel-collector`, dùng cho `scripts/demo.ps1`) và exporter `elasticsearch` (ghi
vào 3 data stream bên dưới) — không có APM Server trung gian, Kibana đọc thẳng Elasticsearch.

## 3 data stream thật — đã xác minh trực tiếp qua `GET /_cat/indices`

| Data stream | Chứa gì | Ghi chú |
|---|---|---|
| `traces-generic.otel-default` | 1 document = 1 span (1 lời gọi HTTP tại 1 service) | |
| `metrics-generic.otel-default` | Chỉ số runtime/.NET theo từng service | |
| `logs-generic.otel-default` | Log có cấu trúc từ `ILogger` | **Khác với comment trong `docker/otel-collector-config.yaml` dòng 38** (`logs-generic.default`) — tên đó là comment mô tả ý định, tên thật đang chạy có thêm `.otel`. `specs/017-otel-servicedefaults-elastic/quickstart.md` đã tự nêu cả 2 khả năng vì lý do này; file 01-03 dưới đây luôn dùng tên đã xác minh thật: `logs-generic.otel-default`. |

## 7 service thật đang phát dữ liệu

Đã xác nhận cả 7 đều có trace trong hệ thống của bạn ngay lúc viết tài liệu này (aggregation theo
`resource.attributes.service.name`): `Gateway.Api`, `Bff.Api`, `Identity.Api`, `Parties.Api`,
`Products.Api`, `Baskets.Api`, `Orders.Api`.

## Không có trong bộ tài liệu này

Kibana ở đây **không** bật `xpack.security`, không có Fleet/Elastic Agent, không có dữ liệu
endpoint/network — nên app "Security" (SIEM) đầy đủ của Kibana không dùng được và không được nhắc tới ở
file 04. File 04 chỉ dạy cách soi các sự kiện *liên quan* bảo mật (401/403, vi phạm tenant) bằng
traces/logs đã có sẵn — không phải dùng công cụ SIEM chuyên dụng.
