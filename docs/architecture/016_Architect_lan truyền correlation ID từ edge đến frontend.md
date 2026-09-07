# Kiến trúc: Lan truyền Correlation ID từ Edge đến Frontend

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-26 ("[SECURE-3] Correlation ID propagation edge-to-frontend"), đặc tả tại
[`specs/016-correlation-id-propagation/`](../../specs/016-correlation-id-propagation/), vá 1 khoảng
trống thật trong cơ chế `CorrelationIdMiddleware` đã có từ `001-scaffold-service-shells` (mục 2).
Bảy quyết định kiến trúc: [`research.md`](../../specs/016-correlation-id-propagation/research.md).

**Trạng thái xác minh**: 12 task `[X]` trong `tasks.md`. Không chạy được `dotnet test Ecommerce.slnx`
một lệnh trong sandbox không có Docker của phiên triển khai — các bước xác minh chạy tách rời theo
từng phần (unit test không cần Docker, phần cần Docker xác nhận riêng khi có môi trường đầy đủ, xem
mục 5).

## 1. Kiến trúc tổng thể

```
SPA (trình duyệt)
   │ request (có/chưa có X-Correlation-Id)
   ▼
Gateway.Api — CorrelationIdMiddleware (shared/ServiceDefaults)
   │ chưa có/không hợp lệ → sinh Guid mới; có sẵn & hợp lệ → giữ nguyên
   │ ghi NGƯỢC vào request.Headers (không chỉ response) — để YARP copy đi tiếp
   ▼
Bff.Api — CorrelationIdMiddleware (dùng chung, giống hệt Gateway)
   │ TenantPropagationHandler đọc HttpContext.Items[...], relay tường minh
   │ (KHOẢNG TRỐNG ĐÃ VÁ — mục 2, trước 016 hop này làm rơi mất ID)
   ▼
Baskets/Orders/Parties/Products.Api — CorrelationIdMiddleware (dùng chung)
   │ ghi vào Activity (OTel span) + mọi structured log qua logging scope
   ▼
RabbitMQ / OrderPlaced event consumer (bất đồng bộ) — ID mang theo trong payload
```

Nguyên lý xuyên suốt (constitution Principle VII): correlation ID sinh một lần "ở edge" và lan truyền
nguyên vẹn qua MỌI lời gọi đồng bộ lẫn bất đồng bộ — không hop nào được phép tự sinh ID riêng.

## 2. Khoảng trống thật đã vá (research.md Decision 1)

Trước tính năng này, `CorrelationIdMiddleware` (có từ `001`) đã tồn tại **độc lập ở từng service** —
nhưng `TenantPropagationHandler` (BFF → domain service, có từ `009`/`014`, relay Tenant/Subject/
Authorization) **chưa relay correlation ID**. Hệ quả: mọi domain service chỉ gọi được qua BFF (tức là
toàn bộ `baskets`/`orders`/`parties`/`products` khi đi qua đường thật của storefront) **tự sinh ID
riêng của chính nó** — log của Gateway/BFF và log của 4 domain service không hề nối được với nhau
trước `016`, dù cơ chế ghi log kèm ID đã có sẵn ở từng service riêng lẻ từ lâu. Bản vá: thêm đúng 1
dòng `Relay(request, CorrelationIdMiddleware.HeaderName, httpContext?.Items[...] as string)` vào
`TenantPropagationHandler.SendAsync(...)`.

## 3. Mô tả từng thành phần

### 3.1. `CorrelationIdMiddleware` (mở rộng, `shared/ServiceDefaults`)

- `ResolveCorrelationId(...)` giờ **validate** giá trị client tự cung cấp (research.md Decision 2) —
  không ép định dạng GUID (client có thể mang bất kỳ token mờ nào), chỉ loại 2 thứ thực sự nguy hiểm
  khi giá trị này bị đẩy thẳng vào mọi dòng log có cấu trúc: ký tự điều khiển (kể cả CRLF — có thể giả
  mạo thêm 1 dòng log) và độ dài vượt 128 ký tự (chặn phình log từ 1 giá trị ác ý/lỗi). Giá trị không
  hợp lệ bị bỏ qua hoàn toàn, thay bằng 1 Guid mới do hệ thống sinh — không cố "làm sạch" giá trị cũ.
- `IsValidCorrelationId` là hàm thuần, không phụ thuộc I/O — dễ unit test độc lập, không cần
  `HttpContext` giả lập.

### 3.2. `TenantPropagationHandler` (vá, `services/bff/.../DownstreamClients/`)

