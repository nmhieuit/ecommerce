# QA: Chạy toàn bộ local bằng một lệnh, container thật

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát (US1 → US3)

1. **Một lệnh dựng cả nền tảng** (US1): `cp .env.example .env` rồi `./scripts/up.ps1` (hoặc `up.sh`) —
   script kiểm tra điều kiện tiên quyết (Docker, daemon, `.env`, bộ nhớ ≥ 6 GB), `docker compose up --build
   --wait` (migration + seed tự chạy), làm nóng đường đi, và chỉ báo "The platform is up" khi mọi thành
   phần khoẻ mạnh.
2. **Storefront chạy đầu-cuối trên container** (US2): mở `http://localhost:4173` (đăng nhập bằng user dev,
   từ spec 004 FR-026) → duyệt → giỏ → thanh toán → xác nhận; mọi request đi qua gateway `:5300`.
3. **Dừng, khởi động lại, reset** (US3): `down` giữ dữ liệu; `reset` xoá volume để lần sau như lần đầu; sửa
   mã nguồn thì `--build` chạy đúng mã mới.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Khác 001-004: chủ thể của spec này **chính là** stack mặc định `docker-compose.yml` cùng các script
`scripts/up|down|reset.*` (kịch bản trong [`specs/005-one-command-local-run/quickstart.md`](../../specs/005-one-command-local-run/quickstart.md)
đã đúng dạng Docker), nên phần thủ công chạy các script đó thay vì `docker-compose.local.yml` (file dùng
cho từng service riêng lẻ ở các spec trước). Kịch bản thủ công có số liệu đo thật khác nằm ở
[`docs/local-testing.md`](../local-testing.md).

> Nếu `localhost` không gọi được dù container `healthy`: khởi động lại hẳn Docker Desktop (lỗi forwarding
> IPv6 loopback `::1` của WSL2), không phải lỗi ứng dụng.

### Thủ công — chạy đúng như spec mô tả

Điều kiện: Docker Desktop cấp ≥ 6 GB (máy QA: 15.5 GB) và `.env` tạo bằng `cp .env.example .env`. Lưu ý: `.env`
cũ (tạo trước spec 014) có thể chỉ có `MSSQL_SA_PASSWORD`; khi đó thiếu `ClientSecret`/`TestUserPassword` nên
không có mật khẩu để đăng nhập storefront và bước làm nóng bị bỏ qua (xem [QA_Debt.md](QA_Debt.md)).

```bash
cp .env.example .env
./scripts/up.ps1        # hoặc ./scripts/up.sh   (dừng: down.ps1 | giữ dữ liệu; xoá dữ liệu: reset.ps1)
```

