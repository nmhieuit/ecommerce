# Chạy nền tảng cục bộ (Running the platform locally)

*(Bản dịch tiếng Việt của [`local-development.md`](local-development.md) — bản gốc tiếng Anh vẫn
được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

1 lệnh duy nhất dựng lên toàn bộ nền tảng — mọi service, mọi dependency nó khai báo, và storefront —
mà không cần cài gì ngoài Docker.

```bash
cp .env.example .env       # không cần chỉnh sửa gì
./scripts/up.ps1           # hoặc ./scripts/up.sh
```

Sau đó mở **<http://localhost:4173>**.

Đó là toàn bộ việc thiết lập. Nếu bạn cần thêm 1 bước thứ 3, đó là 1 lỗi (defect) của tính năng này
chứ không phải 1 hướng dẫn còn thiếu ([spec SC-002](../specs/005-one-command-local-run/spec.md)).

## Điều kiện tiên quyết

| Cần | Vì sao |
|---|---|
| Docker với Compose v2 | Mọi thứ chạy trong container. Không cần gì khác — không .NET SDK, không Node, không pnpm |
| **6 GB** cấp phát cho Docker daemon | Xem [Chi phí thực tế](#chi-phí-thực-tế); lệnh khởi động kiểm tra điều này và từ chối chạy kèm thông báo rõ ràng nếu thấp hơn |
| ~10 GB dung lượng đĩa trống | Base image và 11 image được build |

## Các lệnh

| Lệnh | Nó làm gì |
|---|---|
| `./scripts/up.ps1` · `up.sh` | Build và khởi động mọi thứ. Chỉ trả về khi nền tảng dùng được |
| `./scripts/down.ps1` · `down.sh` | Dừng mọi thứ. **Giữ nguyên dữ liệu** |
| `./scripts/reset.ps1` · `reset.sh` | Dừng mọi thứ và **xoá dữ liệu**. Lần khởi động tiếp theo hành xử như lần chạy đầu tiên |
| `./scripts/up.sh --debug` · `up.ps1 -PublishInternalPorts` | Publish thêm các port nội bộ — xem [Vượt qua cửa chính](#vượt-qua-cửa-chính) |
| `./scripts/demo.ps1` · `demo.sh` | Chạy demo đặt đơn hàng Giai đoạn 1 từ đầu tới cuối — xem [Bản demo](#bản-demo) |

Tất cả các lệnh trên đều uỷ quyền cho Docker Compose chạy trên `docker-compose.yml` mặc định của
repository, nên `docker compose up --build --wait`, `docker compose down`, và
`docker compose down --volumes` cũng hoạt động. Các script tồn tại là để làm các kiểm tra điều kiện
tiên quyết mà Compose không làm được: không có daemon nó in ra lỗi socket, và không có `.env` nó sẽ
thay bằng 1 mật khẩu rỗng và để database fail sau đó vì 1 lý do trông có vẻ không liên quan.

`down` và `reset` là 2 lệnh riêng biệt có chủ đích. Dừng để nghỉ trong ngày và bắt đầu lại từ đầu là 2
ý định khác nhau, và gộp chung chúng là cách khiến ai đó mất trắng 1 buổi chiều các đơn hàng test.

## Bản demo

1 lệnh đặt 1 đơn hàng thật qua stack đang chạy, đọc lại nó, và báo cáo những gì nó đã chứng minh:

```bash
./scripts/demo.ps1        # hoặc ./scripts/demo.sh
```

Nó tự khởi động nền tảng ở chế độ demo nếu chưa chạy, nên đây cũng là 1 cách hợp lý để khởi động stack
lần đầu tiên. 1 lượt chạy lặp lại trên 1 stack đã ấm mất khoảng 10 giây (`-SkipStart` / `--skip-start`).

**[docs/demo-phase-1.md](demo-phase-1.md)** là bài hướng dẫn chi tiết: luồng trông như thế nào từng
bước kèm ảnh chụp màn hình, đường đi mà 1 lượt checkout đi qua các service, và luồng chạy này chứng
minh được tiêu chí thoát nào của Giai đoạn 1. Đọc file đó thay vì file này nếu điều bạn muốn là hiểu
nền tảng làm gì.

Chế độ demo khác với stack mặc định ở 2 điểm hẹp: nó publish service orders và baskets để demo có thể
truy vấn trực tiếp, và nó khiến telemetry collector in ra các span mà demo đọc lại. Không có gì khác
thay đổi, và `up`/`down`/`reset` không bị ảnh hưởng.

## Bạn nhận được gì

| Địa chỉ | Là gì |
|---|---|
| <http://localhost:4173> | Storefront |
| <http://localhost:5300> | Gateway — địa chỉ backend duy nhất mà storefront dùng |

**Không có gì khác được publish.** Các service, database, broker, cache, và collector chỉ truy cập
được trên mạng Compose. Đây là chủ đích: storefront được yêu cầu chỉ tiếp cận đúng 1 điểm vào duy
nhất của nền tảng và không gì khác, và việc để phần còn lại không được publish biến điều đó thành 1
thuộc tính của môi trường thay vì 1 quy tắc ai đó phải tự nhớ.

15 thành phần khởi động. 11 tiếp tục chạy; 4 là migrator áp dụng schema của từng service rồi thoát.
1 stack khoẻ mạnh trông như thế này:

```bash
docker compose ps -a
```

- **10** `Up (healthy)` — SQL Server, Redis, RabbitMQ, 4 domain service, BFF, gateway, storefront
- **1** `Up` không có trạng thái health — OpenTelemetry collector, có image distroless và không mang
  công cụ probe nào, nên Compose chỉ gate được trên việc nó đang chạy
- **4** `Exited (0)` — các migrator

## Chi phí thực tế

Đo trên máy phát triển đã dùng để xây cái này, với base image đã được pull sẵn:

| | Thời gian |
|---|---|
| Khởi động với image đã build sẵn | **~60 giây** |
| Khởi động sau 1 lượt reset (database mới, schema đã áp dụng, catalog đã seed) | **~87 giây** |
| Khởi động sau khi đổi source frontend | **~10 phút** — image storefront build lại từ source |

Bộ nhớ, trạng thái ổn định sau khi phục vụ request:

| Thành phần | Bộ nhớ |
|---|---|
| SQL Server | ~780 MB, tăng lên ~1.6 GB khi dùng liên tục |
| RabbitMQ | ~125 MB |
| 6 service .NET | ~45–70 MB mỗi service |
| Collector, storefront, Redis | ~40 MB gộp lại |
| **Tổng** | **~1.3 GB lúc rảnh, ~2.2 GB đỉnh điểm quan sát được** |

Ngưỡng sàn 6 GB mà lệnh khởi động thực thi không phải chính con số đó — mà là con số đó cộng thêm
khoảng đệm (headroom) BuildKit cần, vì lệnh này luôn build. 1 máy đúng 2.5 GB sẽ chạy được stack
nhưng sẽ fail khi build nó.

**1 khoảng trống thành thật**: các con số này được đo với base image của Docker đã có sẵn. 1 máy hoàn
toàn sạch cũng sẽ pull khoảng 2 GB base image ở lần chạy đầu, việc này phụ thuộc vào băng thông và
chưa được đo ở đây.

## 2 điều sẽ khiến bạn bối rối

### Request đầu tiên sau 1 lượt rebuild lớn có thể fail

Các health gate nói 1 service có thể kết nối tới database của nó. Chúng không nói nền tảng có thể phục
vụ 1 request — lúc khởi động lạnh, lời gọi đầu tiên qua bất kỳ đường nào phải trả giá cho JIT
compilation, xây dựng model EF, và tạo connection pool. Điều này đã vượt quá ngân sách 3 giây cho
downstream của BFF trong lúc test, nên `up` giờ làm ấm đường đi của request trước khi báo thành công.

Riêng biệt, và kém gọn gàng hơn: 1 lượt rebuild image 10 phút khiến máy bận tới mức các request có thể
timeout 1 lúc sau đó — mọi route trả lời `504` trong khi mọi health check báo khoẻ mạnh. Nó tự khắc
phục; các request ổn định trở lại ở mức 30–70 ms. Nếu bạn thấy điều này ngay sau 1 lượt rebuild lớn,
hãy chờ thay vì debug.

### Stack dùng chung 1 server database. Việc triển khai (deployment) thì không

Cả 4 database service ở đây đều nằm trên **1** container SQL Server. Mỗi service vẫn có database
riêng và connection string riêng, và không service nào được cấu hình trỏ tới database của service
khác — nhưng bản thân *server* thì dùng chung, và đây là 1 tiện lợi cục bộ cho ngưỡng sàn bộ nhớ,
**không phải topology khi triển khai**.

Các môi trường đã triển khai cho mỗi service 1 server database riêng, theo Principle I của
constitution. Đừng đọc việc gộp chung này như 1 sự cho phép chia sẻ database giữa các service, hay
vươn tay từ connection của 1 service sang dữ liệu của service khác.

Nếu bạn muốn topology mỗi-service-một-server đúng như thật ở cục bộ, `docker-compose.deps.yml` vẫn
cung cấp điều đó — file đó cũng tồn tại để chứng minh 1 service chạy được mà không cần các "hàng
xóm" của nó, điều mà stack này cố ý không thể hiện được.

## Vượt qua cửa chính

Đôi khi bạn cần 1 port nội bộ — phổ biến nhất là tài liệu OpenAPI của BFF, để sinh lại client API của
frontend.

```bash
./scripts/up.sh --debug            # hoặc ./scripts/up.ps1 -PublishInternalPorts
```

Lệnh đó lớp thêm `docker-compose.debug.yml` lên trên file mặc định và publish BFF (5301), 4 domain
service (5088, 5188, 5041, 5204), và giao diện quản lý của RabbitMQ (15672).

Nó cũng chuyển BFF sang môi trường Development để tài liệu OpenAPI thực sự được map, đồng thời khôi
phục lại các hostname compose mà cấu hình Development của nó nếu không sẽ trỏ tới `localhost`. Chỉ
publish port thôi là chưa đủ — xem các comment trong file đó.

## Khi có gì đó sai

Lệnh khởi động fail thay vì báo cáo 1 stack chạy nửa vời, và nêu đích danh thành phần:

```text
container ecomerce-stack-storefront-1 is unhealthy
```

Từ đó:

```bash
docker compose logs <component>          # vd: docker compose logs bff-api
docker compose ps -a                     # cái gì khoẻ mạnh, cái gì đã thoát, và với mã nào
```

1 dependency bị thiếu cũng hành xử tương tự. Khởi động mà không có database sẽ fail trong khoảng 90
giây, nêu tên mọi thành phần không thể tiếp tục, thay vì khởi động 1 stack sẽ fail ngay lần dùng đầu
tiên.

## Liên quan

- [`specs/005-one-command-local-run/quickstart.md`](../specs/005-one-command-local-run/quickstart.md) — các kịch bản verify mọi điều ở trên
- [`frontend/README.md`](../frontend/README.md) — các lệnh riêng của storefront
- [`services/README.md`](../services/README.md) — các service, và các quy tắc cô lập dữ liệu mà stack này nới lỏng ở cục bộ
