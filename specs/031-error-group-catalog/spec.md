# Feature Specification: Danh mục 8 nhóm lỗi để luyện troubleshoot (tiêm bằng cấu hình trên Docker Compose, lỗi có chủ đích và lỗi bất ngờ, gợi ý theo mức)

**Feature Branch**: `feature/031-error-group-catalog`

**Created**: 2026-10-05

**Status**: Draft

**Input**: User description: "Danh mục 8 nhóm lỗi để luyện troubleshoot, tiêm chỉ bằng cấu hình trên Docker Compose: tách 'loại lỗi' khỏi 'nơi tiêm', mở rộng scripts/incident-drill.ps1 của 028 cho cả lỗi có kịch bản theo nhóm và lỗi bất ngờ không báo trước, thêm lệnh gợi ý theo mức, gỡ phần Kubernetes của 025. (Spec C trong đợt rà soát nợ kỹ thuật; làm song song được với spec A/B; tài liệu troubleshoot chi tiết thuộc spec D.)"

## Clarifications

### Session 2026-10-05 (phiên rà soát nợ kỹ thuật — đã chốt trước khi viết spec)

- Q: Có những nhóm lỗi nào? → A: Tám nhóm — (1) đích kết nối sai (loại A của 028); (2) nghẽn và lỗi theo tỷ lệ (loại B cạn pool của gateway, loại C 5xx bằng header của 027); (3) độ trễ (header `X-Chaos-Latency-Ms` của 025, chỉ Orders.Api); (4) phụ thuộc hạ tầng dừng; (5) xác thực hỏng; (6) thiếu tài nguyên; (7) mạng đứt; (8) container chết hoặc khởi động lại (thay cho kill-pod của 025).
- Q: Giữ ràng buộc nào của 028? → A: KHÔNG sửa code service (cả ServiceDefaults lẫn code nghiệp vụ); chỉ cấu hình/tham số sai hoặc công cụ Docker bên ngoài.
- Q: Có mấy dạng tiêm? → A: Hai — (a) lỗi có chủ đích theo nhóm, có kịch bản (chọn nhóm cụ thể); (b) lỗi bất ngờ không báo trước (bốc thăm mù). Dạng (b) MỞ RỘNG `scripts/incident-drill.ps1`, giữ nguyên `-Start`, `-Reveal`, `-Load`.
- Q: Gợi ý cho dạng (b) thế nào? → A: Cả hai: lệnh script theo mức (không in đáp án trước mức cuối, mỗi lần mở được ghi lại) và tài liệu gợi ý sắp theo triệu chứng (tài liệu chi tiết thuộc spec D).
- Q: Tách "loại lỗi" khỏi "nơi tiêm" thế nào? → A: Danh mục nhóm lỗi mô tả độc lập với Docker Compose; phần thực thi trên Compose là một bộ chuyển đổi riêng, để sau này thêm bộ chuyển đổi cho CD/Kubernetes. Trước mắt chỉ Docker Compose (`docker-compose.local.yml`).
- Q: Gỡ Kubernetes thế nào? → A: CHỈ gỡ phần Kubernetes của 025 (kịch bản kill-pod bằng kubectl và hướng dẫn k8s trong tài liệu 025). GIỮ NGUYÊN `deploy/ansible` vì 018, 019, CI (Jenkinsfile, `scripts/ci/lint-deployment-manifests.sh`), `tests/DeploymentManifestConventionTests` và hiến chương ("Kubernetes, provisioned through Ansible") vẫn dùng.
- Q: Lỗi do diễn tập có tính vào ngân sách lỗi không? → A: Có, như sự cố thật (sau spec A là ngân sách tuần).
- Q: Có viết test tự động cho script mở rộng không? → A: Không (người dùng chốt); plan phải ghi sai lệch Nguyên tắc III kèm lý do và thời hạn mới.
- Q: Elastic xử lý thế nào sau khi xong? → A: Sẽ clean toàn bộ sau khi triển khai xong spec này; PHẢI hỏi lại trước khi xoá.

### Session 2026-10-05 (phiên `/speckit-specify`)

