# QA: Hạ tầng integration test bằng Testcontainers

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **Cả 4 service đã audit (baskets/orders/parties/products) vẫn kiểm thử bằng SQL Server thật**,
   không phải provider giả — đã tự chạy 4 test audit ràng buộc
   (`BasketConstraintsTests`/`OrderConstraintsTests`/`PartyConstraintsTests`/`ProductConstraintsTests`)
   và quan sát `docker ps` trong lúc chạy: container `mcr.microsoft.com/mssql/server` thật xuất hiện
   (FR-001, SC-001).
2. **Một vi phạm ràng buộc SQL thật bị bắt bởi test, không phải bởi code ứng dụng** — đã tự gỡ
   `.IsUnique()` khỏi index `CustomerRef` trong `BasketsDbContext.cs`, xác nhận
   `CustomerRef_Is_UniquePerBasket` chuyển đỏ, rồi khôi phục và xác nhận xanh lại (FR-002, SC-002).
3. **Fixture Redis dùng chung (`shared/IntegrationTestSupport/RedisFixture.cs`) khởi động container
   thật và đọc/ghi được** — đã tự chạy `RedisFixture_Roundtrips_ARealValue` (FR-003, FR-005).
4. **Fixture RabbitMQ dùng chung khởi động container thật, kết nối được, và khi broker "chết" giữa
   lúc test đang chạy thì test đó thất bại trong thời gian giới hạn thay vì treo vô hạn** — đã tự
   chạy cả `RabbitMqFixture_Connects_ToARealBroker` lẫn `RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest`,
   đo thời lượng thật (FR-004, FR-006, FR-008, SC-004).
5. **Container không khoẻ thì cả lượt chạy thất bại rõ ràng, nêu tên thành phần, không âm thầm bỏ
   qua** — đã tự phá 1 fixture (đổi tag image Redis thành tên không tồn tại) và đọc thông báo lỗi
   thật (FR-007, SC-003).
6. **Không có thay đổi hành vi production nào sống sót**: sau khi tự làm 2 lượt "gỡ rồi khôi phục",
   `git status`/`git diff` trên `services/*/src` và `shared/IntegrationTestSupport` sạch, và không
   có file production nào (ngoài `shared/IntegrationTestSupport/*.cs` chính nó) tham chiếu
   `RedisFixture`/`RabbitMqFixture` — đúng tuyên bố FR-009.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Không cần Postman — toàn bộ nội dung spec này là hạ tầng test, chạy thẳng bằng `dotnet test`. Cần
Docker Desktop đang chạy cho mọi bước (mỗi bước tự khởi động container thật). Đây gần như nguyên văn
`quickstart.md` của spec — chỉ khác ở chỗ đã **tự chạy thật** thay vì tin vào mô tả, và thay bước phá
container SQL Server (`docker network disconnect`, có tính phá hoại/khó đoán trên máy đang chạy nhiều
container khác) bằng cách phá fixture Redis (đổi tag image), an toàn hơn nhưng chứng minh đúng cùng 1
hành vi FR-007 vì cả 3 loại container dùng chung 1 cơ chế của thư viện Testcontainers (research.md
Decision 4).

### Thủ công — Scenario 1: container thật xuất hiện trong `docker ps` khi test đang chạy (SC-001)

```bash
dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter CustomerRef_Is_UniquePerBasket &
docker ps --format "table {{.Image}}\t{{.Status}}"
```

**Kết quả đo thật** (2026-09-22, Docker Desktop đang chạy): trong lúc test chạy, `docker ps` liệt kê
`mcr.microsoft.com/mssql/server:2022-latest` (container database thật của `SqlServerFixture`) và
`testcontainers/ryuk:0.14.0` (container dọn dẹp riêng của thư viện Testcontainers, tự thêm, không
phải code của feature này).

### Thủ công — Scenario 2: cố ý vi phạm 1 ràng buộc SQL thật (SC-002)

```bash
dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter CustomerRef_Is_UniquePerBasket
```

1. Đổi dòng `basket.HasIndex(entity => entity.CustomerRef).IsUnique();` trong
   `services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs` thành `basket.HasIndex(entity =>
   entity.CustomerRef);` (bỏ `.IsUnique()`).
2. Chạy lại đúng lệnh trên.

**Kết quả đo thật**: test **FAIL** — không còn ai chặn 2 basket cùng `CustomerRef`. Khôi phục bằng
`git checkout -- services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs`, chạy lại → **PASS**
(1/1), `git status` sạch. `quickstart.md` có kịch bản y hệt cho `orders`/`parties`/`products` (đổi
`HasMaxLength` từ 128/200 thành 500) — đã tự chạy baseline (không mutate) cho cả 3 và đều **PASS**,
không lặp lại thao tác mutate cho cả 3 vì cùng 1 khuôn với basket.

### Thủ công — Scenario 3: container không khoẻ thì thất bại rõ ràng, không skip (SC-003)

```bash
dotnet test shared/IntegrationTestSupport.Tests --filter RedisFixture_Roundtrips_ARealValue
```

1. Đổi `new RedisBuilder("redis:7.4")` trong `shared/IntegrationTestSupport/RedisFixture.cs` thành
   `new RedisBuilder("redis:qa-probe-nonexistent-tag")`.
2. Chạy lại đúng lệnh trên.

