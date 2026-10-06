---

description: "Task list template for feature implementation"
---

# Tasks: Diễn tập chaos engineering — tiêm độ trễ để kiểm chứng resilience

> **Cập nhật (spec 031, 2026-10-06)**: các task kill-pod/Kubernetes đã gỡ; số task giữ nguyên để không gãy tham chiếu.

**Input**: Design documents from `specs/025-chaos-pod-kill-latency/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho phần mã ứng dụng mới
của tính năng này (middleware ở US2) — task test tương ứng là BẮT BUỘC, PHẢI viết trước và xác nhận
FAIL trước khi triển khai. US3 (bản ghi kết quả) không thêm mã ứng dụng nào (chỉ
tái sử dụng hạ tầng đã có / tài liệu markdown) nên không có task test xUnit tương ứng — được xác
thực bằng cách chạy `quickstart.md` trên hạ tầng thật, đúng tiền lệ 019/020/021.

**Organization**: Task được nhóm theo user story trong spec.md để mỗi story có thể triển khai và
kiểm thử độc lập.

**Ghi chú lịch sử thực thi (rút gọn ở spec 031)**:

- Tính năng đã được thực hiện qua 4 phiên `/speckit-implement`. Phần chạy trên cluster Kubernetes
  (kill-pod, pod giả lập bằng image công khai, port-forward) đã gỡ ở spec 031 cùng hai bản ghi kết quả
  kill-pod; không còn dùng cluster cho diễn tập chaos.
- Kết quả còn giá trị cho phần tiêm độ trễ: cơ chế `Task.Delay` không chặn thread nên circuit breaker
  của BFF KHÔNG trip ở bất kỳ lần thử nào, kể cả với `autocannon` (50 kết nối × 25 s) — đặc tính thật
  của cơ chế (quyết định có chủ đích ở research.md), không phải hạn chế của công cụ đo; đáng mở một
  bug/thảo luận ticket riêng về việc Acceptance Criteria gốc của SCRUM-34 có còn phù hợp không.
  Dashboard SLO Kibana thật cho Orders.Api p95 = 3,080 ms / p99 = 3,343 ms trong lúc diễn tập, đúng SC-003.
- T015 và T016 đã đóng — xem ghi chú từng task.

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
sẻ mã nguồn hay hạ tầng nào cần dựng trước: US1 (kill-pod, đã gỡ ở spec 031), US2 (inject-latency) tự chứa hoàn toàn trong
`Features/Chaos/` của Orders.Api, và US3 (bản ghi kết quả) là tài liệu độc lập không phụ thuộc mã của
US1/US2. Vì vậy US1/US2/US3 có thể bắt đầu ngay, không phải chờ nhau.

**Checkpoint**: Không có gì phải hoàn tất trước — có thể bắt đầu bất kỳ user story nào ngay.

---

## Phase 3: User Story 1 - (ĐÃ GỠ) Giết pod của basket service và quan sát hệ thống tự phục hồi

**Đã gỡ ở spec [031-error-group-catalog](../031-error-group-catalog/spec.md) (2026-10-06).** Kịch bản giết pod
trên cluster Kubernetes (T001–T003) không còn được dùng; số task T001–T003 được giữ làm chỗ trống để không
làm gãy tham chiếu. Việc luyện "service chết" nay do nhóm 8 (container chết) của spec 031 đảm nhiệm trên
Docker Compose.

- [X] T001 [US1] (đã gỡ ở spec 031) Kịch bản kill-pod trên Kubernetes.
- [X] T002 [US1] (đã gỡ ở spec 031) Thu bằng chứng circuit breaker/retry engage khi pod thay thế chưa sẵn sàng.
- [X] T003 [US1] (đã gỡ ở spec 031) Đo thời gian phục hồi sau khi xóa pod.

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

- [X] T004 [US2] Viết `services/orders/tests/Orders.Api.UnitTests/Features/Chaos/ChaosLatencyInjectionMiddlewareTests.cs`
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
      (**kết quả thực tế**: đã viết đủ 6 test case [4 bất biến + 2 case bổ sung: header không parse
      được/âm/bằng 0, và `next()` luôn được gọi]. KHÔNG chạy được `dotnet test` — môi trường thực thi
      phiên này không có .NET SDK cài sẵn [`which dotnet` → not found], nên trạng thái FAIL trước khi
      có T005/T006 chỉ được suy luận bằng đọc mã [file tham chiếu `ChaosOptions`/`IChaosDelay`/
      `ChaosLatencyInjectionMiddleware` chưa tồn tại tại thời điểm viết], không được một lần chạy
      thật xác nhận. Cần chạy `dotnet test` thật ở môi trường có .NET 10 SDK trước khi coi tính năng
      sẵn sàng merge — xem T014.
      **[Cập nhật phiên sau, môi trường có .NET 10 SDK]**: chạy thật
      `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection`
      — PASS đủ 8/8 test case sau khi sửa 1 lỗi biên dịch nhỏ [CA1859 ở T006].)

### Implementation for User Story 2

- [X] T005 [P] [US2] Tạo `services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs` — lớp cấu
      hình `AllowLatencyInjection` (bool, mặc định `false`) buộc vào section `Chaos` của
      `IConfiguration`, cùng hằng số `MaxInjectedLatencyMs = 30000` (data-model.md mục 1).
      (**kết quả thực tế**: cũng thêm `services/orders/src/Orders.Api/Features/Chaos/IChaosDelay.cs`
      [không có trong tasks.md ban đầu] — seam nhỏ giữa middleware và `Task.Delay` thật, cần thiết để
      T004 ghi lại khoảng trễ được yêu cầu mà không thực sự chờ trong lúc test, đúng tinh thần
      "TimeProvider/đồng hồ giả lập" của research.md Quyết định 4 mà không cần thêm gói NuGet
      `Microsoft.Extensions.TimeProvider.Testing`.)
- [X] T006 [US2] Tạo `services/orders/src/Orders.Api/Features/Chaos/ChaosLatencyInjectionMiddleware.cs`
      hiện thực đủ 4 bất biến ở T004 — parse header `X-Chaos-Latency-Ms` bằng
      `int.TryParse` (giá trị âm/không parse được coi như vắng mặt), kẹp trần bằng
      `Math.Min(parsed, ChaosOptions.MaxInjectedLatencyMs)`, `await Task.Delay(...)` chỉ khi
      `AllowLatencyInjection=true` và có giá trị hợp lệ, sau đó luôn gọi `next(context)` — không đổi
      status/header/body của response (Bất biến 6 của hợp đồng). Chạy lại T004, xác nhận PASS.
      (**kết quả thực tế**: hiện thực xong, đọc lại thủ công khớp đủ 6 test case của T004. KHÔNG
      chạy được `dotnet test` để xác nhận PASS thật — cùng lý do môi trường ở T004 [không có .NET
      SDK]. Cần chạy thật trước khi merge — xem T014.
      **[Cập nhật phiên sau]**: chạy thật, PASS 8/8. Phải sửa 1 lỗi biên dịch trong
      `ChaosLatencyInjectionMiddlewareTests.cs` phát hiện lúc build thật — cảnh báo CA1859 [Roslyn
      analyzer, treat-warnings-as-errors] đòi kiểu trả về của helper `CreateContextWithHeader` là
      `DefaultHttpContext` cụ thể thay vì `HttpContext` trừu tượng [để JIT devirtualize] — không đổi
      hành vi test, chỉ đổi khai báo kiểu trả về.)
- [X] T007 [US2] Sửa `services/orders/src/Orders.Api/appsettings.json` — thêm comment `"//Chaos"`
      theo đúng quy ước `"//FeatureToggles"` đã có (giải thích: công cụ vận hành thường trực, mặc
      định tắt, không phải toggle rollout có ngày gỡ — xem plan.md Constitution Check mục X) và khối
      `"Chaos": { "AllowLatencyInjection": false }`.
- [X] T008 [US2] Sửa `services/orders/src/Orders.Api/Program.cs` — đăng ký
      `ChaosOptions` (`builder.Services.Configure<ChaosOptions>(builder.Configuration.GetSection("Chaos"))`)
      và `app.UseMiddleware<ChaosLatencyInjectionMiddleware>()` ngay sau `app.UseServiceDefaults()`,
      **trước** `app.UseIdentityValidation()` (data-model.md mục 1, Bất biến 5 của hợp đồng).
      (**kết quả thực tế**: cũng đăng ký `IChaosDelay`/`SystemChaosDelay` [T005] làm singleton — cần
      thiết để DI resolve được constructor mới của middleware.)
- [X] T009 [US2] Thực hiện [quickstart.md](./quickstart.md) Bước 1–2 trên stack Docker Compose với
      `Chaos:AllowLatencyInjection=true`: gửi `X-Chaos-Latency-Ms: 2000` liên tục tới Orders.Api,
      xác nhận circuit breaker của `OrdersApiClient` (BFF) mở theo đúng ngưỡng, và dashboard
      `Xử lý sự cố — 7 service` (đã import từ 021) thể hiện tiêu hao ngân sách latency
      của `Orders.Api` gần thời gian thực. Dừng tiêm (ngừng gửi header) và xác nhận không cần
      restart container nào để dừng.
      (**kết quả thực tế**: đã chạy trên container `orders-api` THẬT [stack `ecomerce-local`] với
      `Chaos__AllowLatencyInjection=true` đặt qua biến môi trường [không sửa `appsettings.json` mặc
      định]. Phát hiện VÀ SỬA một lỗi build thật tiền tồn tại không liên quan tới feature này:
      `services/orders/src/Orders.Api/Dockerfile` thiếu `COPY shared/EventContracts/` [024 thêm
      `ProjectReference` tới `EventContracts.csproj` nhưng không cập nhật Dockerfile]. Middleware hoạt
      động đúng [~2,1 s cho header `2000`]. Tải qua BFF với nhiều mức đồng thời [kể cả `autocannon`, 50
      kết nối × 25 s]: lỗi `504`/timeout chỉ rải rác, log xác nhận Polly retry
      [`OrdersApi-standard//Standard-Retry`] engage thật với `AttemptTimeout=1s` bị vượt — nhưng circuit
      breaker KHÔNG trip [lỗi rải rác, không đủ mật độ trong cửa sổ sampling; `Task.Delay` không chặn
      thread]. Dashboard SLO Kibana xác nhận thật: Orders.Api p95 = 3,080 ms / p99 = 3,343 ms. Chi tiết
      đầy đủ:
      [docs/dien-tap-chaos-engineering/ket-qua/2026-09-14-inject-latency.md](../../docs/dien-tap-chaos-engineering/ket-qua/2026-09-14-inject-latency.md).)