Đọc đúng nguồn `HttpContext.Items[CorrelationIdMiddleware.HeaderName]` — **không** đọc lại từ
`context.Request.Headers` — vì đây chính là giá trị `CorrelationIdMiddleware` của CHÍNH request BFF
đang xử lý đã quyết định (sinh mới hoặc giữ nguyên), cùng nguồn `Bff.Api.ErrorHandling.
DownstreamExceptionHandler` đọc để đưa correlation ID vào thân `ProblemDetails` khi báo lỗi downstream
(đã có từ `009`, không đổi bởi `016`).

### 3.3. Async/RabbitMQ (research.md Decision 3)

**Không xây publisher/consumer mới** cho tính năng này — ranh giới rõ ràng với SCRUM-31 (outbox/
publisher thật, vẫn chưa tồn tại, xem [11-trien-khai-k8s-va-secret-store.md](../onboarding/11-trien-khai-k8s-va-secret-store.md)
mục về `BasketCheckedOutMapper`). Việc "correlation ID đi theo message bất đồng bộ" hiện được chứng
minh ở đúng mức tồn tại thật của hạ tầng messaging hôm nay — payload sự kiện mang theo ID, sẵn sàng
cho khi publisher thật được nối.

### 3.4. Hiển thị cho SPA (research.md Decision 5)

Network tab của trình duyệt vốn đã hiển thị mọi response header — không cần thay đổi gì để **xem
được**. Vấn đề thật là **đọc bằng JavaScript**: CORS mặc định không cho phép script phía client đọc
response header ngoài 1 danh sách "safelisted" nhỏ. Vá bằng
`policy.WithExposedHeaders(CorrelationIdMiddleware.HeaderName)` trong `AddCors(...)` của
`Gateway.Api/Program.cs`.

## 4. Kiểm chứng "không lẫn lộn khi đồng thời" (research.md Decision 7, US3)

`TenantPropagationHandler` đọc `HttpContext.Items` **tươi mỗi lần `SendAsync`**, không capture ở
constructor — đây là hệ quả tất yếu của cách handler này đã được viết (không phải hành vi mới của
`016`). Vì vậy test cho US3 **không theo chu trình fail-trước-rồi-sửa** như các phần khác — nó được kỳ
vọng PASS ngay khi viết, đóng vai trò bằng chứng thực nghiệm xác nhận thiết kế cũ đã đúng, không phải
một đặc tả hành vi mới. Đây là 1 điểm đáng nhớ khi đọc `tasks.md`: không phải mọi task trong dự án đều
đi theo TDD đỏ-trước — 1 số task chỉ xác nhận lại 1 bất biến đã có.

## 5. Kết quả thực tế lúc triển khai (trích tasks.md, không suy đoán)

> **Kết quả thực tế — baseline KHÔNG xanh, vì 2 lý do ngoài phạm vi SCRUM-26** (ghi nhận ở đầu
> `tasks.md`): môi trường triển khai lúc đó là sandbox không có Docker daemon, nên các bài test cần
> Testcontainers/Docker không chạy được trong phiên đó — không phải hồi quy của `016`.

Do giới hạn môi trường trên, phần lớn xác minh của `016` dựa vào: (a) unit test không cần Docker cho
`CorrelationIdMiddleware`/`IsValidCorrelationId` chạy độc lập, và (b) rà soát mã nguồn xác nhận đúng 1
dòng relay đã thêm vào `TenantPropagationHandler`. Khác với `014`/`015` (có môi trường Docker đầy đủ
để chạy `docker-compose.local.yml` thật), `016` **không** có 1 lượt chạy end-to-end thật trên stack
đầy đủ trong chính phiên triển khai của nó — bằng chứng end-to-end thật cho luồng correlation ID
(nối với Elastic) chỉ xuất hiện đầy đủ ở tính năng kế tiếp, `017` (mục 5 của
[017_Architect_*.md](017_Architect_phát%20telemetry%20OTel%20qua%20ServiceDefaults%20tới%20Elastic.md)).

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/016-correlation-id-propagation-component.drawio`](../diagrams/016-correlation-id-propagation-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/016-correlation-id-propagation-flow-nghiep-vu.drawio`](../diagrams/016-correlation-id-propagation-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/016-correlation-id-propagation-sequence.drawio`](../diagrams/016-correlation-id-propagation-sequence.drawio)

## 7. Tham khảo thêm

Cơ chế `CorrelationIdMiddleware`/`TenantPropagationHandler` gốc (trước bản vá này) đã giải thích chi
tiết ở [03-giai-doan-1-nen-tang-dich-vu-va-routing.md](../onboarding/03-giai-doan-1-nen-tang-dich-vu-va-routing.md)
và [09-bff-dependency-downstream-va-trien-khai.md](../onboarding/09-bff-dependency-downstream-va-trien-khai.md)
(đã cập nhật khớp bản vá `016`) — không lặp lại chi tiết cơ chế nền ở đây.