- Q: Làm ở đâu và có commit không? → A: Tạo nhánh mới `feature/031-error-group-catalog` trong worktree hiện tại; KHÔNG commit, người dùng tự commit.
- Q: Danh mục nhóm lỗi nằm ở đâu? → A: Một file dữ liệu riêng (mô tả nhóm, loại, tham số, triệu chứng, nội dung gợi ý), cộng một bộ chuyển đổi Compose nằm trong script. Bộ chuyển đổi Compose là phần duy nhất biết đến Docker Compose.
- Q: Dạng (a) "có kịch bản" khác chế độ không mù hiện có thế nào? → A: Một nhóm, một lệnh, có thời lượng tự gỡ TUỲ CHỌN (hết thời lượng thì script tự khôi phục); nếu không đặt thời lượng thì giữ lỗi cho tới khi khôi phục. KHÔNG có kịch bản nhiều bước/chuỗi trong phạm vi này.
- Q: Cờ cho các nhóm mới? → A: Dùng chung `CHAOS_ALLOW_FAULT_INJECTION` cho mọi nhóm (script từ chối chạy khi cờ này không bật); giữ riêng `CHAOS_ALLOW_LATENCY_INJECTION` của 025 vì middleware của 025 tự kiểm tra cờ đó. Không thêm cờ mới, không sửa code service.
- Q: Khôi phục chuẩn cho các nhóm không qua file override? → A: Lệnh riêng `-Restore -RunId <id>` cho MỌI nhóm (kể cả A/B/C của 028, vốn vẫn gỡ được bằng chạy lại compose như 028); "đã gỡ" = lệnh khôi phục chạy xong và container đích healthy. Mốc "giải quyết" theo 028 vẫn cần SLO liên tục 15 phút.
- Q: Che giấu dạng (b) với nhóm không tạo lại container? → A: Không che; chấp nhận `docker ps`/trạng thái container có thể lộ đích và ghi thành giới hạn đã biết (technical-debt, QA_Debt).
- Q: Lệnh `-Hint`? → A: 3 mức, nội dung lấy từ file danh mục, ghi `hint-log.json` cạnh `sealed.json` (thời điểm + mức mỗi lần mở). KHÔNG ghi vào Kibana Case, KHÔNG ảnh hưởng các mốc thời gian của bản ghi sự cố.
- Q: Tên gọi nhóm/loại? → A: Nhóm đánh số 1–8 là lớp cha; giữ mã A/B/C cho 3 loại cũ của 028 và thêm mã loại mới: nhóm 3 = D (trễ), nhóm 4 = E (hạ tầng dừng), nhóm 5 = F (xác thực hỏng), nhóm 6 = G (thiếu tài nguyên), nhóm 7 = H (mạng đứt), nhóm 8 = I (container chết). Người dùng đã đồng ý ánh xạ này.
- Q: Tham số nhóm 3 (D, trễ)? → A: Chỉ Orders.Api; độ trễ 2000 ms, tỷ lệ request mang header 5–50% như loại C (người dùng chốt cả hai giá trị, vốn là "ví dụ" trong phương án).
- Q: Tham số nhóm 4 (E, hạ tầng dừng)? → A: Chỉ 5 DB (dừng bằng `docker stop`). BỎ Redis và RabbitMQ khỏi phạm vi vì đã kiểm compose: không service nào dùng Redis, còn RabbitMQ chỉ orders-api dùng khi `ORDERS_RABBITMQ_CONNECTION` được đặt (mặc định rỗng) — dừng hai thành phần này không gây triệu chứng đo được. Đây là thu hẹp so với yêu cầu gốc ("DB, Redis hoặc RabbitMQ"), người dùng đã chọn.
- Q: Tham số nhóm 5 (F, xác thực hỏng)? → A: Ban đầu cả hai biến thể; sau khi đo ở `/speckit-implement` (V3) chỉ còn `Identity__Authority` sai cho MỘT service bốc thăm trong 6 service dùng nó (products, baskets, orders, parties, bff, gateway). Biến thể dừng identity-api đã BỎ (xem Clarifications phiên `/speckit-implement`).
- Q: Tham số nhóm 6 (G, thiếu tài nguyên)? → A: `--cpus` 0.1 và `--memory` 96m, áp cho một trong 7 service app (người dùng chốt cả hai giá trị, vốn là "ví dụ"). Compose hiện không đặt giới hạn nào nên mức gốc là không giới hạn. Nếu 96m làm container bị OOM-kill thì thành triệu chứng của nhóm 8 và phải mở lại thảo luận khi đo.
- Q: Tham số nhóm 7 (H, mạng đứt) và 8 (I, container chết)? → A: H: tách MỘT trong 7 service app khỏi network `backbone` (compose chỉ có network này, mọi container nằm chung). I: `docker kill` MỘT trong 7 service app; đã kiểm compose: 7 service app KHÔNG có `restart:` nên không tự sống lại — chỉ container migrate có `restart: on-failure:3`.
- Q: Gỡ phần Kubernetes của 025 đến đâu? → A: Gỡ sạch — sửa cả spec, plan, research, data-model, tasks, quickstart, README, mẫu kết quả của 025 để không còn kịch bản kill-pod/hướng dẫn k8s, và xoá các bản ghi kết quả kill-pod (`ket-qua/2026-09-12-kill-pod.md`, `ket-qua/2026-09-14-kill-pod.md`). Giữ phần tiêm độ trễ của 025 (bản ghi `2026-09-14-inject-latency.md` giữ nguyên).
- Q: Tài liệu và Postman đi kèm? → A: Đủ bộ như 028: tài liệu PO, QA, Architect, 3 sơ đồ drawio, folder Postman mới theo nếp QA 008+ (thủ công điều khiển bằng `.env`/compose + Postman), cập nhật `technical-debt.md`, `QA_Debt.md`, `functional-debt.md`. Tên file PO: `docs/PO/031_PO_danh mục 8 nhóm lỗi luyện troubleshoot.md` (người dùng đồng ý đề xuất).

### Session 2026-10-05 (phiên `/speckit-plan`)