**Checkpoint**: User Story 2 hoạt động độc lập; `dotnet test services/orders/tests/Orders.Api.UnitTests` pass toàn bộ.
(**Trạng thái hiện tại**: `dotnet test` PASS thật [8/8 test chaos, xem T014]. T009 đã chạy trên container
Docker thật — xem bản ghi kết quả trong `docs/dien-tap-chaos-engineering/ket-qua/`. Circuit breaker chưa
được xác nhận trip ở bất kỳ lần chạy nào, luôn rải rác.)

---

## Phase 5: User Story 3 - Ghi lại kết quả bài tập chaos thành một kết luận kiểm chứng được (Priority: P2)

**Goal**: Có một nơi và một mẫu thống nhất để ghi nhận kết luận của mỗi lần chạy bài tập chaos (đạt
hoặc sai lệch kèm bug ticket), và tra cứu lại được các lần chạy trước đó.

**Independent Test**: Hoàn thành một bài tập chaos bất kỳ (US1 hoặc US2), sao chép mẫu thành một bản
ghi kết quả mới trong `docs/dien-tap-chaos-engineering/ket-qua/`, điền đủ trường bắt buộc, và xác
nhận nó xuất hiện trong `docs/dien-tap-chaos-engineering/README.md` — tự đủ, không phụ thuộc việc
US1/US2 đã "xong" theo nghĩa mã nguồn (chỉ cần một lần chạy thực tế bất kỳ).

