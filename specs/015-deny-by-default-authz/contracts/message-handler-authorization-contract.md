# Contract: Quyết định tin cậy tường minh cho message handler

Hợp đồng phòng ngừa (forward-looking guard), không phải hợp đồng cho hành vi đang chạy hôm nay: `services/` hiện không có bất kỳ kiểu nào implement `IConsumer<T>` (xác nhận bằng cách quét toàn bộ mã nguồn — research.md Decision 4). Hợp đồng này tồn tại để lần đầu tiên tính năng hướng sự kiện (constitution Principle IV) được xây, quyết định phân quyền cho handler không bị bỏ sót.

## Cam kết

| | |
|---|---|
| Áp dụng cho | Mọi kiểu implement `IConsumer<T>` (MassTransit) được thêm vào bất kỳ service nào trong `services/`. |
| Yêu cầu | Mỗi handler PHẢI khai báo tường minh nguồn phát hành sự kiện mà nó tin cậy — ví dụ một dòng doc-comment quy ước dạng `/// Trusted source: <tên service/exchange phát hành>` ngay trên khai báo lớp, hoặc một thuộc tính tương đương nếu team chọn hướng đó khi triển khai. |
| Không chấp nhận | Một `IConsumer<T>` xử lý message mà không có dòng khai báo trên — tương đương một endpoint HTTP không có `.RequireAuthorization()`/`.AllowAnonymous()`. |

## Producers

| Nguồn | Hành vi |
|---|---|
| Bất kỳ service nào thêm `IConsumer<T>` đầu tiên | Phải đi kèm khai báo nguồn tin cậy tường minh ngay khi handler được tạo, không phải bổ sung sau. |

## Consumers

| Hop | Hành vi |
|---|---|
| `tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScanner` (mở rộng, research.md Decision 3/4) | Quét toàn bộ `services/**/*.cs` tìm `IConsumer<`; với mỗi kết quả, xác nhận có khai báo nguồn tin cậy tường minh liền kề. Quét này pass (rỗng) hôm nay vì không có handler nào tồn tại — SC-001 vẫn đúng theo nghĩa "0 endpoint/handler nào bị thiếu quyết định", không phải "0 handler được kiểm tra". |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| Handler mới không có khai báo nguồn tin cậy | Scanner thất bại, chặn merge (FR-004) |
| Handler mới có khai báo nguồn tin cậy | Scanner pass; nội dung khai báo được review thủ công như một phần review PR bình thường (constitution: "Changes to... an authorization policy... require review from the owning service's maintainer") |

## Stability

Hợp đồng nội bộ. Không có breaking-change nào có thể xảy ra hôm nay vì không có handler nào để phá vỡ — hợp đồng này chỉ ràng buộc công việc tương lai, không ràng buộc mã hiện có.