- Q: Tên file danh mục và cú pháp lệnh mới? → A: Danh mục `scripts/incident-drill/catalog.json`; lệnh `-Inject -Type <A–I> [-Group <1–8>] [-Target <đích>] [-DurationSeconds <n>]`, `-Restore -RunId <id>`, `-Hint -RunId <id> -Level 1|2|3`. Giữ nguyên `-Start`, `-Reveal`, `-Load` và chế độ không mù cũ (`-Start -Service -FaultType`, bất biến 11 của 028) — `-Inject` là đường mới cho dạng (a), hai đường song song. (Người dùng duyệt bộ tên đề xuất; các tên này vốn là ví dụ.)
- Q: Gỡ phần Kubernetes của 025 thêm đến đâu ngoài danh sách FR-016? → A: Gỡ thêm cả bốn nhóm: `docs/development/025_*.md`, `docs/spec-summary-vi/025-*.json`, `docs/diagrams/025-*.drawio`, và các câu/mục kill-pod trong `docs/QA/QA_Debt.md` + `docs/architecture/technical-debt.md` (giữ phần tiêm độ trễ). Các mục của 018/019 và ADR-0007 vẫn là hạ tầng k8s thật nên GIỮ.
- Q: Folder Postman cho nhóm mới? → A: MỘT folder `31 - Danh mục nhóm lỗi` có 6 subfolder D–I; mỗi subfolder có request gây triệu chứng và request kiểm khôi phục. Tải nền (folder 26/28) giữ nguyên.
- Q: Bốn hành vi giao cho plan? → A: Duyệt cả bốn: (1) xin `-Hint` bỏ cách mức thì cho phép và ghi đúng mức đã xin; (2) còn lần chạy chưa khôi phục thì từ chối `-Start`/`-Inject` mới; (3) khôi phục nhóm G và I bằng tạo lại container đích từ compose (`up -d --force-recreate --no-deps <svc>`); (4) khôi phục nhóm H bằng `docker network connect` kèm alias gốc rồi chờ healthy.
- Q: Thời hạn của sai lệch Nguyên tắc III (không test tự động)? → A: Tới khi spec D (tài liệu troubleshoot chi tiết) hoàn tất; ghi vào `technical-debt.md`.
- Q: `-Restore` chờ container đích healthy tối đa bao lâu? → A: 10 phút (người dùng chốt; vốn là "ví dụ" trong phương án, phủ nhiễu khởi động nguội 5–7 phút của QA_Debt). Quá hạn thì báo thất bại, không tự thử lại.

### Session 2026-10-05 (phiên `/speckit-tasks`)

- Q: Duyệt quy tắc bốc thăm mù mới? → A: Duyệt đúng đề xuất: nhóm đều 1/8 → loại đều trong nhóm (nhóm 2: B hoặc C mỗi loại 1/2) → đích đều trong các đích áp dụng → tham số → độ trễ 0–30 phút. Chế độ không mù cũ không đổi.
- Q: Route nhận header của loại D? → A: Gửi qua gateway có token, route thật `GET :5300/bff/orders/{guid}` (header phải đi xuyên gateway → BFF → orders). Kèm điểm xác minh V9 — kết quả V9: header KHÔNG tới orders; đã thay bằng gửi thẳng orders-api (xem phiên `/speckit-implement`).
- Q: Văn bản 3 mức gợi ý cho 9 loại? → A: Duyệt phong cách đề xuất (mức 1 triệu chứng, mức 2 tên nhóm, mức 3 đáp án có dấu giữ chỗ); Claude viết cả 27 đoạn rồi DỪNG ở một task kiểm để người dùng duyệt trước khi coi xong.

### Session 2026-10-05 (phiên `/speckit-implement`)

- Q: Header `X-Chaos-Latency-Ms` không đi xuyên gateway/BFF tới orders (V9: qua gateway 404 sau 9 ms; BFF dựng request riêng) — route nào cho loại D? → A: Gửi thẳng `GET :5041/orders/{guid}` kèm token và `X-Chaos-Latency-Ms`, thêm header `X-Tenant-Id: contoso` (đo: 404 sau 2024 ms; không có `X-Tenant-Id` thì 500 `MissingTenantContext` — lỗi giả, không dùng). Thay cho quyết định route qua gateway ở phiên `/speckit-tasks`.
- Q: Nhóm G với `--cpus 0.1` + `--memory 256m` cho triệu chứng nhẹ dưới `-Load` (p95 ≈ 102 ms, p99 ≈ 198 ms, nền ≈ 50 ms; chưa vượt ngưỡng 150 ms; `--cpus 0.05` cho 34–951 ms; `--cpus 0.02` gần như chết) — đổi không? → A: Giữ `--cpus 0.1` và ghi giới hạn (triệu chứng nhẹ, chưa vượt SLO dưới tải nền hiện tại).
- Q: `--memory` 96m làm products-api bị OOM-kill ngay (V4: exit 137, 504 qua gateway) — mức nào cho nhóm G? → A: `--cpus 0.1`, `--memory 256m` (kèm `--memory-swap 256m`). Đo ngắn ở 128m/192m/256m đều không OOM; 256m cho độ trễ 95–310 ms (nền ~45 ms). Phải đo lại dưới tải nền ở T029.
- Q: Biến thể F-ii (dừng identity-api) không có triệu chứng trên đường dữ liệu trong 10 phút (V3: sản phẩm 200, đơn 404 như kỳ vọng, độ trễ không đổi; chỉ cấp token/discovery lỗi kết nối) — xử lý thế nào? → A: Bỏ biến thể F-ii; nhóm 5 chỉ còn Authority sai (triệu chứng đo được: 401/502/504). Hệ quả: `identity-api` không phải đích của loại F; ghi giới hạn vào technical-debt.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Danh mục 8 nhóm lỗi độc lập với nơi tiêm (Priority: P1)

Là người đóng vai SRE, tôi muốn một danh mục mô tả 8 nhóm lỗi (loại, tham số, triệu chứng quan sát được, nội dung gợi ý) độc lập với Docker Compose, và một bộ chuyển đổi Compose riêng biết cách biến mỗi loại thành thao tác thật, để sau này thêm CD/Kubernetes chỉ cần thêm bộ chuyển đổi mà không đụng danh mục.