### Implementation for User Story 3

- [X] T010 [P] [US3] Tạo `docs/dien-tap-chaos-engineering/mau-ket-qua.md` — mẫu bản ghi kết quả với
      đủ các trường bắt buộc tại [data-model.md](./data-model.md) mục 3:
      `ngay_chay`, `kich_ban` (`inject-latency`), `nguoi_thuc_hien`, `quan_sat`,
      `ket_luan` (`dat` | `sai_lech`), `jira_ticket` (bắt buộc khi `ket_luan=sai_lech` — xem
      [contracts/exercise-outcome-writeup-contract.md](./contracts/exercise-outcome-writeup-contract.md)
      Bất biến 3).
- [X] T011 [P] [US3] Tạo `docs/dien-tap-chaos-engineering/README.md` — tóm tắt runbook (tham chiếu
      [quickstart.md](./quickstart.md) của tính năng này thay vì lặp lại nội dung), và một mục "Lịch
      sử chạy" liệt kê mọi file trong `ket-qua/` (rỗng ban đầu, mới nhất trước — Bất biến 4 của hợp
      đồng bản ghi kết quả).
- [X] T012 [US3] Tạo `docs/dien-tap-chaos-engineering/ket-qua/.gitkeep` để thư mục rỗng ban đầu được
      theo dõi bởi git (không nội dung nào khác — bản ghi kết quả thật chỉ xuất hiện khi bài tập
      thật sự được chạy, T013).