**Kết quả đo thật**: test **FAIL** (không phải skip) trong 117ms, thông báo lỗi nêu rõ:
`Docker API responded with status code='NotFound' ... "docker.io/library/redis:qa-probe-nonexistent-tag: not found"`
— xác nhận đúng FR-007 (lỗi nêu tên container/image cụ thể). Khôi phục bằng `git checkout --
shared/IntegrationTestSupport/RedisFixture.cs`, chạy lại → **PASS**.

### Thủ công — Scenario 4/5: fixture Redis/RabbitMQ dùng lại được, và RabbitMQ fail-fast khi broker chết giữa test (FR-003–006, FR-008, SC-004)

```bash
dotnet test shared/IntegrationTestSupport.Tests --filter RedisFixture_Roundtrips_ARealValue
dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_Connects_ToARealBroker
time dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest
```

**Kết quả đo thật**: cả 3 **PASS**. Lệnh `time` cho tổng thời gian tiến trình `dotnet test` là ~32s
(gồm khởi động runtime + dựng container RabbitMQ), nhưng **thời lượng của chính bài test** (cột
`Duration` trong output xUnit) chỉ **1 giây** — tức khoảng chờ có giới hạn (`Task.WhenAny` 30 giây)
không hề bị chạm tới, đóng SC-004 với biên độ rất rộng.

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002 — SQL Server thật bắt vi phạm ràng buộc (baskets) | [`BasketConstraintsTests.cs:23`](../../services/baskets/tests/Baskets.Api.IntegrationTests/BasketConstraintsTests.cs#L23) — `CustomerRef_Is_UniquePerBasket` | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter CustomerRef_Is_UniquePerBasket` |
| FR-001/FR-002 — SQL Server thật bắt vi phạm ràng buộc (orders) | [`OrderConstraintsTests.cs:23`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderConstraintsTests.cs#L23) — `TenantId_ExceedingMaxLength_IsRejectedByTheDatabase` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter TenantId_ExceedingMaxLength_IsRejectedByTheDatabase` |
| FR-001/FR-002 — SQL Server thật bắt vi phạm ràng buộc (parties) | [`PartyConstraintsTests.cs:23`](../../services/parties/tests/Parties.Api.IntegrationTests/PartyConstraintsTests.cs#L23) — `DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase` | `dotnet test services/parties/tests/Parties.Api.IntegrationTests --filter DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase` |
| FR-001/FR-002 — SQL Server thật bắt vi phạm ràng buộc (products) | [`ProductConstraintsTests.cs:23`](../../services/products/tests/Products.Api.IntegrationTests/ProductConstraintsTests.cs#L23) — `Name_ExceedingMaxLength_IsRejectedByTheDatabase` | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter Name_ExceedingMaxLength_IsRejectedByTheDatabase` |
| FR-003/FR-005 — fixture Redis dùng chung, đọc/ghi được | [`RedisFixtureTests.cs:20`](../../shared/IntegrationTestSupport.Tests/RedisFixtureTests.cs#L20) — `RedisFixture_Roundtrips_ARealValue` (fixture: [`RedisFixture.cs:13`](../../shared/IntegrationTestSupport/RedisFixture.cs#L13)) | `dotnet test shared/IntegrationTestSupport.Tests --filter RedisFixture_Roundtrips_ARealValue` |
| FR-004/FR-006 — fixture RabbitMQ dùng chung, kết nối được | [`RabbitMqFixtureTests.cs:22`](../../shared/IntegrationTestSupport.Tests/RabbitMqFixtureTests.cs#L22) — `RabbitMqFixture_Connects_ToARealBroker` (fixture: [`RabbitMqFixture.cs:13`](../../shared/IntegrationTestSupport/RabbitMqFixture.cs#L13)) | `dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_Connects_ToARealBroker` |
| FR-008/SC-004 — broker chết giữa test thì fail nhanh, không treo | [`RabbitMqFixtureTests.cs:43`](../../shared/IntegrationTestSupport.Tests/RabbitMqFixtureTests.cs#L43) — `RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest` | `dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest` |

Chạy cả project `IntegrationTestSupport.Tests` để xem tổng quan cả 2 fixture cùng lúc:

```bash
dotnet test shared/IntegrationTestSupport.Tests
```

## Kết luận

**PASS** — cả 3 nguồn (architecture/summary/spec-summary-vi) mô tả cùng 1 nội dung, không mâu thuẫn
nhau, và đã tự chạy lại thật (không suy diễn) toàn bộ 6 điều nêu ở "Luồng happy-case đã rà soát",
gồm 2 lượt cố ý phá vỡ thật (1 ràng buộc SQL, 1 image container) rồi khôi phục sạch. Tuyên bố FR-009
("không có thay đổi hành vi production nào") cũng đã xác minh bằng grep — chỉ chính
`shared/IntegrationTestSupport/*.cs` tham chiếu 2 fixture mới, không service production nào khác.

Không phát hiện sai lệch tài liệu nào cần ghi vào QA_Debt — mục "Giới hạn phạm vi đã biết" của
`architecture/010` (chưa gộp `SqlServerFixture.cs` 4 bản copy-paste, Redis/RabbitMQ chưa có tính năng
nghiệp vụ nào dùng) đã tự ghi rõ và vẫn đúng tại thời điểm rà soát; riêng phần RabbitMQ đã có Amendment
2026-09-12 ở `technical-debt.md` (mục 010) xác nhận đúng: `orders` nay publish thật qua MassTransit
(spec 024) nhưng dùng hạ tầng RabbitMQ production riêng (`docker-compose.yml`), không phải qua
`RabbitMqFixture` của spec này — 2 việc tách bạch, không mâu thuẫn tuyên bố FR-009.
