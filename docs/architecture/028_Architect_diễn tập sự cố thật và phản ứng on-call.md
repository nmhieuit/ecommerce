# Kiến trúc: Diễn tập sự cố thật và phản ứng on-call

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-36 ("[OPERATE-5] Trigger a real incident and run on-call response"). Đặc tả
tại [`specs/028-incident-oncall-drill/`](../../specs/028-incident-oncall-drill/). Các quyết định kiến
trúc, điểm xác minh V1–V6 và các quyết định người dùng chốt lúc triển khai nằm ở
[`research.md`](../../specs/028-incident-oncall-drill/research.md).

**Trạng thái xác minh**: không có test tự động. Đây là sai lệch Nguyên tắc III do người dùng chốt, hạn
tới khi SCRUM-37 xong, xem [`plan.md`](../../specs/028-incident-oncall-drill/plan.md). Mọi loại hỏng hóc,
rule, bảng dashboard và Case đã chạy trên stack Docker Compose thật, ở chế độ **không mù**. Buổi diễn tập
mù đầu tiên chưa chạy (T031, người dùng tự làm). Xem [technical-debt.md](technical-debt.md).

## 1. Bốn mảnh ghép, không mảnh nào nằm trong code service

| Mảnh | Nằm ở đâu | Vai trò |
|---|---|---|
| **Tiêm lỗi mù** | [`scripts/incident-drill.ps1`](../../scripts/incident-drill.ps1) `-Start` / `-Reveal`; file tạm `.incident-drill/<runId>/` (gitignore) | Bốc thăm service, loại lỗi, thời điểm; niêm phong + SHA-256; tạo lại 7 container với file compose override tạm |
| **Tải nền** | Cùng script, `-Load`; folder Postman 00 + 26 + **28** | Traffic cho cả 7 service để rule có dữ liệu |
| **Phát hiện** | Rule Kibana `incident-fast-detection`; export [`alerts/incident-fast-detection-rule.ndjson`](../kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson); bảng trên dashboard SLO hằng ngày | Alert theo service khi vượt SLO trong 5 phút gần nhất |
| **Phản ứng** | [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md) (quy trình triage), Kibana Case (tạo tay), [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md) | Severity SEV1–3, thông báo trạng thái, dòng thời gian có mốc tách bạch |

Người dùng chốt **không sửa code service** (Quyết định 1). Không file nào dưới `services/` hay
`shared/` bị đổi. Lỗi được tạo bằng biến môi trường sai, hoặc bằng header `X-Chaos-Fault` mà middleware
của 027 vốn đã hiểu.

## 2. Ba loại hỏng hóc, chỉ bằng cấu hình

| Loại | Áp dụng | Cách tạo | Số đo khi thử (2026-10-01/02) |
|---|---|---|---|
| A — đích kết nối sai | cả 7 | DB → `incident-missing-db`; BFF: 1 trong 4 downstream → `incident-missing-host`; gateway: cluster BFF → `incident-missing-host` | products 38/56 span 5xx; bff 49/243; gateway 93/226 |
| B — cạn pool | **chỉ gateway** | `ReverseProxy__Clusters__bff-cluster__HttpClient__MaxConnectionsPerServer=1` | gateway 45% span 5xx. `Max Pool Size=1` ở orders: 0 lỗi → người dùng bỏ B cho service có DB |
| C — 5xx của 027 | cả 7 | Tiến trình nền gửi request mang `X-Chaos-Fault: 5xx` vào cổng của service đích, tốc độ `r/(1−r)` × tốc độ nền, `r` = 5–50% | parties 31.8% (niêm phong 46%); identity 33.3% (niêm phong 39%) |

**File override tạm.** Script ghi `docker-compose.incident.yml`, chỉ có khối `environment` cho service
đích, rồi chạy compose với `-f docker-compose.local.yml -f <override>`. Mật khẩu không bao giờ nằm trong
file này: giá trị là biểu thức nội suy (`${MSSQL_SA_PASSWORD}`), Compose đọc từ `.env` lúc chạy.

**Tạo lại cả 7 container, mỗi service một lệnh `up --no-deps`.** Mục đích là để uptime trong `docker ps`
không lộ đích. Ban đầu dùng một lệnh gộp, nhưng BFF và gateway kẹt ở `Created` vì chờ service đích
healthy theo `depends_on`, và Compose in đích danh service hỏng.

## 3. Phát hiện nhanh, khác ngân sách tháng của 027