- [X] T013 [US3] Thực hiện [quickstart.md](./quickstart.md) Bước 3: dựa trên kết quả quan sát được ở
      T009 (inject-latency), sao chép `mau-ket-qua.md` thành
      `docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-inject-latency.md`, điền đủ trường; nếu bất kỳ
      kỳ vọng nào ở T009 không khớp thực tế, đặt `ket_luan: sai_lech` và mở bug ticket, dán liên kết vào
      `jira_ticket`. Cập nhật `docs/dien-tap-chaos-engineering/README.md` (T011) để liệt kê file vừa tạo.
      (**kết quả thực tế**: đã tạo
      [ket-qua/2026-09-14-inject-latency.md](../../docs/dien-tap-chaos-engineering/ket-qua/2026-09-14-inject-latency.md)
      dựa trên quan sát thật của T009 [các lần thử trên container Docker], `ket_luan: sai_lech` với
      `jira_ticket` để trống [chưa mở ticket thật — cần người dùng mở ticket Jira thật và dán link nếu
      muốn theo dõi chính thức]. README đã liệt kê. Hai bản ghi kill-pod trước đây đã gỡ ở spec 031.)

**Checkpoint**: Cả 3 user story hoạt động độc lập — có ít nhất một bản ghi kết quả tra cứu được cho
mỗi bài tập đã chạy.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác nhận toàn bộ tính năng nhất quán, không hồi quy.

- [X] T014 Chạy `dotnet build Ecommerce.slnx` và `dotnet test services/orders/tests/Orders.Api.UnitTests`
      — xác nhận không hồi quy cho các test đã có của Orders.Api ngoài `ChaosLatencyInjectionMiddlewareTests`.
      (**kết quả thực tế**: CHƯA chạy được — phiên làm việc này không có .NET SDK cài sẵn. Rà soát
      thủ công: `ChaosOptions.cs`/`IChaosDelay.cs`/`ChaosLatencyInjectionMiddleware.cs` chỉ dùng API
      đã có sẵn trong `Microsoft.AspNetCore.Http`/`Microsoft.Extensions.Options` [không thêm
      `PackageReference` nào]; `Program.cs` chỉ thêm 2 dòng đăng ký DI + 1 dòng `UseMiddleware` không
      đổi thứ tự các middleware/registration khác đã có. Chưa có xác nhận biên dịch thật.
      **[Cập nhật phiên sau, môi trường có .NET 10 SDK]**: `dotnet build Ecommerce.slnx` — SUCCEEDED
      [0 Warning, 0 Error] sau khi sửa CA1859 [xem T004/T006].
      `dotnet test services/orders/tests/Orders.Api.UnitTests` [toàn bộ project, không filter] — 22
      passed, 1 failed, tổng 23. Test fail duy nhất là `HealthCheckTests.HealthLive_ReturnsOk`
      [`OptionsValidationException: Missing required secret(s): ConnectionStrings:OrdersDb`] — xác
      nhận đây KHÔNG phải hồi quy do tính năng này: đối chiếu bằng `git worktree` tại commit
      `0351698` [ngay trước khi có middleware chaos] cho kết quả fail giống hệt. Đây là hành vi có
      chủ đích của specs/018-cluster-secret-store [`RequiredSecretsValidation`] — test này đòi hỏi
      secret `ConnectionStrings:OrdersDb` được set cục bộ [`dotnet user-secrets set ...`, xem
      `appsettings.Development.json`] mà môi trường CI/sandbox không có sẵn, không liên quan gì tới
      `Features/Chaos/`. Toàn bộ 8/8 test của `ChaosLatencyInjectionMiddlewareTests` PASS.)
