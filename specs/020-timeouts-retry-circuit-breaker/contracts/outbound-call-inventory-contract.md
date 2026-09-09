# Hợp đồng: Danh sách kiểm kê điểm gọi ra ngoài (Outbound Call Inventory)

Danh sách tường minh mà `tests/ResilienceCoverageTests` dùng làm nguồn sự thật (research.md Decision
7; data-model.md "Outbound Call Inventory Entry"). Đây là hợp đồng giữa mã nguồn thực tế và test quét
— khi một trong hai lệch khỏi cái còn lại, build PHẢI đỏ.

## Danh sách tại thời điểm tính năng này hoàn tất

| Name | Caller → Callee | ConfigurationFile | RequiredMarkers |
|---|---|---|---|
| `bff-cluster` | gateway → bff | `services/gateway/src/Gateway.Api/appsettings.json` | `"ActivityTimeout"`, `"HealthCheck"`, `"Passive"`, `"AvailableDestinationsPolicy"` |
| `ProductsApi` | bff → products | `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` | `"AddStandardResilienceHandler"` |
| `BasketsApi` | bff → baskets | `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` | `"SafeToRetryMethods"` |
| `OrdersApi` | bff → orders | `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` | `"SafeToRetryMethods"` |
| `PartiesApi` | bff → parties | `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` | `"AddStandardResilienceHandler"` |
| `IdentityBackchannel` (gateway) | gateway → identity-server | `services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs` | `"AddIdentityBackchannelResilience"` |
| `IdentityBackchannel` (shared) | bff/parties/products/orders/baskets → identity-server | `shared/Identity/IdentityValidationExtensions.cs` | `"AddIdentityBackchannelResilience"` |

**Marker chính xác đã chốt** khi triển khai (tasks.md T009-T021):
- `AddIdentityBackchannelResilience` — phương thức mở rộng dùng chung trong
  `shared/Identity/IdentityBackchannelResilience.cs` (research.md Decision 4), gọi từ cả 2 điểm
  backchannel; giữ một nơi duy nhất định nghĩa timeout của backchannel thay vì 2 bản sao.
- `AvailableDestinationsPolicy` — phát hiện qua xác thực thủ công trong lúc triển khai: YARP mặc
  định (`HealthyOrPanic`) không thực sự fail-fast cho cluster chỉ có 1 destination; phải đặt tường
  minh `"HealthyAndUnknown"` (xem `plan.md`/`tasks.md` T016 để biết chi tiết phát hiện này).
- `SafeToRetryMethods` — `HashSet<HttpMethod>` trong `DownstreamClientRegistrationExtensions.cs`
  (research.md Decision 5), chỉ chứa `GET`/`HEAD`; method của request đọc qua
  `Polly.HttpResilienceContextExtensions.GetRequestMessage(context)` chứ không phải qua
  `Outcome.Result` (một lỗi transport không có `HttpResponseMessage` để đọc `RequestMessage` từ đó).

## Quy tắc thay đổi danh sách này

- Thêm một điểm gọi ra ngoài mới vào hệ thống (service mới, dependency ngoài mới, một typed client
  mới) PHẢI thêm một dòng tương ứng vào bảng trên VÀ vào `ResilienceCoverageScanner.ExpectedCallSites`
  trong cùng pull request. Thiếu một trong hai là vi phạm hợp đồng.
- Xoá một điểm gọi ra ngoài khỏi hệ thống (ví dụ ngừng gọi một dependency) PHẢI xoá dòng tương ứng ở
  cả hai nơi — một dòng còn sót lại trỏ tới một file không còn tồn tại sẽ làm `ResilienceCoverageTests`
  đỏ, đúng chủ đích (buộc dọn dẹp thay vì để lại rác).
- `service → broker` KHÔNG có dòng nào trong bảng này cho tới khi SCRUM-31 thêm cuộc gọi thật đầu
  tiên; khi đó, dòng mới PHẢI tuân theo `resilience-policy-contract.md`.
