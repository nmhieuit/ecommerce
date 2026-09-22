# QA: Event schema có version — OrderPlaced, BasketCheckedOut

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

**Nguồn đối chiếu**:
[`architecture/008`](../architecture/008_Architect_event%20schema%20có%20version.md) ·
[`development/008`](../development/008_Development_event%20schema%20có%20version.md) ·
[`summary/008`](../summary/008_PO_event%20schema%20có%20version.md) ·
[`spec-summary-vi/008`](../spec-summary-vi/008-versioned-event-schemas.json) — đối chiếu chéo cả 4,
kèm xác minh lại với source code thật khi có nghi vấn.

## Luồng happy-case đã rà soát (US1 → US3)

1. **Hợp đồng ở 1 nơi** (US1): JSON Schema 2020-12 + record C# của `OrderPlaced`/`BasketCheckedOut` nằm trong `shared/EventContracts`,
   service không tự định nghĩa lại.
2. **Phiên bản đã công bố là bất biến** (US2): sửa 1 schema đã công bố (kể cả thêm trường bắt buộc mà không tạo phiên bản mới)
   làm test đóng băng nội dung thất bại — chặn merge.
3. **Bên phát/bên nhận** (US3): event thật serialize ra kiểm chứng hợp lệ với schema; consumer không vỡ khi gặp trường lạ.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Spec 008 chỉ là **thư viện hợp đồng** (`shared/EventContracts` + `EventContracts.UnitTests`, không DB/broker/container), nên không có
bước dựng Docker Desktop hay gọi Postman; phần "thủ công" là các lệnh kiểm tra hợp đồng ở quickstart
[`specs/008-versioned-event-schemas/quickstart.md`](../../specs/008-versioned-event-schemas/quickstart.md), chạy tay và ghi kết quả thật.

> Nếu `localhost` không gọi được dù container `healthy`: khởi động lại hẳn Docker Desktop (lỗi forwarding
> IPv6 loopback `::1` của WSL2), không phải lỗi ứng dụng (không liên quan tới spec này nhưng giữ để nhất quán các file QA).

### Thủ công — kiểm tra hợp đồng bằng lệnh