Mở `http://localhost:4173`, đăng nhập `postman-test@local.test` + `TestUserPassword` trong `.env` (script in sẵn hướng dẫn này ở cuối).

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Tiên quyết — thiếu `.env` (Scenario 7, FR-011) | Chạy `up.ps1` từ 1 bản sao repo không có `.env` | Nêu tên `.env` + template, trước khi khởi động gì | Đúng: "Cannot start the stack: '.env' does not exist. Copy the template first...", `exit 1`, không container nào được tạo |
| Tiên quyết — daemon không phản hồi (Scenario 7) | Đặt `DOCKER_HOST=tcp://127.0.0.1:1` rồi chạy `up.ps1` | Nêu tên "Docker daemon" | **Chưa đạt trên Windows PowerShell 5.1**: nhận lỗi thô `docker : error during connect ... NativeCommandError`, không phải câu của script (xem QA_Debt) |
| Scenario 1 — lần chạy đầu (SC-001 < 10 phút) | `./scripts/up.ps1` | Thành công < 10 phút; không phải chờ thêm | **8 phút 15 giây, `exit 0`**, nhưng cache build đã ấm một phần (1 lượt build trước đó bị gián đoạn); lượt build lạnh hoàn toàn chưa đo được — xem QA_Debt |
| Scenario 1 — kiểm kê thành phần | `docker compose ps -a` | 15 thành phần: 10 healthy, 1 Up, 4 Exited(0) | **19 thành phần: 13 `healthy`** (elasticsearch, kibana, redis, rabbitmq, sqlserver, storefront, 5 API gồm identity, bff, gateway), **1 `Up`** (otel-collector, không có probe), **5 `Exited (0)`** (migrator của products, baskets, orders, parties, identity) |
| Làm nóng đường đi | Cuối `up.ps1` | Không có bước thủ công nào còn lại | Có `TestUserPassword` → in "Warming the request path..." rồi "The platform is up"; thiếu → in "no TestUserPassword in .env ... skipping the warm-up" |
| Scenario 1 — lần chạy sau (SC-001 < 3 phút) | `up.ps1` khi image đã build | < 3 phút | Không đổi gì: **1m36s**; sau `down`: **2m51s**; sau `reset`: **3m01s** (sát/vượt ngưỡng) |
| Scenario 2 — storefront đầu-cuối (SC-003) | Chạy walkthrough Playwright trỏ vào container (bảng "Tự động") | Duyệt → giỏ → thanh toán → xác nhận, không lỗi console, chỉ gọi gateway | Lần 1: 2/4 đỏ (giỏ trống sau khi thêm; thanh toán hỏng) ngay sau khi stack vừa dựng lại; lần 2: 3/4 PASS, chỉ đỏ ở assertion "storage không có gì" đã ghi ở QA 004 |
| Scenario 3 — dừng (US3) | Tạo 1 đơn, rồi `./scripts/down.ps1` | Không container mồ côi; cổng giải phóng; volume còn | **21 giây**; 0 container trong project `ecomerce-stack`; cổng `4173`/`5300`/`5205` không còn lắng nghe; còn **3** volume (`elasticsearch-data`, `rabbitmq-data`, `sqlserver-data`) |
| Scenario 3 — khởi động lại | `up.ps1`, đọc lại đơn qua gateway (kèm token) | Đơn cũ còn nguyên | Đúng: `GET /bff/orders/<mã>` → `200`, total `12.50`. **Chưa lặp 10 chu kỳ** (SC-004), mới chạy 1 chu kỳ |
| Scenario 4 — reset (FR-008, SC-008) | `./scripts/reset.ps1` rồi `up.ps1` | Volume bị xoá; sau đó chỉ còn 3 sản phẩm seed, không đơn cũ | **25 giây**; 3 volume bị xoá; sau `up`: `GET /bff/orders/<mã cũ>` → `404`, `/bff/products` → 3 sản phẩm, `/bff/basket` → giỏ rỗng |
| Scenario 5 — thiếu dependency (SC-005) | `docker compose stop sqlserver` rồi `up.ps1` | Lệnh thất bại ≤ 2 phút và nêu tên thành phần | **Không thất bại**: `up.ps1` khởi động lại chính `sqlserver` (nó thuộc stack) và thành công (`exit 0`, 1m41s). Kịch bản như viết không tạo ra lỗi; các biến thể "gỡ migrator khỏi Compose" chưa chạy |
| Scenario 6 — đổi mã nguồn (FR-009) | Đổi chữ ở storefront rồi `up` | Chữ mới xuất hiện | **Chưa chạy** |
| Scenario 8 — chỉ cổng vào công bố | `GET :5300/bff/products`, `GET :5301/...`, kiểm cổng nội bộ | 5300 trả lời; 5301 bị từ chối | Đúng: `5300` → `200` (kèm token); `5301` → "Unable to connect"; `1433`, `5088`, `5188`, `5041`, `5204`, `5672`, `6379`, `9200` không lắng nghe. Cổng công bố **thực tế là 3** (`4173`, `5300`, `5205`). Chế độ `--debug` chưa chạy |
| Dọn dẹp | `./scripts/down.ps1` (giữ dữ liệu) hoặc `reset.ps1` | | |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm/`test()` (comment mỗi hàm đã gắn `Task nguồn: spec 005 ...` hoặc spec gốc, xem lại tại đó nếu cần biết test ứng với US/task nào). Test hậu tố `IntegrationTests` dùng Testcontainers → cần Docker Desktop đang chạy.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US1/FR-014 — mọi image service build được từ checkout sạch: Dockerfile `COPY` đủ mọi `shared/*` mà `.csproj` tham chiếu; scanner không bị "mù" | [`DockerfileSharedProjectTests.cs:33`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L33) · [`:48`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L48) · [`:67`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L67) · [`:99`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L99) · [`:119`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L119) | `dotnet test tests/ContainerConventionTests --filter "FullyQualifiedName~DockerfileSharedProjectTests"` |
| US2/FR-004,005 — gateway admit origin của storefront container (`:4173`) và dev server (`:5173`); danh sách origin đọc từ cấu hình; origin lạ bị từ chối | [`StorefrontCorsTests.cs:33`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L33) · [`:61`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L61) · [`:91`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L91) · [`:116`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L116) · [`:146`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L146) · [`:171`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L171) · [`:200`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L200) · [`:232`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L232) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~StorefrontCorsTests"` |
| US2/SC-003 — bài chấp nhận: walkthrough của 004 chạy trên container (`STOREFRONT_URL=http://localhost:4173 GATEWAY_ORIGIN=http://localhost:5300`) — luồng đầu-cuối, không lỗi console, chỉ gọi gateway, double-checkout, chỉ bàn phím | [`walkthrough.spec.ts:78`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L78) · [`:153`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L153) · [`:182`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L182) · [`:217`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L217) | `cd frontend && STOREFRONT_URL=http://localhost:4173 GATEWAY_ORIGIN=http://localhost:5300 E2E_USERNAME=postman-test@local.test E2E_PASSWORD=<TestUserPassword> corepack pnpm --filter @ecommerce/web e2e` (stack đang chạy; cần shim `pnpm`, xem QA 004) |
| FR-001 — file Compose hợp lệ (không link test) |  | `docker compose config --quiet` |

