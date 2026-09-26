# QA: Hạ tầng integration test bằng Testcontainers

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **4 service có DB (baskets/orders/parties/products) kiểm thử bằng SQL Server thật**, không phải provider giả — 4 test audit ràng buộc (`*ConstraintsTests`) chạy trên container `mcr.microsoft.com/mssql/server` thật (FR-001, SC-001).
2. **Vi phạm ràng buộc SQL thật bị bắt bởi test**, không phải bởi mã ứng dụng: gỡ `.IsUnique()` khỏi index `CustomerRef` thì `CustomerRef_Is_UniquePerBasket` đỏ (FR-002, SC-002).
3. **Fixture Redis/RabbitMQ dùng chung** (`shared/IntegrationTestSupport`) khởi động container thật, đọc/ghi và kết nối được; khi broker "chết" giữa test thì test thất bại trong thời gian giới hạn (FR-003–006, FR-008, SC-004).
4. **Container không khoẻ thì cả lượt chạy thất bại rõ ràng, nêu tên image**, không âm thầm bỏ qua (FR-007, SC-003).
5. **Không có thay đổi hành vi production**: chỉ chính `shared/IntegrationTestSupport/*.cs` tham chiếu 2 fixture mới (FR-009).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — quan sát ràng buộc của SQL Server thật qua Postman

Spec 010 là hạ tầng test nên **không có công tắc cấu hình**. Điều quan sát được trên hệ thống chạy thật là chính điều spec muốn chứng minh: ràng buộc cột do **SQL Server thật** thực thi, không chỉ mã ứng dụng.
Dựng stack rồi bấm Postman:

```bash
docker compose -f docker-compose.local.yml up -d --wait orders-api baskets-api   # kéo theo identity-api và DB tương ứng
```

Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền → 01 Lấy access token` một lần, rồi folder
**`10 - Ràng buộc CSDL thật (SQL Server)`** (bước 01 → 05; bước 01 và 03 tạo dữ liệu, bước 05 dọn giỏ).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| Container thật trong stack | (không có công tắc) — image do `docker-compose.local.yml` khai (`orders-db`, `baskets-db`, …); xem trong Docker Desktop → Containers | (không có) | Mỗi service có SQL Server thật riêng | Đúng: 3 container `mcr.microsoft.com/mssql/server:2022-latest` (`orders-db`, `baskets-db`, `identity-db`) |
| FR-001/002 — ràng buộc `Orders.TenantId` `nvarchar(128)` | (không có công tắc) | `10` bước 01 (128 ký tự) rồi bước 02 (129 ký tự) | 128 ghi được; 129 bị SQL Server từ chối | Bước 01 `201`, `tenantId` dài 128; bước 02 **`500`** (lỗi CSDL `String or binary data would be truncated`), bảng `Orders` **không có** hàng nào dài > 128. Test đỏ/xanh đúng kỳ vọng nhưng API trả `500` chứ không phải `4xx` (xem QA_Debt) |
| FR-001/002 — ràng buộc `Baskets.CustomerRef` `nvarchar(200)` | (không có công tắc) | `10` bước 03 (200 ký tự) rồi bước 04 (201 ký tự) | 200 ghi được; 201 bị từ chối | Bước 03 `200`, `customerRef` dài 200; bước 04 **`500`**, không có hàng `Baskets` nào dài > 200 |
| Dọn dẹp | Trả compose/`.env` về mặc định (không đổi gì) | `10` bước 05 | Không dữ liệu dư | Đã xoá đơn (giữ đơn gốc QA 017) và giỏ do QA tạo |
| Scenario 1 — container test xuất hiện lúc test chạy (SC-001) *(ngoại lệ: hạ tầng test, cần chạy test)* | (không có) — chạy `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter CustomerRef_Is_UniquePerBasket` và xem `docker ps` | (không có) | Thấy container SQL Server + Ryuk của Testcontainers | Sau ~8 giây `docker ps` thêm `testcontainers/ryuk:0.14.0` và 1 container `mssql/server:2022-latest` tên ngẫu nhiên (`exciting_goodall`) bên cạnh 3 DB của stack; test PASS 1/1 |
| Scenario 2 — cố ý vi phạm ràng buộc SQL thật (SC-002) *(ngoại lệ: sửa mã)* | Bỏ `.IsUnique()` khỏi `basket.HasIndex(entity => entity.CustomerRef)` trong `BasketsDbContext.cs`; xong `git checkout --` | (không có) | Test đỏ; khôi phục thì xanh | Đúng: `CustomerRef_Is_UniquePerBasket` **FAIL**; khôi phục → **PASS**; `git status` sạch |
| Scenario 3 — container không khoẻ thì thất bại rõ ràng (SC-003) *(ngoại lệ: sửa mã)* | Đổi `new RedisBuilder("redis:7.4")` thành `redis:qa-probe-nonexistent-tag` trong `RedisFixture.cs`; xong `git checkout --` | (không có) | Test đỏ (không phải skip), nêu tên image | Đúng: **FAIL** trong 1 ms với `Docker API responded with status code='NotFound' … docker.io/library/redis:qa-probe-nonexistent-tag: not found`; khôi phục → **PASS** |
| Scenario 4/5 — fixture Redis/RabbitMQ, broker chết giữa test (FR-003–006, FR-008, SC-004) *(ngoại lệ: hạ tầng test)* | (không có) — `dotnet test shared/IntegrationTestSupport.Tests` | (không có) | 3/3 PASS, fail-fast không treo | 3/3 PASS: Redis 261 ms, RabbitMQ kết nối 141 ms, `RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest` **1 giây** (biên giới hạn 30 giây không bị chạm) |
| FR-009 — không code production dùng 2 fixture | (không có) — tìm `RedisFixture`/`RabbitMqFixture` trong `services/**/src` | (không có) | Không có | Đúng: 0 file |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Cần Docker Desktop đang chạy (mỗi test tự khởi động container thật).

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002 — SQL Server thật bắt vi phạm ràng buộc (baskets) | [`BasketConstraintsTests.cs:23`](../../services/baskets/tests/Baskets.Api.IntegrationTests/BasketConstraintsTests.cs#L23) — `CustomerRef_Is_UniquePerBasket` | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter CustomerRef_Is_UniquePerBasket` |
| FR-001/FR-002 — (orders) | [`OrderConstraintsTests.cs:23`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderConstraintsTests.cs#L23) — `TenantId_ExceedingMaxLength_IsRejectedByTheDatabase` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter TenantId_ExceedingMaxLength_IsRejectedByTheDatabase` |
| FR-001/FR-002 — (parties) | [`PartyConstraintsTests.cs:23`](../../services/parties/tests/Parties.Api.IntegrationTests/PartyConstraintsTests.cs#L23) — `DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase` | `dotnet test services/parties/tests/Parties.Api.IntegrationTests --filter DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase` |
| FR-001/FR-002 — (products) | [`ProductConstraintsTests.cs:23`](../../services/products/tests/Products.Api.IntegrationTests/ProductConstraintsTests.cs#L23) — `Name_ExceedingMaxLength_IsRejectedByTheDatabase` | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter Name_ExceedingMaxLength_IsRejectedByTheDatabase` |
| FR-003/FR-005 — fixture Redis dùng chung, đọc/ghi được | [`RedisFixtureTests.cs:20`](../../shared/IntegrationTestSupport.Tests/RedisFixtureTests.cs#L20) — `RedisFixture_Roundtrips_ARealValue` (fixture: [`RedisFixture.cs:13`](../../shared/IntegrationTestSupport/RedisFixture.cs#L13)) | `dotnet test shared/IntegrationTestSupport.Tests --filter RedisFixture_Roundtrips_ARealValue` |
| FR-004/FR-006 — fixture RabbitMQ dùng chung, kết nối được | [`RabbitMqFixtureTests.cs:22`](../../shared/IntegrationTestSupport.Tests/RabbitMqFixtureTests.cs#L22) — `RabbitMqFixture_Connects_ToARealBroker` (fixture: [`RabbitMqFixture.cs:13`](../../shared/IntegrationTestSupport/RabbitMqFixture.cs#L13)) | `dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_Connects_ToARealBroker` |
| FR-008/SC-004 — broker chết giữa test thì fail nhanh, không treo | [`RabbitMqFixtureTests.cs:43`](../../shared/IntegrationTestSupport.Tests/RabbitMqFixtureTests.cs#L43) — `RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest` | `dotnet test shared/IntegrationTestSupport.Tests --filter RabbitMqFixture_FailsFast_WhenBrokerDiesMidTest` |

**Kết quả lượt QA này (2026-09-26)**: 4 test ràng buộc **PASS** (mỗi cái 1/1, 3–4 giây), 3 test fixture **PASS 3/3**; sau 2 lượt cố ý phá (gỡ `.IsUnique()`, tag Redis sai) khôi phục xanh và `git status` sạch.

## Kết luận

**PASS kèm ghi chú nhỏ.** Ràng buộc cột do SQL Server thật thực thi (biên 128/129 và 200/201 ký tự đúng như kỳ vọng), 4 test ràng buộc + 3 test fixture xanh, 2 lượt cố ý phá đều làm test đỏ đúng cách rồi khôi phục sạch, không code production nào dùng 2 fixture mới. Ghi chú: (1) vi phạm ràng buộc lộ ra dưới dạng `500` vì API chưa kiểm độ dài đầu vào; (2) `docker ps` trên máy đang chạy stack có sẵn nhiều container SQL Server nên phải nhận container test qua tên ngẫu nhiên. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 010.
