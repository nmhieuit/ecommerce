---

description: "Task list template for feature implementation"
---

# Tasks: Diễn tập chaos engineering — giết một pod / tiêm độ trễ để kiểm chứng resilience

**Input**: Design documents from `specs/025-chaos-pod-kill-latency/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho phần mã ứng dụng mới
của tính năng này (middleware ở US2) — task test tương ứng là BẮT BUỘC, PHẢI viết trước và xác nhận
FAIL trước khi triển khai. US1 (kill-pod) và US3 (bản ghi kết quả) không thêm mã ứng dụng nào (chỉ
tái sử dụng hạ tầng đã có / tài liệu markdown) nên không có task test xUnit tương ứng — được xác
thực bằng cách chạy `quickstart.md` trên hạ tầng thật, đúng tiền lệ 019/020/021.

**Organization**: Task được nhóm theo user story trong spec.md để mỗi story có thể triển khai và
kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa hoàn thành)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task đều có đường dẫn file cụ thể

## Path Conventions

Bổ sung vào monorepo hiện có (xem plan.md § Project Structure) — không có project/`.csproj` mới,
không có service runtime mới:

- Mã ứng dụng mới (US2): `services/orders/src/Orders.Api/Features/Chaos/`
- Test mới (US2): `services/orders/tests/Orders.Api.UnitTests/Features/Chaos/` (project đã tồn tại)
- Tài liệu vận hành mới (US3): `docs/dien-tap-chaos-engineering/`

---

## Phase 1: Setup (Shared Infrastructure)

**Không có task nào ở phase này.** Tính năng không tạo project/`.csproj` mới, không thêm dependency
mới (research.md Quyết định 1) — mọi file mới đều rơi thẳng vào project đã tồn tại
(`Orders.Api.csproj`, `Orders.Api.UnitTests.csproj`, hai project SDK-style tự động include file `.cs`
mới không cần đăng ký thủ công), nên không có gì để khởi tạo trước khi vào các user story.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Không có task nào ở phase này — để trống có chủ đích.** Ba user story của tính năng này không chia
sẻ mã nguồn hay hạ tầng nào cần dựng trước: US1 (kill-pod) chỉ dùng nguyên trạng resilience/telemetry
đã có (research.md Quyết định 0), US2 (inject-latency) tự chứa hoàn toàn trong
`Features/Chaos/` của Orders.Api, và US3 (bản ghi kết quả) là tài liệu độc lập không phụ thuộc mã của
US1/US2. Vì vậy US1/US2/US3 có thể bắt đầu ngay, không phải chờ nhau.

**Checkpoint**: Không có gì phải hoàn tất trước — có thể bắt đầu bất kỳ user story nào ngay.

---

## Phase 3: User Story 1 - Giết pod của basket service và quan sát hệ thống tự phục hồi (Priority: P1) 🎯 MVP

**Goal**: Xác nhận bằng thực nghiệm rằng khi một pod `baskets` bị xóa trong lúc có tải nhẹ,
Kubernetes tái lập lịch pod thay thế và circuit breaker/retry của `BasketsApiClient` ở BFF engage
quan sát được — không cần thêm bất kỳ mã ứng dụng nào (research.md Quyết định 0).

**Independent Test**: Tạo tải tổng hợp nhẹ gọi basket service qua BFF, chạy
`kubectl delete pod -l app=baskets`, xác nhận (a) pod thay thế được tái lập lịch, (b) circuit
breaker/retry engage quan sát được qua log/telemetry "Polly", (c) tỷ lệ lỗi về bình thường sau khi
pod mới `READY` — tự đủ, không phụ thuộc US2/US3.

### Implementation for User Story 1

- [ ] T001 [US1] Thực hiện [quickstart.md](./quickstart.md) Bước 1 trên một cluster diễn tập (`kind`
      cục bộ theo đúng cách `specs/019-liveness-readiness-probes/quickstart.md` Bước 1–2, hoặc cluster
      diễn tập thật): dựng tải tổng hợp nhẹ gọi `baskets` qua BFF, chạy
      `kubectl delete pod -l app=baskets --field-selector=status.phase=Running`, quan sát
      `kubectl get pods -l app=baskets -w`.
- [ ] T002 [US1] Thu bằng chứng circuit breaker/retry engage của `BasketsApiClient` trong lúc pod thay
      thế chưa `READY` — qua log có cấu trúc nguồn `"Polly"` của BFF, hoặc Kibana Data View **Traces**
      (`traces-generic.otel-default*`) lọc `resource.attributes.service.name : "Bff.Api"`, hoặc
      `dotnet-counters monitor --process-id <pid> Polly` nếu chạy cục bộ không có Elastic (khớp kỹ
      thuật đã dùng ở `specs/020-timeouts-retry-circuit-breaker/quickstart.md` Bước 6).
- [ ] T003 [US1] Xác nhận thời gian phục hồi (từ lúc pod cũ bị xóa tới lúc pod mới `READY 1/1` và tỷ
      lệ lỗi của tải nền về bình thường) và ghi lại số đo được — dữ liệu đầu vào cho bản ghi kết quả
      của User Story 3 (không tạo file ở task này, chỉ ghi chú tại chỗ để dùng ở T010).

**Checkpoint**: User Story 1 đã được xác nhận độc lập — không có mã ứng dụng nào bị chạm, không phụ
thuộc User Story 2/3.

---

## Phase 4: User Story 2 - Tiêm độ trễ vào orders service và quan sát ngân sách SLO bị tiêu hao trên dashboard theo thời gian thực (Priority: P1) 🎯 MVP

**Goal**: Thêm một cơ chế tiêm độ trễ tối thiểu, mặc định tắt, cho Orders.Api để có thể lặp lại kịch
bản "làm chậm một endpoint" mà không cần sửa mã nguồn mỗi lần chạy (research.md Quyết định 1), rồi
xác nhận dashboard SLO đã có (021) phản ánh việc tiêu hao ngân sách gần thời gian thực.

**Independent Test**: `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection`
pass đủ 4 bất biến của [contracts/chaos-latency-injection-contract.md](./contracts/chaos-latency-injection-contract.md);
sau đó bật `Chaos:AllowLatencyInjection=true` trên một môi trường diễn tập, gửi header
`X-Chaos-Latency-Ms: 2000` tới Orders.Api, xác nhận response bị trì hoãn ≈2s và dashboard SLO thể
hiện tiêu hao — tự đủ, không phụ thuộc US1/US3.

### Tests for User Story 2 ⚠️

> **Viết các test này TRƯỚC, xác nhận FAIL (middleware chưa tồn tại) trước khi triển khai (Constitution Principle III).**

- [ ] T004 [US2] Viết `services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs`
      — gọi middleware trực tiếp với một `RequestDelegate` giả và một `TimeProvider`/đồng hồ giả lập
      (không `Task.Delay` thật) để test tất định, xác nhận đủ 4 bất biến của
      [contracts/chaos-latency-injection-contract.md](./contracts/chaos-latency-injection-contract.md):
      (1) `AllowLatencyInjection=false` (mặc định) → không đọc header, hành vi giống hệt không có
      middleware; (2) `AllowLatencyInjection=true` nhưng không có header `X-Chaos-Latency-Ms` →
      không có độ trễ; (3) `AllowLatencyInjection=true` + header hợp lệ (ví dụ `2000`) → trì hoãn
      đúng khoảng đó trước khi gọi `next()`; (4) header vượt `MaxInjectedLatencyMs=30000` (ví dụ
      `999999`) → bị kẹp về đúng 30000, không bao giờ trì hoãn lâu hơn. Chạy
      `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection`
      và xác nhận FAIL (biên dịch lỗi hoặc test đỏ) vì middleware/`ChaosOptions` chưa tồn tại.

### Implementation for User Story 2

- [ ] T005 [P] [US2] Tạo `services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs` — lớp cấu
      hình `AllowLatencyInjection` (bool, mặc định `false`) buộc vào section `Chaos` của
      `IConfiguration`, cùng hằng số `MaxInjectedLatencyMs = 30000` (data-model.md mục 1).
- [ ] T006 [US2] Tạo `services/orders/src/Orders.Api/Features/Chaos/ChaosLatencyInjectionMiddleware.cs`
      hiện thực đủ 4 bất biến ở T004 — parse header `X-Chaos-Latency-Ms` bằng
      `int.TryParse` (giá trị âm/không parse được coi như vắng mặt), kẹp trần bằng
      `Math.Min(parsed, ChaosOptions.MaxInjectedLatencyMs)`, `await Task.Delay(...)` chỉ khi
      `AllowLatencyInjection=true` và có giá trị hợp lệ, sau đó luôn gọi `next(context)` — không đổi
      status/header/body của response (Bất biến 6 của hợp đồng). Chạy lại T004, xác nhận PASS.
- [ ] T007 [US2] Sửa `services/orders/src/Orders.Api/appsettings.json` — thêm comment `"//Chaos"`
      theo đúng quy ước `"//FeatureToggles"` đã có (giải thích: công cụ vận hành thường trực, mặc
      định tắt, không phải toggle rollout có ngày gỡ — xem plan.md Constitution Check mục X) và khối
      `"Chaos": { "AllowLatencyInjection": false }`.
- [ ] T008 [US2] Sửa `services/orders/src/Orders.Api/Program.cs` — đăng ký
      `ChaosOptions` (`builder.Services.Configure<ChaosOptions>(builder.Configuration.GetSection("Chaos"))`)
      và `app.UseMiddleware<ChaosLatencyInjectionMiddleware>()` ngay sau `app.UseServiceDefaults()`,
      **trước** `app.UseIdentityValidation()` (data-model.md mục 1, Bất biến 5 của hợp đồng).
- [ ] T009 [US2] Thực hiện [quickstart.md](./quickstart.md) Bước 2–3 trên một cluster diễn tập với
      `Chaos:AllowLatencyInjection=true`: gửi `X-Chaos-Latency-Ms: 2000` liên tục tới Orders.Api,
      xác nhận circuit breaker của `OrdersApiClient` (BFF) mở theo đúng ngưỡng, và dashboard
      `SLO vận hành hằng ngày — 7 service` (đã import từ 021) thể hiện tiêu hao ngân sách latency
      của `Orders.Api` gần thời gian thực. Dừng tiêm (ngừng gửi header) và xác nhận không cần
      restart pod nào để dừng.

**Checkpoint**: User Story 1 VÀ 2 đều hoạt động độc lập; `dotnet test services/orders/tests/Orders.Api.UnitTests` pass toàn bộ.

---

## Phase 5: User Story 3 - Ghi lại kết quả bài tập chaos thành một kết luận kiểm chứng được (Priority: P2)

**Goal**: Có một nơi và một mẫu thống nhất để ghi nhận kết luận của mỗi lần chạy bài tập chaos (đạt
hoặc sai lệch kèm bug ticket), và tra cứu lại được các lần chạy trước đó.

**Independent Test**: Hoàn thành một bài tập chaos bất kỳ (US1 hoặc US2), sao chép mẫu thành một bản
ghi kết quả mới trong `docs/dien-tap-chaos-engineering/ket-qua/`, điền đủ trường bắt buộc, và xác
nhận nó xuất hiện trong `docs/dien-tap-chaos-engineering/README.md` — tự đủ, không phụ thuộc việc
US1/US2 đã "xong" theo nghĩa mã nguồn (chỉ cần một lần chạy thực tế bất kỳ).

### Implementation for User Story 3

- [ ] T010 [P] [US3] Tạo `docs/dien-tap-chaos-engineering/mau-ket-qua.md` — mẫu bản ghi kết quả với
      đủ các trường bắt buộc tại [data-model.md](./data-model.md) mục 3:
      `ngay_chay`, `kich_ban` (`kill-pod` | `inject-latency`), `nguoi_thuc_hien`, `quan_sat`,
      `ket_luan` (`dat` | `sai_lech`), `jira_ticket` (bắt buộc khi `ket_luan=sai_lech` — xem
      [contracts/exercise-outcome-writeup-contract.md](./contracts/exercise-outcome-writeup-contract.md)
      Bất biến 3).
- [ ] T011 [P] [US3] Tạo `docs/dien-tap-chaos-engineering/README.md` — tóm tắt runbook (tham chiếu
      [quickstart.md](./quickstart.md) của tính năng này thay vì lặp lại nội dung), và một mục "Lịch
      sử chạy" liệt kê mọi file trong `ket-qua/` (rỗng ban đầu, mới nhất trước — Bất biến 4 của hợp
      đồng bản ghi kết quả).
- [ ] T012 [US3] Tạo `docs/dien-tap-chaos-engineering/ket-qua/.gitkeep` để thư mục rỗng ban đầu được
      theo dõi bởi git (không nội dung nào khác — bản ghi kết quả thật chỉ xuất hiện khi bài tập
      thật sự được chạy, T013).
- [ ] T013 [US3] Thực hiện [quickstart.md](./quickstart.md) Bước 4: dựa trên kết quả quan sát được ở
      T001–T003 (kill-pod) và/hoặc T009 (inject-latency), sao chép `mau-ket-qua.md` thành
      `docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-kill-pod.md` và/hoặc
      `...-inject-latency.md`, điền đủ trường; nếu bất kỳ kỳ vọng nào ở T001–T003/T009 không khớp
      thực tế, đặt `ket_luan: sai_lech` và mở bug ticket, dán liên kết vào `jira_ticket`. Cập nhật
      `docs/dien-tap-chaos-engineering/README.md` (T011) để liệt kê (các) file vừa tạo.

**Checkpoint**: Cả 3 user story hoạt động độc lập — có ít nhất một bản ghi kết quả tra cứu được cho
mỗi bài tập đã chạy.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác nhận toàn bộ tính năng nhất quán, không hồi quy.

- [ ] T014 Chạy `dotnet build Ecommerce.slnx` và `dotnet test services/orders/tests/Orders.Api.UnitTests`
      — xác nhận không hồi quy cho các test đã có của Orders.Api ngoài `ChaosLatencyInjectionMiddlewareTests`.
- [ ] T015 Chạy lại toàn bộ [quickstart.md](./quickstart.md) từ đầu tới cuối (Bước 1–4 + Dọn dẹp) một
      lượt liền mạch trên cùng một cluster diễn tập, xác nhận thứ tự các bước không phụ thuộc ẩn nào
      bị bỏ sót giữa US1/US2/US3.
- [ ] T016 Đối chiếu lại `specs/025-chaos-pod-kill-latency/checklists/requirements.md` — xác nhận mọi
      mục vẫn PASS sau khi có kết quả triển khai thật (theo đúng tiền lệ "Cập nhật sau khi triển
      khai" của `specs/021-declare-service-slos/plan.md`), cập nhật `plan.md` nếu triển khai thật
      phát hiện sai lệch so với Technical Context/Constitution Check đã viết trước.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** và **Foundational (Phase 2)**: không có task — không chặn gì.
- **User Stories (Phase 3–5)**: có thể bắt đầu ngay và **độc lập hoàn toàn với nhau** (US1 không
  chạm US2; US2 không chạm US1; US3 chỉ cần MỘT trong hai đã chạy xong để có dữ liệu điền vào bản
  ghi kết quả, không cần cả hai, và không chạm mã nguồn của US1/US2).
- **Polish (Phase 6)**: phụ thuộc US1, US2, US3 đã hoàn tất (T015 chạy lại toàn bộ quickstart cần cả
  ba).

### User Story Dependencies

- **User Story 1 (P1)**: Không phụ thuộc story nào khác. Không có mã nguồn để phụ thuộc.
- **User Story 2 (P1)**: Không phụ thuộc User Story 1. Độc lập hoàn toàn (khác service, khác cơ chế).
- **User Story 3 (P2)**: Cần ít nhất kết quả của MỘT trong US1 hoặc US2 đã chạy để có nội dung điền
  vào bản ghi kết quả (T013) — nhưng mẫu/README (T010–T012) có thể tạo trước, độc lập với việc US1/US2
  đã chạy hay chưa.

### Trong mỗi User Story

- US2: Test (T004) PHẢI viết trước và FAIL trước khi có T005–T006 (Constitution Principle III).
- US1, US3: không có mã nguồn — thứ tự task là thứ tự thực hiện vận hành/tài liệu, không có ràng buộc
  biên dịch.

### Parallel Opportunities

- T005 (`ChaosOptions.cs`) có thể làm song song với việc viết T004 (test) vì là một lớp dữ liệu
  thuần không có logic cần test riêng — nhưng T006 (middleware, có logic được T004 kiểm chứng) PHẢI
  đợi T004 đã viết và FAIL.
- T010 và T011 (mẫu + README của US3) độc lập file, có thể làm song song.
- US1 (T001–T003), US2 (T004–T009), và phần tạo mẫu/README của US3 (T010–T012) có thể được 3 người
  khác nhau làm song song ngay từ đầu.

---

## Parallel Example: User Story 2

```bash
# T005 (model cấu hình) có thể làm song song với việc bắt đầu viết T004 (test, viết trước, phải FAIL trước khi có T005/T006 tồn tại đầy đủ):
Task: "Tạo services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs"
Task: "Viết services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 và 2, cả hai đều P1)