**Why this priority**: Danh mục là nền cho cả hai dạng tiêm, cho `-Hint` và cho tài liệu của spec D. Thiếu nó thì mỗi nhóm lỗi là một đoạn mã riêng lẻ và không thể mở rộng ra môi trường khác.

**Independent Test**: Đọc file danh mục (không cần chạy Docker): xác nhận có đủ 8 nhóm, 9 loại (A–I), mỗi loại có tham số, service/thành phần áp dụng được, triệu chứng và 3 mức gợi ý, và không có tên lệnh docker/compose nào nằm trong danh mục; sau đó xác nhận mỗi loại có đúng một thao tác tiêm và một thao tác khôi phục trong bộ chuyển đổi Compose.

**Acceptance Scenarios**:

1. **Given** danh mục, **When** tôi liệt kê nhóm lỗi, **Then** thấy đúng 8 nhóm và các loại A–I theo ánh xạ đã chốt, mỗi loại ghi rõ thành phần áp dụng được.
2. **Given** một loại lỗi trong danh mục, **When** tôi đọc mô tả, **Then** không có lệnh docker hay biến compose nào trong mô tả; các chi tiết đó chỉ nằm trong bộ chuyển đổi Compose.
3. **Given** một loại không áp dụng được cho service đã chỉ định (ví dụ loại B cho service không phải gateway, loại D cho service không phải Orders.Api), **When** tôi yêu cầu tiêm, **Then** script từ chối kèm lý do và không thay đổi gì.

---

### User Story 2 - Tiêm lỗi có chủ đích theo nhóm (dạng a) để luyện từng nhóm (Priority: P1)

Là người đóng vai SRE, tôi muốn chọn một nhóm lỗi (và đích nếu cần) rồi tiêm bằng một lệnh, tuỳ chọn đặt thời lượng để script tự khôi phục, để luyện troubleshoot đúng nhóm mình đang cần mà không phải chờ bốc thăm.

**Why this priority**: Đây là cách luyện có kiểm soát và là cách QA kiểm chứng từng nhóm; không có nó thì 6 nhóm mới không kiểm chứng được độc lập.

**Independent Test**: Bật cờ tiêm lỗi, chạy tải nền; với mỗi loại A–I, chạy một lệnh tiêm có chủ đích vào một đích hợp lệ, xác nhận triệu chứng đo được trong telemetry hoặc trạng thái container; chạy lệnh khôi phục và xác nhận container đích healthy và triệu chứng biến mất.

**Acceptance Scenarios**:

1. **Given** cờ `CHAOS_ALLOW_FAULT_INJECTION` bật, **When** tôi chạy lệnh tiêm có chủ đích cho một nhóm và đích hợp lệ, **Then** lỗi được tiêm đúng bằng cấu hình hoặc công cụ Docker, không sửa file đã commit, và script in rõ đã tiêm gì (khác dạng mù).
2. **Given** tôi đặt thời lượng tự gỡ, **When** hết thời lượng, **Then** script tự khôi phục và container đích healthy; nếu không đặt thời lượng thì lỗi giữ nguyên cho tới khi tôi chạy lệnh khôi phục.
3. **Given** cờ `CHAOS_ALLOW_FAULT_INJECTION` tắt, **When** tôi chạy lệnh tiêm, **Then** script từ chối chạy và không có thay đổi nào.
4. **Given** nhóm 3 (độ trễ), **When** tôi tiêm, **Then** script yêu cầu cờ `CHAOS_ALLOW_LATENCY_INJECTION` của 025 cũng đang bật; nếu tắt thì từ chối và nêu rõ cờ nào thiếu.

---

### User Story 3 - Lỗi bất ngờ không báo trước (dạng b) trên 8 nhóm (Priority: P1)

Là người đóng vai SRE, tôi muốn `-Start` (bốc thăm mù) bốc được từ cả 8 nhóm, niêm phong lựa chọn bằng SHA-256 như 028 và chỉ cho mở khi sự cố đã giải quyết, để diễn tập sự cố thật với phạm vi lỗi rộng hơn ba loại ban đầu.

**Why this priority**: Đây là phần mở rộng trực tiếp của 028 và là mục tiêu luyện chính; nhưng phụ thuộc danh mục (Story 1) nên đứng sau về trình tự làm.

**Independent Test**: Chạy `-Start` nhiều lần (ví dụ 20 lần trong một ngày, có khôi phục giữa các lần) và xác nhận lựa chọn bốc ra phủ nhiều nhóm; mỗi lần chỉ in runId và mã băm; `-Reveal` đối chiếu đúng mã băm.

**Acceptance Scenarios**:

1. **Given** cờ bật và tải nền đang chạy, **When** tôi chạy `-Start` không tham số, **Then** script bốc ngẫu nhiên một nhóm, loại hợp lệ, đích, tham số và thời điểm 0–30 phút, niêm phong và chỉ in runId và mã băm.
2. **Given** sự cố nhóm không tạo lại container (E, G, H, I), **When** tôi xem `docker ps`, **Then** trạng thái container có thể lộ đích; đây là giới hạn đã biết, không phải lỗi của script.
3. **Given** sự cố đã được giải quyết, **When** tôi chạy `-Reveal`, **Then** script kiểm mã băm, in lựa chọn, thời điểm tiêm thực tế và nhật ký các lần mở gợi ý.
4. **Given** các lệnh `-Start`, `-Reveal`, `-Load` và chế độ không mù của 028, **When** tôi chạy lại các lệnh đó theo hợp đồng 028, **Then** hành vi cũ không đổi, ngoại trừ phần hợp đồng đã được sửa có chủ đích trong spec này.