Rule của 027 đo tiêu hao **cả tháng**, nên một sự cố đơn lẻ cần hàng giờ mới qua mốc 50%. Rule
`incident-fast-detection` chỉ nhìn **5 phút**:
- chu kỳ 5 phút, `groupBy: row`;
- `STATS ... BY service | KEEP service` để mã alert ổn định (Hệ quả 2–3 của research 027).

Một service có alert khi 5xx ≥ 0.1%, hoặc p95/p99 vượt `slos.latency`. **Riêng gateway chỉ xét 5xx**:
ngưỡng 150/500 ms của gateway chặt hơn ngưỡng 300/800 ms của BFF mà nó chuyển tiếp tới, nên gateway vượt
độ trễ ngay ở mức nền. Rule ở file export riêng, để `ErrorBudgetRuleDefinitionTests` của 027 (đếm đúng
4 rule) không bị ảnh hưởng. Không có test nào canh ngưỡng của rule này.

## 4. Tải nền phải nhẹ và tự sửa token

Đo thật cho thấy tải nền quyết định việc rule có ý nghĩa hay không:

| Tải nền | Mức nền (không sự cố) |
|---|---|
| Lấy token mỗi vòng, nghỉ 200 ms | identity `POST /connect/token` p50 311 ms → identity luôn vượt SLO |
| Token mỗi 30 phút, nghỉ 200 ms | 5xx nền ở gateway, BFF, baskets; p95 vượt ở hầu hết service |
| Token mỗi 30 phút, **nghỉ 1000 ms** (người dùng chốt) | 0 lỗi, p95 ≤ 133 ms; rule trả 0 hàng |

Tạo lại `identity-api` làm token cũ hỏng theo hai cách:
- `401` ở gateway;
- hoặc qua được gateway (còn giữ khoá cũ) nhưng bị service hạ lưu vừa tạo lại từ chối, và BFF trả
  `502`/`504`.

Vì thế `-Load` lấy token lại không chỉ sau 30 phút, mà cả khi runner gặp `401` hoặc khi Id container
`identity-api` đổi. Đo được: token mới có trong 27 giây.

## 5. Khôi phục và xác nhận

- **Khôi phục chuẩn** cho mọi loại: bỏ `CHAOS_ALLOW_FAULT_INJECTION` khỏi `.env` rồi chạy lại compose.
  Cờ đổi nên cả 7 container được tạo lại không kèm override. Loại C không có override, nên chỉ chạy lại
  compose mà không đổi cờ thì **không** tạo lại đích và việc gửi header không dừng.
- **Xác nhận** bằng truy vấn ES|QL theo phút: 15 phút liên tục có traffic và đạt SLO, đồng thời rule
  không còn active cho service đó (file
  [`08`](../kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút)).

## 6. Giới hạn đã biết

- **Nhiễu khởi động nguội**: tạo lại 7 container làm mọi service chậm 5–7 phút (đo thật). Tiêu chí
  người dùng chốt là alert qua ≥ 2 lần chạy rule mới là sự cố, nên đôi khi nhiễu bị tính là sự cố.
- **Môi trường local chậm từng đợt** không rõ nguyên nhân: parties, products, baskets, identity có lúc p95
  320–560 ms mà không bị tiêm, và rule lại bắn cho chúng sau hơn 15 phút.
- **Lỗi lan theo chuỗi phụ thuộc**: hỏng orders thì BFF (22.5%) và gateway (31.9%) cũng có alert. Đây là
  hành vi đúng, nhưng người vận hành phải tự lần ra gốc.
- **Diễn tập tiêu hao ngân sách lỗi tháng như thật** (người dùng chốt). Sau các lần thử, cả 7 service đã ở
  trạng thái "cạn ngân sách" của 027.
- Niêm phong dựa vào kỷ luật không mở `.incident-drill/` (người dùng chọn băm, không mã hoá).
- Không có test tự động (sai lệch Nguyên tắc III), nên ngưỡng trong rule có thể lệch khỏi manifest mà
  không bị phát hiện.

Đầy đủ: [technical-debt.md](technical-debt.md) mục 028.

## 7. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/028-incident-oncall-drill-component.drawio`](../diagrams/028-incident-oncall-drill-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/028-incident-oncall-drill-flow-nghiep-vu.drawio`](../diagrams/028-incident-oncall-drill-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/028-incident-oncall-drill-sequence.drawio`](../diagrams/028-incident-oncall-drill-sequence.drawio)

## 8. Tham khảo thêm

Cách dựng rule/bảng, truy vấn đầy đủ:
[`08-phat-hien-nhanh-va-xu-ly-su-co.md`](../kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md).
Quy trình chạy buổi diễn tập:
[`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md).
Postmortem và ticket follow-up thuộc SCRUM-37.
