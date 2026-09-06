# Contract: Khả năng truy cập Correlation ID từ phía SPA

Hợp đồng giữa gateway (nguồn header) và `@ecommerce/api-client` (`frontend/packages/api-client`) — điểm gọi API duy nhất của toàn SPA (constitution Principle IX). Hiện thực hoá spec US2 (spec.md).

## Cam kết

| | |
|---|---|
| Header nguồn | `X-Correlation-Id` trên phản hồi HTTP của gateway (đã tồn tại — xem `contracts/correlation-id-header-contract.md`) |
| Điều kiện để JS đọc được | Gateway CORS policy (`services/gateway/src/Gateway.Api/Program.cs`, policy `StorefrontCorsPolicy`) PHẢI khai báo header này trong `.WithExposedHeaders(CorrelationIdMiddleware.HeaderName)` — mặc định CORS chỉ cho JS đọc các header "safelisted" (`Cache-Control`, `Content-Language`, `Content-Type`, `Expires`, `Last-Modified`, `Pragma`), không gồm header tuỳ biến |
| Nơi mang giá trị ở SPA | Trường mới `correlationId` trên `ApiError` (`frontend/packages/api-client/src/http/fetcher.ts`) |
| Không phụ thuộc | Việc trình duyệt hiển thị header trong tab mạng (Network) của DevTools — điều này KHÔNG phụ thuộc CORS, đã đúng từ trước (research.md Decision 5) |

## Producers

| Nguồn | Hành vi |
|---|---|
| Gateway (`CorrelationIdMiddleware`) | Echo `X-Correlation-Id` trên mọi phản hồi (đã có, không đổi) |
| Gateway CORS policy (mới) | Khai báo `X-Correlation-Id` trong `WithExposedHeaders(...)`, cho phép mã JS chạy trên origin của storefront đọc được header này qua `fetch()` |

## Consumers

| Nơi | Hành vi |
|---|---|
| `bffFetch<TResponse>(...)` (`fetcher.ts`) | Đọc `response.headers.get(CorrelationIdMiddleware.HeaderName)` trên mọi phản hồi, cả thành công lẫn lỗi |
| `ApiError` (khi phản hồi không phải 2xx) | Mang thêm trường `readonly correlationId: string \| null` — `null` khi header vắng mặt (ví dụ lỗi mạng xảy ra trước khi có phản hồi) |
| Mã màn hình gọi qua các hook do Orval sinh ra | Khi bắt `isError`, có thể đọc `error.correlationId` nếu cần hiển thị/đính kèm vào báo cáo lỗi trong tương lai — tính năng này KHÔNG bắt buộc `ErrorState`/màn hình nào phải hiển thị giá trị này cho người dùng cuối (ngoài phạm vi Jira AC3, vốn chỉ yêu cầu quan sát được qua tab mạng) |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| CORS chưa expose header (trước khi sửa) | DevTools Network tab vẫn hiển thị đầy đủ header (không đổi); nhưng `response.headers.get(...)` từ mã JS trả về `null` — SPA không thể dùng giá trị này theo chương trình |
| Lỗi mạng xảy ra trước khi có phản hồi (server không phản hồi, mất kết nối) | Không có `Response` object nào để đọc header — `ApiError`/kết quả không có `correlationId` (giới hạn cố hữu, không phải lỗi cần khắc phục thêm) |
| Phản hồi 2xx (thành công) | `bffFetch` vẫn đọc được header nếu cần dùng, nhưng không bắt buộc phải lộ ra ngoài qua kiểu trả về hiện có của Orval (`{ data, status }`) — chỉ đường lỗi (`ApiError`) mở rộng kiểu dữ liệu trong phạm vi tính năng này, vì đó là nơi correlation ID thực sự hữu ích (spec US2 AC2: khoá tra cứu khi có lỗi) |

## Trước và sau tính năng này

| | Trước | Sau |
|---|---|---|
| Hiển thị trong DevTools Network tab | ✅ Đã đúng (không phụ thuộc CORS) | Không đổi |
| Mã JS của SPA đọc được `X-Correlation-Id` qua `fetch()` | ❌ Bị chặn bởi CORS mặc định | ✅ |
| `ApiError` mang correlation ID của request thất bại | ❌ Không có trường này | ✅ `ApiError.correlationId` |

## Stability

Hợp đồng nội bộ giữa gateway và package `@ecommerce/api-client` trong cùng monorepo, không phải một API công khai bên ngoài nền tảng. `ApiError` là kiểu nội bộ của package này (không do Orval sinh ra), nên mở rộng thêm trường là thay đổi tương thích ngược, không cần versioning theo Principle II.