---

### User Story 4 - Gợi ý theo mức cho dạng (b) (Priority: P2)

Là người đóng vai SRE đang bế tắc, tôi muốn xin gợi ý ba mức tăng dần cho một lần diễn tập mà không bị lộ đáp án trước mức cuối, và mỗi lần xin được ghi lại, để biết mình đã cần bao nhiêu trợ giúp.

**Why this priority**: Giá trị sau khi dạng (b) chạy được; không chặn các story khác.

**Independent Test**: Với một runId đang chạy, mở lần lượt mức 1, 2, 3; xác nhận mức 1 và 2 không chứa service, tham số hay mã loại, mức 3 mới là đáp án; xác nhận `hint-log.json` có đủ 3 dòng có thời điểm và mức; xác nhận các mốc thời gian của bản ghi sự cố 028 không đổi.

**Acceptance Scenarios**:

1. **Given** một runId đang mở, **When** tôi chạy lệnh gợi ý mức 1, **Then** nhận gợi ý theo triệu chứng, không nêu nhóm, loại, service hay tham số.
2. **Given** tôi xin mức 2, **When** script trả lời, **Then** nêu nhóm lỗi nhưng không nêu service hay tham số cụ thể.
3. **Given** tôi xin mức 3, **When** script trả lời, **Then** nêu đầy đủ đáp án; sau đó `-Reveal` vẫn kiểm mã băm bình thường.
4. **Given** mỗi lần xin gợi ý, **When** tôi mở `hint-log.json`, **Then** có một dòng ghi thời điểm và mức; không có thao tác nào ghi vào Kibana Case.
5. **Given** tôi xin một mức cao hơn khi chưa xin mức thấp hơn, **When** script xử lý, **Then** (hành vi bỏ cách mức do plan quyết định, ghi trong hợp đồng) việc xin vẫn được ghi lại đúng mức đã xin.

---

### User Story 5 - Gỡ phần Kubernetes của 025 (Priority: P2)

Là người bảo trì tài liệu, tôi muốn phần kill-pod/Kubernetes của 025 bị gỡ sạch và thay bằng nhóm 8 (container chết) trên Docker Compose, để tài liệu sống không còn dẫn người đọc tới một cluster mà dự án không còn dùng cho diễn tập chaos, trong khi hạ tầng triển khai Ansible/Kubernetes vẫn nguyên.

**Why this priority**: Dọn nợ tài liệu; độc lập với phần script nhưng cần nhóm 8 có mặt để không mất kịch bản "dịch vụ chết".

**Independent Test**: Tìm trong specs/025-chaos-pod-kill-latency và docs/dien-tap-chaos-engineering không còn `kubectl`, kill-pod hay hướng dẫn cluster; bản ghi kill-pod đã xoá, bản ghi inject-latency còn nguyên; `deploy/ansible`, Jenkinsfile, `lint-deployment-manifests.sh`, `DeploymentManifestConventionTests` không bị thay đổi.

**Acceptance Scenarios**:

1. **Given** tài liệu 025 sau khi sửa, **When** tôi tìm `kubectl`, `kill-pod`, "cluster kind", **Then** không còn kết quả trong specs/025 và docs/dien-tap-chaos-engineering (ngoại trừ chỗ trỏ sang spec 031 nếu có).
2. **Given** thư mục `docs/dien-tap-chaos-engineering/ket-qua/`, **When** tôi liệt kê, **Then** hai bản ghi kill-pod đã bị xoá, bản ghi inject-latency còn nguyên, và README liệt kê lại cho khớp.
3. **Given** toàn bộ repo, **When** tôi so sánh `deploy/ansible/**`, `Jenkinsfile`, `scripts/ci/lint-deployment-manifests.sh` và `tests/DeploymentManifestConventionTests` với master, **Then** không có thay đổi nào.
4. **Given** tài liệu PO/QA/Architect của 025, **When** tôi đọc, **Then** phần mô tả kill-pod trên k8s được thay bằng mô tả nhóm 8 của spec này hoặc bỏ, nhất quán với phần đã gỡ.

---

### User Story 6 - Postman và tài liệu đi kèm theo nếp 027/028 (Priority: P3)

Là QA, tôi muốn có folder Postman cho từng nhóm lỗi mới và bộ tài liệu PO, QA, Architect, ba sơ đồ drawio và các mục nợ cập nhật, theo nếp của 027/028, để kiểm chứng thủ công từng nhóm bằng cấu hình/compose cộng Postman.

**Why this priority**: Cần cho bàn giao nhưng không chặn hành vi của script.

**Independent Test**: Mở bộ tài liệu theo nếp QA 008+ (phần Thủ công trước phần Tự động, kèm bảng liên kết); chạy các folder Postman mới theo hướng dẫn thủ công và xác nhận kỳ vọng.

**Acceptance Scenarios**:

1. **Given** folder Postman mới, **When** tôi chạy theo hướng dẫn QA thủ công cho một nhóm, **Then** quan sát được triệu chứng và sau khi khôi phục quan sát được trạng thái bình thường.
2. **Given** `technical-debt.md`, `QA_Debt.md`, `functional-debt.md`, **When** tôi mở mục 031, **Then** có các giới hạn đã biết (không che đích cho nhóm không tạo lại container, Redis/RabbitMQ ngoài phạm vi, không có test tự động…) và mọi phát hiện nằm trong QA_Debt, không nằm trong tài liệu QA.