| Bước | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| SC-001 — schema ở vị trí dùng chung | `find shared/EventContracts/schemas -name "*.schema.json"` | `OrderPlaced.v1` và `BasketCheckedOut.v1` | Đúng: 2 file (`OrderPlaced.v1.schema.json`, `BasketCheckedOut.v1.schema.json`) |
| SC-001 — service không tự định nghĩa lại | `grep -rl "OrderPlaced\|BasketCheckedOut" services/orders services/baskets services/bff --include="*.cs"` | **0 kết quả** (quickstart) | **Lệch tài liệu**: 9 file khớp (Orders `OrdersDbContext`, `OrderEndpoints`, Baskets `BasketCheckedOutMapper` + các file test) — vì từ spec 024 Orders/Baskets thật sự phát/tiêu thụ 2 event. Không có `record OrderPlaced…`/`BasketCheckedOut…` định nghĩa lại trong `services/`; `Orders.Api`, `Baskets.Api` và 2 project `*.ContractTests` tham chiếu `shared/EventContracts` (đúng US1-KB2) |
| SC-002 — thay đổi phá vỡ không kèm phiên bản bị bắt | Sửa `OrderPlaced.v1.schema.json` (thêm thuộc tính bắt buộc `qaExperimentField`), chạy `dotnet test shared/EventContracts.UnitTests`, rồi hoàn tác (`git checkout`) | Test đóng băng thất bại | Đúng: **2 test đỏ** — `SchemaImmutabilityTests.OrderPlaced_V1_Schema_Content_Is_Frozen` và `SchemaValidationTests.Serialized_OrderPlacedV1_Validates_Against_Its_Published_Schema` (vì record không có trường mới); sau hoàn tác 6/6 PASS, `git status` sạch |
| SC-003 — consumer cũ chịu được trường lạ | `dotnet test ... --filter TolerantReaderTests` | PASS | 2/2 PASS |
| SC-004 — tìm phiên bản/khung ngưng dùng ở 1 chỗ | `shared/EventContracts/README.md` | Nêu phiên bản hiện hành, phiên bản cũ, chính sách deprecation | Có bảng phiên bản (cả 2 event: V1, chưa có phiên bản cũ) và mục "Deprecation window". **Nhưng dòng đầu README "Nothing here is referenced by a service yet… no broker exists" đã sai** (xem QA_Debt) |
| Chặn merge trong CI | `scripts/ci/run-dotnet-tests.sh unit` (Jenkins stage "unit tests") | `EventContracts.UnitTests` nằm trong tầng unit | Theo script: tầng `unit` = mọi `*Tests.csproj` còn lại kể cả `shared/`, nên `EventContracts.UnitTests` chạy trong cổng PR. **Chưa chạy Jenkins thật** — chỉ đọc script |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm test (comment mỗi hàm đã gắn `Task nguồn: spec 008 ...`, xem lại tại đó nếu cần biết test ứng với US/task nào). Bộ test không cần Docker/DB/broker.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US2/FR-003,006/SC-002 — schema đã công bố là bất biến (hash SHA-256 đóng băng) cho cả 2 event | [`SchemaImmutabilityTests.cs:46`](../../shared/EventContracts.UnitTests/SchemaImmutabilityTests.cs#L46) · [`:60`](../../shared/EventContracts.UnitTests/SchemaImmutabilityTests.cs#L60) | `dotnet test shared/EventContracts.UnitTests --filter "FullyQualifiedName~SchemaImmutabilityTests"` |
| US3/FR-009 — event dựng thật + serialize kiểm chứng hợp lệ với schema đã công bố (record và schema không lệch nhau) | [`SchemaValidationTests.cs:49`](../../shared/EventContracts.UnitTests/SchemaValidationTests.cs#L49) · [`:82`](../../shared/EventContracts.UnitTests/SchemaValidationTests.cs#L82) | `dotnet test shared/EventContracts.UnitTests --filter "FullyQualifiedName~SchemaValidationTests"` |
| US3/FR-007/SC-003 — consumer không vỡ khi payload có trường lạ (tolerant reader) | [`TolerantReaderTests.cs:41`](../../shared/EventContracts.UnitTests/TolerantReaderTests.cs#L41) · [`:93`](../../shared/EventContracts.UnitTests/TolerantReaderTests.cs#L93) | `dotnet test shared/EventContracts.UnitTests --filter "FullyQualifiedName~TolerantReaderTests"` |

**Kết quả lượt QA này**: `dotnet test shared/EventContracts.UnitTests` **6/6 PASS** (2 immutability + 2 validation + 2 tolerant reader); khi cố ý thêm trường bắt buộc vào
`OrderPlaced.v1.schema.json` thì 2/6 đỏ (xem bảng thủ công), sau hoàn tác 6/6 PASS trở lại.

## Kết luận

**PASS kèm ghi chú** — 4 nguồn mô tả cùng 1 luồng và bộ test **6/6 PASS**; thí nghiệm của quickstart (thêm trường bắt buộc vào `OrderPlaced.v1.schema.json` không kèm phiên bản mới) làm 2 test đỏ
đúng như thiết kế, hoàn tác thì xanh lại (SC-002). Services thật sự dùng lại hợp đồng dùng chung, không có định nghĩa trùng lặp (US1). Ghi chú: (1) `shared/EventContracts/README.md` và quickstart SC-001 vẫn nói "chưa service nào tham chiếu / 0 kết quả grep" trong khi
Orders/Baskets đã phát/tiêu thụ 2 event từ spec 024; (2) đường nâng phiên bản (V2, khung ngưng dùng — FR-004, US2-KB1/KB3) chưa từng được thực hành vì cả 2 event mới chỉ có V1; (3) kiểm tra tương thích chỉ là hash đóng băng thô, không phân biệt thay đổi
phá vỡ/không phá vỡ; (4) chưa chạy Jenkins thật. Chi tiết: [QA_Debt.md](QA_Debt.md).
