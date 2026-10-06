# Kiến trúc: Danh mục 8 nhóm lỗi để luyện troubleshoot

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Spec C của đợt rà soát nợ kỹ thuật (mở rộng công cụ diễn tập sự cố của 028, thay phần
kill-pod của 025). Đặc tả tại [`specs/031-error-group-catalog/`](../../specs/031-error-group-catalog/).
Quyết định kiến trúc, điểm xác minh V1–V9 (kèm số đo thật) và các quyết định người dùng chốt nằm ở
[`research.md`](../../specs/031-error-group-catalog/research.md).

**Trạng thái xác minh**: không có test tự động. Đây là sai lệch Nguyên tắc III do người dùng chốt, hạn
tới khi spec D (tài liệu troubleshoot chi tiết) hoàn tất, xem
[`plan.md`](../../specs/031-error-group-catalog/plan.md). Cả 9 loại lỗi A–I đã tiêm và khôi phục thật trên
stack Docker Compose local, dưới tải nền `-Load`; bốc thăm mù đã chạy 24 lần (phủ đủ 8 nhóm) và một
vòng mù trọn vẹn (tiêm → khôi phục → mở niêm phong); các lệnh của 028 chạy lại không đổi hành vi. Xem
[technical-debt.md](technical-debt.md) mục 031.

## 1. Ba mảnh, không mảnh nào nằm trong code service

| Mảnh | Nằm ở đâu | Vai trò |
|---|---|---|
| **Danh mục** | [`scripts/incident-drill/catalog.json`](../../scripts/incident-drill/catalog.json) | Dữ liệu thuần: 8 nhóm, 9 loại A–I, đích áp dụng, tham số trừu tượng, triệu chứng, ba mức gợi ý. KHÔNG có lệnh docker hay biến compose |
| **Bộ chuyển đổi Compose** | Các hàm `Invoke-ComposeInject` / `Invoke-ComposeRestore` trong [`scripts/incident-drill.ps1`](../../scripts/incident-drill.ps1) | Nơi duy nhất biết cách tiêm và khôi phục từng loại trên `docker-compose.local.yml` |
| **Trạng thái từng lần chạy** | `.incident-drill/<runId>/` (gitignore): `sealed.json` (băm SHA-256), `state.json`, `hint-log.json`, `stop.flag`, `injector.log` | Niêm phong lựa chọn; theo dõi `pending → injected → restored/failed`; nhật ký gợi ý |

Người dùng giữ ràng buộc của 028: **không sửa code service**. Không file nào dưới `services/` hay
`shared/` bị đổi. Lỗi được tạo bằng biến môi trường sai, header của middleware 025/027 vốn có sẵn, hoặc
lệnh `docker` bên ngoài.

## 2. Tách "loại lỗi" khỏi "nơi tiêm"

Danh mục chỉ nói *cái gì hỏng* và *triệu chứng*, bằng từ vựng trừu tượng (`restoreKind`:
`recreate-without-override`, `stop-sending`, `resume-component`, `reattach-network`, `recreate-target`;
tham số như `latencyMs`, `cpuLimit`, `memoryLimitMb`). Bộ chuyển đổi Compose ánh xạ sang lệnh cụ thể.
Khi script nạp danh mục, nó kiểm: đủ 8 nhóm và 9 loại, mỗi `restoreKind` được bộ chuyển đổi hỗ trợ, danh
mục không chứa `docker` hay `${`, và gợi ý mức 1–2 không lộ tên đích hay mã loại.

**Điểm mở rộng**: thêm bộ chuyển đổi CD/Kubernetes sau này = thêm một tập hàm cùng giao diện (tiêm, khôi
phục, chờ khoẻ) chọn theo "nơi tiêm". Danh mục không đổi.

## 3. Chín loại, chỉ bằng cấu hình hoặc công cụ ngoài

| Nhóm / Loại | Đích | Cách tiêm (Compose) | Khôi phục (`-Restore`) | Số đo khi thử (2026-10-05/06) |
|---|---|---|---|---|
| 1 / **A** đích kết nối sai | 7 service | File override, tạo lại cả 7 | Tạo lại 7 không override | orders: 504/502 qua gateway; khôi phục 40 s |
| 2 / **B** cạn pool | gateway | `MaxConnectionsPerServer=1`, tạo lại cả 7 | như A | **không có triệu chứng** dưới `-Load` 1 tiến trình: 0% 5xx (giới hạn của 028, cần tải đồng thời) |
| 2 / **C** 5xx theo tỷ lệ | 7 service | Tạo lại 7 (nhận cờ) + vòng gửi `X-Chaos-Fault: 5xx` | Dừng vòng gửi (`stop.flag`) | products: niêm phong 31% → đo 31,9% |
| 3 / **D** độ trễ | orders-api | Tạo lại 7 + vòng **bất đồng bộ** gửi `X-Chaos-Latency-Ms: 2000` thẳng vào `:5041` kèm token + `X-Tenant-Id` | Dừng vòng gửi | 44/216 span ≥ 1,9 s (20%), p95 2006 ms, 5xx = 0 |
| 4 / **E** DB dừng | 5 DB | `docker stop` | `docker start`, chờ DB healthy rồi service `/health/ready` 200 | `/health/ready` 503 trong ≤ 6 s; ready lại sau ≈ 68 s (orders), 18 s (products) |
| 5 / **F** Authority sai | 6 service (không có identity-api) | Override `Identity__Authority`, tạo lại cả 7 | như A | gateway 401 sau ≈ 24 s; BFF 504 sau ≈ 10 s; service phía sau: 502 qua gateway; khôi phục ≈ 31 s |
| 6 / **G** thiếu tài nguyên | 7 service | `docker update --cpus 0.1 --memory 256m --memory-swap 256m` | Tạo lại container đích | p95 ≈ 102 ms, p99 ≈ 198 ms dưới `-Load` — **triệu chứng nhẹ**, chưa vượt 150 ms; 96 m bị OOM-kill |
| 7 / **H** mạng đứt | 7 service | `docker network disconnect …_backbone` | `network connect --alias <service> --alias <container>` | gateway 504 sau ≈ 3 s; container vẫn `healthy`; khôi phục 1–3 s |
| 8 / **I** container chết | 7 service | `docker kill` (không có `restart:`) | Tạo lại container đích | exit 137, gateway 504 sau ≈ 3 s; khôi phục ≈ 11–13 s |

