# Kiến trúc: Danh tính giả lập với tenant context đã xác định

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-12 ("[WALK-1] Stub identity with a resolved tenant context"), đặc tả tại
[`specs/003-stub-identity-tenant-context/`](../../specs/003-stub-identity-tenant-context/), xây trên
gateway/BFF của [002-gateway-bff-routing](../../specs/002-gateway-bff-routing/). Đây là feature mà
toàn bộ cơ chế lan truyền tenant/subject của
[014-identity-server-auth](../../specs/014-identity-server-auth/) sau này TÁI SỬ DỤNG nguyên vẹn,
không sửa — bằng chứng trực tiếp cho constitution Principle V.

**Trạng thái xác minh**: 39/39 task trong `tasks.md` đã hoàn thành. Khối **"T039 results"** cuối
`tasks.md` ghi nhận lượt chạy thật trên full local stack (4 SQL container, 4 domain service, BFF,
gateway) cho 5 scenario của `quickstart.md` — tất cả PASS, kèm 2 phát hiện thật đã sửa (mục 4).

## 1. Kiến trúc tổng thể

```
Client ──▶ Gateway (StubIdentityAuthenticationHandler)  ──▶  BFF  ──▶  Products/Baskets/Orders/Parties
            xác định tenant MỘT LẦN, gán X-Tenant-Id       forward       AddDbContext GATED trên
            (thay cho token thật — chưa có identity server) header       TenantContext đã resolve
```

Nguyên lý: tenant được xác định đúng **một lần** tại gateway (từ danh tính giả lập, chưa phải token
thật), rồi lan truyền qua header `X-Tenant-Id` không thể chỉnh sửa bởi client (research.md Decision
2) — không service nào tự suy luận lại tenant. Ở đầu bên kia, mọi truy cập persistence bị **chặn cấu
trúc** nếu tenant chưa được resolve (spec FR-004/FR-005).

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| Quyết định | Tóm tắt |
|---|---|
| 1 | Stub identity là một `AuthenticationHandler` thật (dù giả), không phải một shortcut tự stamp header thủ công |
| 2 | Gateway stamp `X-Tenant-Id` lên chính request, giống hệt cách `CorrelationIdMiddleware` đã làm trước đó |
| 3 | Thư viện chia sẻ mới `shared/Tenancy`, không phải 4 bản copy middleware giống nhau ở 4 service |
| 4 | Một `DelegatingHandler` ở downstream client của BFF đóng một lỗ hổng lan truyền — trước khi nó thực sự xảy ra |
| 5 | Schema-per-tenant trên cùng database mỗi service đã có, không phải database-per-tenant |
| 6 | Persistence bị gate ngay tại điểm gọi `AddDbContext` của mỗi service, không phải ở từng method repository riêng lẻ |
| 7 | Helper seed dữ liệu test có sẵn phải set `TenantContext` trực tiếp, vì chúng không chạy qua middleware |
| 8 | Không cần sửa cách `ConnectionStringScanner` đọc hình dạng connection string |

Quyết định 6 (gate tại `AddDbContext`, không phải từng repository method) là điểm cấu trúc quan trọng
nhất: nó biến "yêu cầu tenant" từ một kỷ luật code review thành một điều kiện tiên quyết để DbContext
thậm chí được tạo ra — không có method nào có thể "quên" gate vì không có cách nào lấy được DbContext
khi chưa có tenant.

## 3. Cơ chế thực thi — không có tenant mặc định

`data-model.md`/`contracts/` mô tả `TenantContext` có hai trạng thái: **Resolved** (có tenant hợp lệ)
và **Unresolved** (không có/không hợp lệ). Không có trạng thái thứ ba "tenant mặc định". Mọi service
gọi `AddDbContext` với một factory kiểm tra `TenantContext.RequireTenantId()` — nếu Unresolved, ném
exception TRƯỚC khi kết nối database được mở, không phải sau. Đây chính là hành vi
`Tenancy.MissingTenantContextException` mà [014's T047 verification run](../../specs/014-identity-server-auth/tasks.md)
sau này vẫn quan sát thấy nguyên vẹn khi gọi domain service trực tiếp không qua gateway — xác nhận cơ
chế không bị suy yếu qua nhiều feature sau.

## 4. Hai phát hiện thật khi chạy thử toàn luồng (T039 results)

Trích nguyên văn từ `tasks.md`:

> Two things the run surfaced and fixed: `dotnet ef` could no longer discover a `DbContext` (design-time
> discovery resolves it through DI and hit the gate), so each domain service gained an
> `IDesignTimeDbContextFactory`; and the local run needs `ASPNETCORE_ENVIRONMENT=Development`, without
> which the Development connection strings never load.

Cả hai đều là hệ quả trực tiếp của cùng một nguyên lý cấu trúc ở mục 3: gate chặt tới mức công cụ
`dotnet ef` (chạy ngoài luồng request thật, không có `TenantContext` nào để resolve) cũng bị chặn
theo — buộc mỗi service phải có một `IDesignTimeDbContextFactory` cung cấp đường tắt hợp lệ riêng cho
tình huống design-time, tách biệt khỏi luồng request thật.

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/003-stub-identity-tenant-context-component.drawio`](../diagrams/003-stub-identity-tenant-context-component.drawio)
- Sơ đồ trình tự (resolve tenant một lần → lan truyền header → gate tại `AddDbContext`, gồm nhánh
  thiếu tenant): [`docs/diagrams/003-stub-identity-tenant-context-sequence.drawio`](../diagrams/003-stub-identity-tenant-context-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/003-stub-identity-tenant-context-flow-nghiep-vu.drawio`](../diagrams/003-stub-identity-tenant-context-flow-nghiep-vu.drawio)

Sơ đồ tổng thể 3 nhóm kiến trúc của toàn nền tảng (bao gồm tenancy) xem
[`docs/diagrams/kien-truc-3-nhom.drawio`](../diagrams/kien-truc-3-nhom.drawio).