---

### Edge Cases

- Nhóm 4 (E) dừng một DB: service sở hữu DB phải báo 503 và vẫn sống (compose ghi rõ nó không có `restart:` để giữ hành vi này); lỗi lan sang BFF/gateway theo chuỗi phụ thuộc, nên triệu chứng xuất hiện ở nhiều service — đây là nhiễu có thật, không phải lỗi.
- Nhóm 5 (F) với `Identity__Authority` sai ở gateway hoặc BFF lan ra mọi route đi qua đó (gateway trả 401 sau ~24 giây, BFF làm gateway trả 504 sau ~10 giây); ở service phía sau thì route qua gateway trả 502. Biến thể dừng identity-api đã bỏ vì không có triệu chứng trên đường dữ liệu (token cũ vẫn dùng được nhờ khoá ký đã cache).
- Nhóm 6 (G): `--memory` 96m làm container bị OOM-kill (đã đo ở V4), nên mức được chốt là 256m; dưới tải nền vẫn phải đo lại và nếu OOM-kill thì hỏi lại người dùng.
- Nhóm 7 (H): tách service khỏi `backbone` làm service mất cả liên lạc với DB lẫn các service khác, và Docker có thể không còn publish được cổng ra máy như cũ; cách khôi phục (kết nối lại network với alias gốc) phải đưa service về trạng thái healthy.
- Nhóm 8 (I): vì không có `restart:`, service bị kill chết hẳn tới khi `-Restore`; tải nền sẽ nhận lỗi kết nối và có thể làm Case bắn nhiều rule.
- Dạng (b) với nhóm không tạo lại container: không còn nhiễu "tạo lại cả 7 container" của 028 che đích; đã chấp nhận (xem Clarifications).
- `-Restore` chạy khi lỗi chưa được tiêm (còn trong thời gian chờ 0–30 phút): không có gì để khôi phục, script phải hủy tiến trình nền đang chờ và báo rõ.
- Tiến trình nền bị dừng giữa chừng (tắt máy, đóng terminal): trạng thái lỗi có thể còn sót; `-Restore -RunId` phải dùng được để dọn dựa trên bản ghi của lần chạy đó.
- Hai lần chạy cùng lúc (hai runId cùng tiêm): ngoài phạm vi; script từ chối bắt đầu khi còn một lần chạy chưa khôi phục (hành vi chính xác do plan chốt).
- Lỗi do diễn tập tiếp tục tiêu hao ngân sách lỗi tuần như sự cố thật (theo 029); không có ngoại lệ.
- Nhóm 3 (D) không dùng được cho service khác Orders.Api vì cơ chế của 025 chỉ có ở đó; nhóm 3 cho service khác ngoài phạm vi.
- Nhóm 1/2 (A/B/C) giữ hành vi của 028: loại B chỉ gateway; loại A/C áp dụng mọi service như 028.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: PHẢI có một danh mục nhóm lỗi dạng dữ liệu, mô tả độc lập với Docker Compose, gồm 8 nhóm và các loại A–I theo ánh xạ đã chốt (A: đích kết nối sai; B: cạn pool gateway; C: 5xx theo tỷ lệ; D: trễ Orders.Api; E: DB dừng; F: xác thực hỏng; G: thiếu tài nguyên; H: mạng đứt; I: container chết), mỗi loại ghi thành phần áp dụng được, tham số, triệu chứng quan sát được và ba mức gợi ý.
- **FR-002**: PHẢI có một bộ chuyển đổi Docker Compose riêng, là nơi duy nhất biết thao tác tiêm và thao tác khôi phục cụ thể của từng loại trên `docker-compose.local.yml`; danh mục KHÔNG chứa lệnh docker hay biến compose.
- **FR-003**: Mọi lỗi PHẢI được tạo chỉ bằng cấu hình/tham số sai hoặc công cụ Docker bên ngoài; KHÔNG sửa code của bất kỳ service nào (cả ServiceDefaults lẫn code nghiệp vụ) và KHÔNG sửa file đã commit để tiêm lỗi.
- **FR-004**: Script PHẢI từ chối chạy tiêm (cả hai dạng) khi `CHAOS_ALLOW_FAULT_INJECTION` trong `.env` không bật; riêng nhóm 3 (D) còn PHẢI yêu cầu `CHAOS_ALLOW_LATENCY_INJECTION` bật và nêu rõ cờ nào thiếu. Không thêm cờ mới.
- **FR-005**: Dạng (a) PHẢI cho chọn một nhóm (hoặc loại) và đích hợp lệ và tiêm bằng một lệnh, in rõ đã tiêm gì, với thời lượng tự gỡ tuỳ chọn; hết thời lượng thì script tự khôi phục. Không có kịch bản nhiều bước.
- **FR-006**: Dạng (b) PHẢI mở rộng `scripts/incident-drill.ps1`: `-Start` bốc ngẫu nhiên từ cả 8 nhóm (loại, đích, tham số, thời điểm 0–30 phút), niêm phong bằng SHA-256 như 028 và chỉ in runId và mã băm; `-Reveal` và `-Load` giữ hành vi của 028.
- **FR-007**: PHẢI có lệnh khôi phục `-Restore -RunId <id>` cho mọi loại A–I: đưa đích về cấu hình/trạng thái gốc; "đã gỡ" khi lệnh chạy xong và container đích healthy. Khi lỗi chưa được tiêm thì hủy tiến trình nền đang chờ.
- **FR-008**: PHẢI có lệnh `-Hint -RunId <id> -Level 1|2|3`: mức 1 là gợi ý theo triệu chứng, mức 2 nêu nhóm lỗi, mức 3 là đáp án đầy đủ; mức 1 và 2 KHÔNG tiết lộ service, tham số hay mã loại; nội dung lấy từ danh mục.
- **FR-009**: Mỗi lần mở gợi ý PHẢI được ghi vào `hint-log.json` cạnh `sealed.json` (thời điểm và mức); `-Reveal` PHẢI in lại nhật ký này; KHÔNG ghi vào Kibana Case và KHÔNG làm đổi các mốc thời gian của bản ghi sự cố.
- **FR-010**: Nhóm 4 (E) PHẢI chỉ áp dụng cho 5 DB, dừng bằng `docker stop`; Redis và RabbitMQ KHÔNG nằm trong phạm vi (không gây triệu chứng đo được).
- **FR-011**: Nhóm 5 (F) PHẢI đặt `Identity__Authority` sai cho một service trong products, baskets, orders, parties, bff, gateway (không có biến thể dừng identity-api; identity-api KHÔNG là đích của nhóm 5).
- **FR-012**: Nhóm 6 (G) PHẢI dùng `--cpus` 0.1 và `--memory` 256m (kèm `--memory-swap` 256m) cho một trong 7 service app; PHẢI đo dưới tải nền xem có OOM-kill không (nếu có thì ghi lại và hỏi lại người dùng).
- **FR-013**: Nhóm 7 (H) PHẢI tách một trong 7 service app khỏi network `backbone` và khôi phục bằng kết nối lại; nhóm 8 (I) PHẢI `docker kill` một trong 7 service app và khôi phục bằng khởi động lại container đó.
- **FR-014**: Nhóm 3 (D) PHẢI chỉ áp dụng cho Orders.Api, độ trễ 2000 ms, tỷ lệ request mang header `X-Chaos-Latency-Ms` chọn ngẫu nhiên 5–50% (cùng cách tính tốc độ gửi như loại C).
- **FR-015**: Script PHẢI từ chối loại không áp dụng được cho đích (B ngoài gateway, D ngoài Orders.Api, F ngoài 6 service dùng Authority — kể cả identity-api…) kèm lý do và không thay đổi gì.
- **FR-016**: PHẢI gỡ sạch phần Kubernetes của 025: sửa specs/025 (spec, plan, research, data-model, tasks, quickstart, contracts, checklist), `docs/dien-tap-chaos-engineering/` (README, mẫu kết quả), PO/QA/Architect/Development của 025, `docs/spec-summary-vi/025-*.json`, `docs/diagrams/025-*.drawio` và các câu/mục kill-pod trong `QA_Debt.md` + `technical-debt.md` để không còn kịch bản kill-pod hay hướng dẫn k8s (giữ phần tiêm độ trễ; mục 018/019 và ADR-0007 giữ nguyên); xoá `ket-qua/2026-09-12-kill-pod.md` và `ket-qua/2026-09-14-kill-pod.md`; giữ `ket-qua/2026-09-14-inject-latency.md`. Nhóm 8 thay cho kill-pod.
- **FR-017**: KHÔNG được thay đổi `deploy/ansible/**`, `Jenkinsfile`, `scripts/ci/lint-deployment-manifests.sh`, `tests/DeploymentManifestConventionTests` hay hiến chương.
- **FR-018**: Khi cờ tiêm lỗi tắt, các thay đổi của tính năng này KHÔNG được thay đổi hành vi phản hồi hiện có của bất kỳ endpoint nào; cấu hình mặc định của compose (`.env.example`) không đổi hành vi.
- **FR-019**: Hợp đồng script của 028 (`incident-drill-script-contract.md`) PHẢI được cập nhật bởi hoặc tham chiếu từ spec này cho các lệnh `-Hint`, `-Restore`, dạng (a) và mã loại D–I; hành vi cũ không đổi ngoài phần đã sửa có chủ đích.
- **FR-020**: PHẢI có folder Postman `31 - Danh mục nhóm lỗi` với 6 subfolder D–I (theo nếp QA 008+, thủ công điều khiển bằng `.env`/compose + Postman) và bộ tài liệu theo nếp 027/028: PO, QA, Architect, 3 sơ đồ drawio, cập nhật `technical-debt.md`, `QA_Debt.md`, `functional-debt.md`; mọi phát hiện chỉ nằm trong QA_Debt.
- **FR-021**: Lỗi do diễn tập (mọi nhóm) PHẢI được tính vào ngân sách lỗi như sự cố thật; KHÔNG được loại trừ khỏi phép tính ngân sách.