Chỉ nhóm **A, B, C, D, F** tạo lại cả 7 container (giữ nhiễu che đích của 028). Nhóm **E, G, H, I** không
che: `docker ps` có thể lộ đích; người dùng chấp nhận.

## 4. Hai dạng tiêm, một cơ chế khôi phục

- **Dạng (a)** `-Inject -Type|-Group [-Target] [-DurationSeconds]`: không phải bài mù, in rõ đã tiêm gì;
  hết thời lượng thì tự khôi phục (cùng hàm `Complete-Restore`).
- **Dạng (b)** `-Start`: bốc nhóm đều 1/8 → loại → đích → tham số → trễ 0–30 phút; chỉ in `runId` và mã
  băm. Loại có cờ chưa bật (hoặc loại D khi chưa có token của `-Load`) bị loại khỏi vòng bốc.
- **`-Restore`** dùng chung: tạo `stop.flag` để tiến trình nền đang chờ/đang gửi header dừng mà không bị
  kill giữa lúc tiêm, rồi chạy thao tác khôi phục theo `restoreKind` và chờ đích khoẻ (tối đa 10 phút).
  Chờ `/health/ready` 200, không chỉ health của docker: container service vẫn `healthy` khi DB đã dừng.
- Chỉ một lần chạy mở tại một thời điểm (`pending`/`injected`/`failed` chặn `-Start`/`-Inject` mới).

## 5. Gợi ý theo mức

`-Hint -RunId <id> -Level 1|2|3`: mức 1 mô tả triệu chứng, mức 2 nêu tên nhóm, mức 3 là đáp án (kiểm băm
như `-Reveal`, điền `{target}`, `{parameters}`…). Nội dung nằm trong danh mục. Mỗi lần mở ghi
`hint-log.json` (kể cả bỏ cách mức); không ghi vào Kibana Case, không đổi mốc thời gian của bản ghi sự
cố. `-Reveal` in lại nhật ký này.

## 6. Giới hạn đã biết

- **Loại B không có triệu chứng** dưới `-Load` 1 tiến trình (0% 5xx): cần tải đồng thời cao hơn.
- **Loại G nhẹ**: `--cpus 0.1` chưa vượt ngưỡng p95 150 ms dưới tải nền hiện tại (`--cpus 0.05` thì trễ
  rõ, `0.02` gần như chết). `--memory 96m` làm container bị OOM-kill nên chốt 256 m.
- **Dừng identity-api không có triệu chứng** trên đường dữ liệu (token cũ còn dùng được nhờ khoá ký đã
  cache): đã bỏ khỏi nhóm 5.
- **Redis và RabbitMQ không là đích của nhóm 4**: không service nào dùng Redis; RabbitMQ chỉ orders-api
  dùng khi `ORDERS_RABBITMQ_CONNECTION` được đặt (mặc định rỗng).
- **Header chaos không đi xuyên gateway/BFF** (đã biết từ QA 025): loại D phải gửi thẳng orders-api, nên
  chỉ làm chậm các request do script gửi, không làm chậm lưu lượng qua BFF.
- **Nhiễu khởi động nguội 5–7 phút** và **token hỏng sau khi tạo lại identity** (028) áp dụng thêm: sau
  A/B/C/D/F và khi khôi phục, `-Load` cần 1–2 phút lấy token mới; trong lúc đó request có token trả 401/502.
- **Không che đích** cho E, G, H, I; niêm phong dựa vào kỷ luật không mở `.incident-drill/`.
- Diễn tập tiêu hao ngân sách lỗi tuần như thật (spec 029).
- Không có test tự động (sai lệch Nguyên tắc III): danh mục và bộ chuyển đổi có thể lệch nhau, hoặc lệnh
  docker đổi hành vi theo phiên bản, mà không build nào bắt.

Đầy đủ: [technical-debt.md](technical-debt.md) mục 031.

## 7. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/031-error-group-catalog-component.drawio`](../diagrams/031-error-group-catalog-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/031-error-group-catalog-flow-nghiep-vu.drawio`](../diagrams/031-error-group-catalog-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/031-error-group-catalog-sequence.drawio`](../diagrams/031-error-group-catalog-sequence.drawio)

## 8. Tham khảo thêm

Quy trình chạy buổi diễn tập, danh mục và lệnh:
[`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md). Kiến trúc nền của
công cụ: [`028_Architect_diễn tập sự cố thật và phản ứng on-call.md`](028_Architect_diễn%20tập%20sự%20cố%20thật%20và%20phản%20ứng%20on-call.md).
Tài liệu troubleshoot chi tiết theo triệu chứng thuộc spec D.
