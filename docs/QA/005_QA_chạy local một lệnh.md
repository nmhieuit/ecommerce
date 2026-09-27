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

Sau khi `up.ps1` xong, folder Postman đánh số **`05 - Chạy local một lệnh (smoke sau khi up.ps1)`** (2 request: gateway + BFF health live) cho một smoke check nhanh trước khi đi tiếp các bước đo bằng tay dưới đây — bản thân script `up/down/reset` không đo được qua Postman.

Điều kiện: Docker Desktop cấp ≥ 6 GB (máy QA: 15.5 GB) và `.env` tạo bằng `cp .env.example .env`. Lưu ý: `.env`
cũ (tạo trước spec 014) có thể chỉ có `MSSQL_SA_PASSWORD`; khi đó thiếu `ClientSecret`/`TestUserPassword` nên
không có mật khẩu để đăng nhập storefront và bước làm nóng bị bỏ qua (xem [QA_Debt.md](QA_Debt.md)).

```bash
cp .env.example .env
./scripts/up.ps1        # hoặc ./scripts/up.sh   (dừng: down.ps1 | giữ dữ liệu; xoá dữ liệu: reset.ps1)
```

Mở `http://localhost:4173`, đăng nhập `postman-test@local.test` + `TestUserPassword` trong `.env` (script in sẵn hướng dẫn này ở cuối).

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|
| Tiên quyết — thiếu `.env` (Scenario 7, FR-011) | Chạy `up.ps1` từ 1 bản sao repo không có `.env` | Nêu tên `.env` + template, trước khi khởi động gì | Không đổi so với lượt trước: đúng, `exit 1`, không container nào được tạo |
| Tiên quyết — daemon không phản hồi (Scenario 7) | `DOCKER_HOST=tcp://127.0.0.1:1` rồi chạy `up.ps1` | Nêu tên "Docker daemon" | Không đổi: nhận `NativeCommandError` thô của PowerShell 5.1, không phải câu của script — xem QA_Debt |
| Scenario 3 — dừng (US3) | `./scripts/down.ps1` khi có 1 đơn vừa đặt | Không container mồ côi; cổng giải phóng; volume còn | **19 giây**; 0 container còn lại trong project `ecomerce-stack`; volume `sqlserver-data`/`rabbitmq-data`/`elasticsearch-data` còn nguyên |
| Scenario 1 — lần chạy sau `down` (cache ấm) | `./scripts/up.ps1` | < 3 phút | **170 giây (2m50s)** — trong ngưỡng |
| Scenario 3 — khởi động lại, đơn cũ còn nguyên | `GET /bff/orders/<mã đơn đã tạo trước down>` | Đơn cũ đọc lại được | `200`, `total = 12.50` — đúng |
| Scenario 4 — reset (FR-008, SC-008) | `./scripts/reset.ps1` | Volume bị xoá | **16 giây**; cả 3 volume (`sqlserver-data`, `rabbitmq-data`, `elasticsearch-data`) bị xoá |
| Scenario 1 — chạy lại sau reset (như lần đầu) | `./scripts/up.ps1` | Thành công, seed lại catalog | **164 giây**; `exit 0` |
| Scenario 4 — xác nhận đã xoá sạch | `GET /bff/orders/<mã đơn cũ>`, `GET /bff/products`, `GET /bff/basket` | Đơn cũ biến mất; chỉ còn 3 sản phẩm seed; giỏ rỗng | Đơn cũ **`404`**; catalog đúng **3** sản phẩm (Notebook/Pour-Over/Apron); giỏ `items: []` |
| Scenario 1 — kiểm kê thành phần (trên stack vừa dựng lại) | `docker ps -a --filter label=com.docker.compose.project=ecomerce-stack` | 15 thành phần: 10 healthy, 1 Up, 4 Exited(0) | **19 thành phần: 13 `healthy`, 1 `Up`** (otel-collector, không có probe), **5 `Exited (0)`** (5 migrator) — không đổi so với lượt trước, xem QA_Debt |
| Scenario 8 — chỉ cổng vào công bố | Quét cổng `4173/5300/5205/5301/5088/5188/5041/5204/1433/5672/15672/6379/9200/5601` | Chỉ cổng gateway/storefront lắng nghe | Đúng **3** cổng lắng nghe: `4173`, `5300`, `5205` (identity, thêm từ spec 004); 11 cổng còn lại đóng |
| Test tự động chạy trên stack vừa dựng bằng `up.ps1` (đối chiếu US2) | `00 - Xác thực & phân quyền → 01`, folder `Gateway`, `00 - Smoke Flow` | Hành vi giống các spec trước, không phụ thuộc script dựng | `Gateway`: OpenAPI đi qua gateway `404` (rỗng) ngay sau khi vừa `up` xong — cold start, các bước còn lại `200`; `Smoke Flow`: bước 00 "Dọn giỏ" đứng trước khi có dòng nào trong giỏ nên **không có phản hồi** (giỏ đã rỗng từ đầu, không phải lỗi); chạy lại đủ chu trình → **18/19** xanh (1 đỏ hợp lý: bước 09 "đặt hàng lần hai" `201` vì giỏ đã được thêm lại hàng giữa 2 lần chạy — không phải hồi quy) |
| Dọn dẹp | `./scripts/down.ps1` (giữ dữ liệu) hoặc `reset.ps1` | | Đã `down`; dữ liệu giữ nguyên cho lượt QA kế tiếp |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm/`test()` (comment mỗi hàm đã gắn `Task nguồn: spec 005 ...` hoặc spec gốc, xem lại tại đó nếu cần biết test ứng với US/task nào). Test hậu tố `IntegrationTests` dùng Testcontainers → cần Docker Desktop đang chạy.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US1/FR-014 — mọi image service build được từ checkout sạch: Dockerfile `COPY` đủ mọi `shared/*` mà `.csproj` tham chiếu; scanner không bị "mù" | [`DockerfileSharedProjectTests.cs:33`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L33) · [`:48`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L48) · [`:67`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L67) · [`:99`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L99) · [`:119`](../../tests/ContainerConventionTests/DockerfileSharedProjectTests.cs#L119) | `dotnet test tests/ContainerConventionTests --filter "FullyQualifiedName~DockerfileSharedProjectTests"` |
| US2/FR-004,005 — gateway admit origin của storefront container (`:4173`) và dev server (`:5173`); danh sách origin đọc từ cấu hình; origin lạ bị từ chối | [`StorefrontCorsTests.cs:33`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L33) · [`:61`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L61) · [`:91`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L91) · [`:116`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L116) · [`:146`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L146) · [`:171`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L171) · [`:200`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L200) · [`:232`](../../services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs#L232) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~StorefrontCorsTests"` |
| US2/SC-003 — bài chấp nhận: walkthrough của 004 chạy trên container (`STOREFRONT_URL=http://localhost:4173 GATEWAY_ORIGIN=http://localhost:5300`) — luồng đầu-cuối, không lỗi console, chỉ gọi gateway, double-checkout, chỉ bàn phím | [`walkthrough.spec.ts:78`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L78) · [`:153`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L153) · [`:182`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L182) · [`:217`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L217) | `cd frontend && STOREFRONT_URL=http://localhost:4173 GATEWAY_ORIGIN=http://localhost:5300 E2E_USERNAME=postman-test@local.test E2E_PASSWORD=<TestUserPassword> corepack pnpm --filter @ecommerce/web e2e` (stack đang chạy; cần shim `pnpm`, xem QA 004) |
| FR-001 — file Compose hợp lệ (không link test) |  | `docker compose config --quiet` |

**Kết quả lượt QA này (2026-09-27)**: `docker compose config --quiet` hợp lệ · `DockerfileSharedProjectTests` **9/9** (cả project `ContainerConventionTests`) · `StorefrontCorsTests` **10/10**. Walkthrough Playwright trên container không chạy lại ở lượt này (xem QA 004 — đã đo với `STOREFRONT_URL=http://localhost:4173`, 3/4 PASS).
## Kết luận

**PASS kèm ghi chú** — luồng cốt lõi của 005 chạy đúng trên máy thật: `./scripts/up.ps1` dựng 19 thành phần (13 `healthy`, 1 `Up`, 5 `Exited (0)`), `down` giải phóng cổng và giữ 3 volume (19 giây), `reset` xoá cả 3 volume (16 giây) rồi `up` lại đưa hệ thống về đúng trạng thái lần đầu (đơn cũ `404`, catalog 3 sản phẩm, giỏ rỗng), lần chạy sau khi cache ấm mất 164–170 giây (< 3 phút); chỉ 3 cổng được công bố (`4173`, `5300`, `5205`); 9/9 test container convention và 10/10 test CORS PASS.
Ghi chú cần xử lý: (1) `up.ps1` trên Windows PowerShell 5.1 vẫn không in được thông báo tiên quyết khi daemon không phản hồi (`NativeCommandError` thô); (2) Scenario 5 (thiếu dependency) như viết không tạo ra lỗi vì Compose tự khởi động lại thành phần đó; (3) tài liệu còn ghi 15 thành phần/2 cổng/2 volume trong khi thực tế 19/3/3; (4) SC-001 "dưới 10 phút" cho lần build lạnh hoàn toàn vẫn chưa đo được trong lượt này (chỉ đo được các lần cache ấm). Chi tiết: [QA_Debt.md](QA_Debt.md).