### Key Entities *(include if feature involves data)*

- **Nhóm lỗi**: một trong 8 nhóm; mô tả loại lỗi độc lập với nơi tiêm; có một hoặc nhiều loại.
- **Loại lỗi**: một mã A–I thuộc một nhóm; có thành phần áp dụng được, tham số, triệu chứng và ba mức gợi ý.
- **Danh mục**: file dữ liệu liệt kê nhóm, loại, tham số, triệu chứng, gợi ý; không chứa lệnh docker.
- **Bộ chuyển đổi Compose**: thành phần thực thi tiêm và khôi phục từng loại trên Docker Compose; thay thế được bằng bộ chuyển đổi khác.
- **Lần chạy (runId)**: một lần tiêm (dạng a hoặc b) với thư mục `.incident-drill/<runId>/` chứa lựa chọn niêm phong, mã băm, thời điểm tiêm, nhật ký gợi ý.
- **Nhật ký gợi ý**: `hint-log.json` cạnh `sealed.json`; mỗi dòng có thời điểm và mức đã mở.
- **Folder Postman nhóm lỗi**: tập request kiểm chứng thủ công từng nhóm theo nếp QA 008+.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Mỗi loại trong 9 loại A–I được tiêm thành công ít nhất một lần vào một đích hợp lệ, gây triệu chứng đo được trong telemetry hoặc trạng thái container, và khôi phục về bình thường (container đích healthy) bằng `-Restore`.
- **SC-002**: 100% lần tiêm bị từ chối khi cờ `CHAOS_ALLOW_FAULT_INJECTION` tắt (và khi cờ latency tắt với nhóm 3), không có thay đổi nào về container hay cấu hình.
- **SC-003**: Qua một chuỗi ít nhất 20 lần `-Start` mù (khôi phục sau mỗi lần), lựa chọn phủ được ít nhất 6 trong 8 nhóm; mỗi lần đầu ra chỉ có runId và mã băm; 100% lần `-Reveal` đối chiếu đúng mã băm.
- **SC-004**: Gợi ý mức 1 và mức 2 không chứa tên service, tham số hay mã loại trong 100% trường hợp kiểm; mức 3 chứa đầy đủ đáp án; mỗi lần mở có đúng một dòng trong `hint-log.json`.
- **SC-005**: Sau khi gỡ, không còn chuỗi `kubectl`, kill-pod hay hướng dẫn cluster trong specs/025 và docs/dien-tap-chaos-engineering; các file ansible, Jenkinsfile, script lint và test quy ước manifest không đổi so với master (so sánh diff trống).
- **SC-006**: Khi cờ tiêm lỗi tắt, mọi endpoint phản hồi như trước và không cảnh báo nào bắn do tính năng này.
- **SC-007**: Với mỗi nhóm mới, QA chạy được hướng dẫn thủ công của folder Postman tương ứng và quan sát đúng kỳ vọng ghi trong tài liệu QA.