**Kết quả lượt QA này**: `DockerfileSharedProjectTests` 9/9 PASS (cả project `ContainerConventionTests`) · `StorefrontCorsTests` 10/10 PASS ·
`docker compose config --quiet` hợp lệ · walkthrough Playwright trên container: **lần 1 2/4 PASS, lần 2 3/4 PASS** — test đỏ còn lại là
`browse, add to basket, check out, and see the confirmation` ở `walkthrough.spec.ts:112` (assertion "không có gì trong browser storage",
xem QA 004). Lệnh e2e cần `pnpm` trong PATH (shim `pnpm.cmd` gọi `corepack pnpm`).

## Kết luận

**PASS kèm ghi chú** — luồng cốt lõi của 005 chạy đúng trên máy thật: một lệnh `./scripts/up.ps1` dựng 19 thành phần (13
`healthy`, 1 `Up`, 5 `Exited (0)`), migration + seed tự chạy, storefront/gateway trả lời; `down` giữ dữ liệu (đơn cũ đọc lại được), `reset`
đưa về đúng trạng thái lần đầu (3 sản phẩm, không đơn), chỉ 3 cổng được công bố và BFF/DB/broker không truy cập được từ host; 9/9
test container convention và 10/10 test CORS PASS. Ghi chú cần xử lý: (1) **SC-001 "dưới 10 phút" chưa xác nhận được** — lượt đo được
8m15s đã có cache ấm một phần, lượt build lạnh hoàn toàn chưa hoàn tất trong thời gian quan sát; (2) `up.ps1` trên Windows PowerShell 5.1 không
in được thông báo tiên quyết khi daemon không phản hồi và vỡ khi bị chuyển hướng luồng; (3) Scenario 5 (thiếu dependency) như viết không
tạo ra lỗi; (4) tài liệu còn ghi 15 thành phần/2 cổng/2 volume trong khi thực tế 19/3/3; (5) walkthrough e2e trên container vẫn đỏ ở
assertion storage của QA 004. Chi tiết: [QA_Debt.md](QA_Debt.md).
