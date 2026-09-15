# Bước 025: Thay đổi nghiệp vụ so với bước 024

## Phạm vi

Tài liệu này mô tả phần code được tạo bởi bước 025 (SCRUM-34, `specs/025-chaos-pod-kill-latency/`) so
với trạng thái code sau bước 024. US1 (kill-pod) và US3 (bản ghi kết quả) không thêm mã ứng dụng nào —
chỉ US2 (inject-latency) chạm `services/orders/`.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 024: commit `3c290bf`.
- Đặc tả: commit `e616520` (spec), `db85704` (plan), `76147b8` (tasks).
- Triển khai middleware tiêm độ trễ: commit `286173e`.
- Chạy bài tập chaos thật, đóng `tasks.md`: commit `2b8705d`.
- Chạy lại US1/US2 trên Pod Kubernetes thật: commit `aaea036`.
- Đóng T015/T016 với 1 lượt quickstart liền mạch: commit `6750d2d` (mốc hoàn tất bước 025).

## 1. Middleware tiêm độ trễ — mới hoàn toàn, chỉ trên Orders.Api

[services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs](../../services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs)
(mới):

```csharp
public sealed class ChaosOptions
{
    public const string SectionName = "Chaos";

    // 025: mặc định false — an toàn. Không dùng khối FeatureToggles có sẵn (dùng cho rollout có ngày
    // gỡ) vì đây là công cụ vận hành thường trực, không có ngày gỡ.
    public bool AllowLatencyInjection { get; set; }

    // 025: chặn 1 sai sót thao tác biến bài tập có kiểm soát thành treo vô hạn.
    public const int MaxInjectedLatencyMs = 30_000;
}
```

[services/orders/src/Orders.Api/Features/Chaos/ChaosLatencyInjectionMiddleware.cs](../../services/orders/src/Orders.Api/Features/Chaos/ChaosLatencyInjectionMiddleware.cs)
(mới):

```csharp
public sealed class ChaosLatencyInjectionMiddleware
{
    public const string LatencyHeaderName = "X-Chaos-Latency-Ms";

    public async Task InvokeAsync(HttpContext context, IOptions<ChaosOptions> options)
    {
        // 025: 2 lớp gate — cấu hình môi trường (AllowLatencyInjection) VÀ header per-request. Tách
        // riêng nghĩa là bắt đầu/dừng tiêm giữa chừng chỉ là gửi/không gửi header, không cần restart
        // pod (constitution Principle X).
        if (options.Value.AllowLatencyInjection && TryGetRequestedDelay(context, out var duration))
        {
            await _delay.DelayAsync(duration, context.RequestAborted);
        }

        // 025: luôn chạy, request/response không đổi ở CẢ HAI nhánh — middleware này chỉ đổi KHI
        // pipeline còn lại chạy, không bao giờ đổi NÓ TRẢ VỀ GÌ (Bất biến 6 của hợp đồng).
        await _next(context);
    }
}
```

`IChaosDelay`/`SystemChaosDelay` (mới, cùng thư mục) là seam để unit test đo khoảng trễ được yêu cầu
mà không thực sự chờ (research.md Quyết định 4) — không nằm trong kế hoạch ban đầu, thêm khi viết test
(T005 phát hiện cần).

[services/orders/src/Orders.Api/Program.cs](../../services/orders/src/Orders.Api/Program.cs):

```csharp
// 025: đăng ký ChaosOptions + middleware, đặt NGAY SAU UseServiceDefaults(), TRƯỚC
// UseIdentityValidation() (data-model.md mục 1, Bất biến 5 — độ trễ áp dụng trước khi request chạm
// pipeline xác thực/tenant phía sau).
builder.Services.Configure<ChaosOptions>(builder.Configuration.GetSection("Chaos"));
builder.Services.AddSingleton<IChaosDelay, SystemChaosDelay>();
// ...
app.UseMiddleware<ChaosLatencyInjectionMiddleware>();
```

`appsettings.json` thêm khối `"Chaos": { "AllowLatencyInjection": false }` — mặc định tắt ở mọi cấu
hình đã commit (spec FR-006: không bao giờ bật ở production).

## 2. US1 (kill-pod) và US3 (bản ghi kết quả) — không có mã ứng dụng

US1 hoàn toàn là lệnh vận hành (`kubectl delete pod -l app=baskets`) + quan sát telemetry `"Polly"`
(020) đã có sẵn — không sửa `services/baskets` hay BFF. US3 là tài liệu markdown mới ở
`docs/dien-tap-chaos-engineering/` (`mau-ket-qua.md`, `README.md`, `ket-qua/*.md`) — không phải mã
nghiệp vụ, xem chi tiết ở
[025_Architect_*.md](../architecture/025_Architect_diễn%20tập%20chaos%20engineering%20giết%20pod%20tiêm%20độ%20trễ.md)
và [technical-debt.md](../architecture/technical-debt.md) cho kết quả thật (circuit breaker chưa từng
trip).

## Tóm tắt 024 → 025

| Khu vực | Bước 024 | Bước 025 |
|---|---|---|
| `Orders.Api/Features/` | `Orders/` (OrderEndpoints), không có `Chaos/` | + `Chaos/` (`ChaosOptions`, `IChaosDelay`, `ChaosLatencyInjectionMiddleware`) |
| `appsettings.json` (orders) | Không có khối `Chaos` | + `"Chaos": { "AllowLatencyInjection": false }` |
| `services/baskets`, BFF | Không đổi | Không đổi (US1 thuần vận hành) |
| `docs/dien-tap-chaos-engineering/` | Không tồn tại | Thư mục mới — mẫu, README, 3 bản ghi kết quả thật |

**Kết luận:** bước 025 thêm đúng 1 middleware tối thiểu (tiêm độ trễ có kiểm soát, mặc định tắt) và 1
thư mục tài liệu vận hành mới. Không đổi hành vi phản hồi của bất kỳ endpoint nào khi
`AllowLatencyInjection=false` (trạng thái mặc định của mọi cấu hình đã commit).

## Shared project trong bước 025

Không tạo, không sửa shared project nào. `Features/Chaos/` rơi thẳng vào project `Orders.Api.csproj`
đã tồn tại; test mới rơi vào `Orders.Api.UnitTests.csproj` đã tồn tại — không có `PackageReference`
mới nào được thêm (middleware chỉ dùng API đã có sẵn trong `Microsoft.AspNetCore.Http`/
`Microsoft.Extensions.Options`).