1. Bỏ qua Phase 1–2 (không có task).
2. Hoàn thành Phase 3 (User Story 1) — xác nhận độc lập, không cần mã nguồn mới.
3. Hoàn thành Phase 4 (User Story 2) — test trước (T004, FAIL) → middleware (T005–T008, PASS) → xác
   nhận trên hạ tầng thật (T009).
4. **DỪNG VÀ XÁC NHẬN**: cả hai bài tập chaos chính (kill-pod, inject-latency) đã chạy được và quan
   sát đúng như Acceptance Criteria của SCRUM-34 — đây đã là giá trị cốt lõi mà Jira ticket yêu cầu.

### Incremental Delivery

1. User Story 1 → chạy thử trên cluster diễn tập → có bằng chứng circuit breaker engage (MVP một
   nửa — không cần code).
2. User Story 2 → viết test, thêm middleware, xác nhận dashboard SLO tiêu hao (MVP nửa còn lại).
3. User Story 3 → thêm mẫu/README, điền bản ghi kết quả cho (các) lần chạy ở bước 1–2 (hoàn thiện
   giá trị "kiểm chứng được", không chỉ "chạy được").
4. Polish (Phase 6) → chạy lại toàn bộ end-to-end một lượt liền mạch để xác nhận không có phụ thuộc
   ẩn nào giữa ba story.

---

## Notes

- [P] tasks = khác file, không phụ thuộc nhau.
- [Story] label ánh xạ task về đúng user story để truy vết.
- US1 và US3 không có test tự động — bản chất là vận hành/tài liệu; xác thực bằng cách chạy
  `quickstart.md` trên hạ tầng thật, không phải bằng `dotnet test`.
- US2 PHẢI theo Red-Green: T004 viết trước và FAIL, T005–T008 làm nó PASS — không đảo thứ tự.
- Không sửa `services/baskets`, BFF, hay bất kỳ manifest Ansible/K8s nào ở bất kỳ task nào trong danh
  sách này (khớp Constraints của plan.md).