- [X] T015 Chạy lại toàn bộ [quickstart.md](./quickstart.md) từ đầu tới cuối (Bước 1–3 + Dọn dẹp) một
      lượt liền mạch, xác nhận thứ tự các bước không phụ thuộc ẩn nào bị bỏ sót giữa US2/US3.
      (**kết quả thực tế**: đã chạy liền mạch trong MỘT phiên không gián đoạn [~10,5 phút]: tiêm độ trễ
      bằng `autocannon` [50 kết nối × 25 s] → import dashboard SLO qua Kibana Saved Objects API, xác nhận
      thật Orders.Api p95 = 3,080 ms / p99 = 3,343 ms → cập nhật bản ghi kết quả → Dọn dẹp [tắt
      `Chaos:AllowLatencyInjection`, xác nhận hết độ trễ]. Circuit breaker vẫn KHÔNG trip dù dùng công cụ
      load-test thật, loại trừ giả thuyết "công cụ tải yếu". Chi tiết:
      [2026-09-14-inject-latency.md](../../docs/dien-tap-chaos-engineering/ket-qua/2026-09-14-inject-latency.md).
      Phần chạy trên Kubernetes của các phiên cũ đã gỡ ở spec 031.)
- [X] T016 Đối chiếu lại `specs/025-chaos-pod-kill-latency/checklists/requirements.md` — xác nhận mọi
      mục vẫn PASS sau khi có kết quả triển khai thật (theo đúng tiền lệ "Cập nhật sau khi triển
      khai" của `specs/021-declare-service-slos/plan.md`), cập nhật `plan.md` nếu triển khai thật
      phát hiện sai lệch so với Technical Context/Constitution Check đã viết trước.
      (**kết quả thực tế**: đã đối chiếu — mọi mục vẫn PASS [checklist đánh giá CHẤT LƯỢNG ĐẶC TẢ, không
      phải kết quả triển khai]. `plan.md`/Constitution Check KHÔNG cần sửa: phát hiện "circuit breaker
      không trip" là một quan sát về ACCEPTANCE CRITERIA của SCRUM-34 [đáng đưa vào bug/thảo luận ticket
      riêng], không phải sai lệch trong quyết định kỹ thuật của plan.md [research.md Quyết định 1: dùng
      `Task.Delay` không chặn thread là quyết định CÓ CHỦ ĐÍCH để không làm sập hạ tầng khi diễn tập].)

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

### MVP First (User Story 2)

1. Bỏ qua Phase 1–2 (không có task).
2. Phase 3 (User Story 1, kill-pod) đã gỡ ở spec 031.
3. Hoàn thành Phase 4 (User Story 2) — test trước (T004, FAIL) → middleware (T005–T008, PASS) → xác
   nhận trên hạ tầng thật (T009).
4. **DỪNG VÀ XÁC NHẬN**: bài tập tiêm độ trễ đã chạy được và quan sát đúng như Acceptance Criteria
   của SCRUM-34 — đây là giá trị cốt lõi còn lại của Jira ticket.

### Incremental Delivery

1. (Đã gỡ ở spec 031) User Story 1 — kill-pod.
2. User Story 2 → viết test, thêm middleware, xác nhận dashboard SLO tiêu hao (MVP nửa còn lại).
3. User Story 3 → thêm mẫu/README, điền bản ghi kết quả cho (các) lần chạy ở bước 2 (hoàn thiện
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
- Không sửa `services/baskets`, BFF, hay bất kỳ manifest triển khai nào ở bất kỳ task nào trong danh
  sách này (khớp Constraints của plan.md).