## Assumptions

- Ràng buộc chung đã kế thừa: KHÔNG sửa code service; chỉ cấu hình/tham số sai hoặc công cụ Docker bên ngoài (028).
- Tải nền (`-Load`), niêm phong SHA-256, `-Reveal` và các bất biến của 028 giữ nguyên; spec này mở rộng chứ không thay thế (trừ phần hợp đồng nêu ở FR-019).
- Telemetry, dashboard Xử lý sự cố, rule phát hiện nhanh và bốn rule ngân sách lỗi của 021/027/028/029/030 đã có và không đổi; spec này không thêm rule hay dashboard mới.
- Ngân sách lỗi tính theo tuần lịch giờ Việt Nam (spec 029); lỗi diễn tập tiếp tục được tính như sự cố thật.
- Các giá trị 2000 ms, 5–50% (nhóm 3) và `--cpus` 0.1 (nhóm 6) đã được người dùng chốt trong phiên này (trước đó là "ví dụ" trong phương án); `--memory` 96m còn phải đo ở plan/implement và có thể mở lại nếu gây OOM-kill (FR-012).
- Không viết test tự động cho script mở rộng (người dùng chốt); plan PHẢI ghi sai lệch Nguyên tắc III kèm lý do và thời hạn mới.
- Spec D (tài liệu troubleshoot chi tiết) tồn tại riêng; spec này chỉ cung cấp nội dung gợi ý trong danh mục và các tài liệu theo nếp 027/028, không viết tài liệu troubleshoot chi tiết.
- Spec A và B có thể làm song song; spec này không phụ thuộc kết quả của chúng ngoài định nghĩa ngân sách tuần của 029.
- Elastic sẽ được dọn sạch sau khi triển khai xong spec này; việc xoá PHẢI được hỏi lại trước khi thực hiện và nằm ngoài phạm vi của các nhiệm vụ trong spec này.
- Bộ chuyển đổi cho CD/Kubernetes nằm ngoài phạm vi; kiến trúc chỉ cần để ngỏ chỗ thêm sau.
- Không che giấu đích của dạng (b) với nhóm không tạo lại container; ghi thành giới hạn đã biết (technical-debt, QA_Debt).
- Redis và RabbitMQ không là đích của nhóm 4 vì compose hiện không có service nào dùng Redis, và RabbitMQ chỉ được orders-api dùng khi đặt `ORDERS_RABBITMQ_CONNECTION` (mặc định rỗng).
- Tên file PO đã chốt: `docs/PO/031_PO_danh mục 8 nhóm lỗi luyện troubleshoot.md` (người dùng đồng ý đề xuất, vốn là ví dụ). Tên file QA và Architect theo cùng phần tên: `docs/QA/031_QA_danh mục 8 nhóm lỗi luyện troubleshoot.md`, `docs/architecture/031_Architect_danh mục 8 nhóm lỗi luyện troubleshoot.md`.
- Hành vi chi tiết khi xin gợi ý bỏ cách mức, khi hai lần chạy đồng thời, và định dạng file danh mục cụ thể là quyết định thiết kế của plan (ghi vào hợp đồng), không làm đổi phạm vi spec.
