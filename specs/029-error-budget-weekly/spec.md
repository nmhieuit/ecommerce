# Feature Specification: Ngân sách lỗi theo tuần lịch giờ Việt Nam (thay thế tháng lịch của đặc tả 027)

**Feature Branch**: `feature/029-error-budget-weekly`

**Created**: 2026-10-05

**Status**: Draft

**Input**: User description: "Ngân sách lỗi theo tuần lịch giờ Việt Nam, thay thế ngân sách tháng lịch của spec 027 (spec A trong đợt rà soát nợ kỹ thuật dashboard/tiêm lỗi). [...] Đọc lại cách làm của spec 027 và 028 [...] và làm tương tự các task cần thiết. [...] Không suy diễn những điểm trên; mọi câu trả lời ghi vào mục Clarifications của spec.md."

## Clarifications

### Đã chốt trước trong phiên rà soát nợ kỹ thuật (người dùng nêu sẵn trong mô tả, không hỏi lại)

- Chu kỳ ngân sách là **tuần lịch giờ Việt Nam**: từ thứ Hai 00:00 tới Chủ nhật 23:59, UTC+7. Chu kỳ này **thay thế hoàn toàn** tháng lịch của đặc tả 027 và áp dụng cho cả 4 ngân sách (khả dụng, tỷ lệ lỗi 5xx, độ trễ p95, độ trễ p99).
- Hiến chương được sửa bằng PR amendment, đổi dòng "99.9% monthly availability" sang chu kỳ tuần. Con số cũng đổi, xem phiên 2026-10-05 bên dưới.
- Giữ nguyên mọi quy tắc khác của 027:
  - các mốc cảnh báo 50%/75%/100%;
  - "cạn" = bất kỳ ngân sách nào đạt 100%;
  - hệ quả khi cạn: dừng merge tính năng mới;
  - hồi phục khi đạt SLO 3 ngày liên tiếp, ngày không có traffic tính là đạt;
  - đặt lại ngân sách đầu kỳ không gỡ trạng thái đóng băng.
- Giữ cách lưu bằng file export ndjson trong repo. `ErrorBudgetPolicyTests` và `ErrorBudgetRuleDefinitionTests` được sửa theo chu kỳ tuần.
- Lỗi do diễn tập (025/027/028) tiếp tục tính vào ngân sách như sự cố thật.
- Dashboard: phần ngân sách chỉ dùng cửa sổ tuần lịch. Việc tách dashboard thuộc spec B (làm sau spec này), **không** làm trong spec này.
- Elastic: người dùng đã xoá volume. Sau khi triển khai xong spec này sẽ dọn toàn bộ Elastic, nhưng **phải hỏi lại người dùng trước khi xoá**.

### Session 2026-10-05

- Q: PR sửa hiến chương nằm trong spec này hay là việc riêng làm trước? → A: Trong spec này. Sửa `constitution.md` là một task trong tasks.md của spec này và đi cùng thay đổi manifest/rule.
- Q: Phiên bản hiến chương tăng MAJOR hay MINOR? → A: MAJOR, từ `1.0.0` lên `2.0.0`.
- Q: Câu chữ mới cho dòng availability? → A: Ban đầu chọn dạng ngắn gọn "99.9% weekly availability.". Sau khi chọn nới tỷ lệ và sửa mặc định hiến chương (hai câu hỏi dưới), hai dòng của Nguyên tắc VIII thành:
  - "- 99% weekly availability."
  - "- 5xx responses below 1% of requests."
- Q: Tỷ lệ cho phép của 4 ngân sách giữ nguyên hay đổi khi chu kỳ ngắn lại? → A: Nới rộng ×10, chỉ cho hai ngân sách 5xx và khả dụng. Kết quả: khả dụng 1%, 5xx 1%; độ trễ giữ p95 5%, p99 1%.
- Q: Tỷ lệ ngân sách đã nới quan hệ thế nào với SLO đã khai báo trong manifest? → A: Đổi luôn SLO, để ngân sách vẫn bằng 1 − SLO. SLO khả dụng thành 99%, SLO 5xx thành dưới 1%. Ngưỡng độ trễ p95/p99 của từng service giữ nguyên.
- Q: SLO mới đưa vào bằng cách sửa mặc định hiến chương hay để mỗi manifest ghi ngoại lệ? → A: Sửa mặc định của hiến chương, nên 7 manifest không cần lý do ngoại lệ. Các mặc định nền tảng trong test của 021 cũng sửa theo.
- Q: Rule đóng băng `error-budget-frozen` đang nhìn lại 62 ngày; với chu kỳ tuần thì bao nhiêu? → A: 14 ngày (2 tuần).
- Q: Ba rule mốc `error-budget-50/75/100` có cửa sổ rule 31 ngày; với tuần lịch thì đổi thế nào? → A: 7 ngày.
- Q: Ngưỡng 5xx của rule `incident-fast-detection` (028, đang ≥ 0.1% trong 5 phút) và ngưỡng "ngày đạt SLO" của rule đóng băng có đổi theo SLO mới không? → A: Đổi cả hai theo SLO. `incident-fast-detection` bắn khi 5xx ≥ 1%; rule đóng băng xét một ngày là đạt SLO khi 5xx < 1%. Ngưỡng độ trễ và quy tắc "gateway chỉ xét 5xx" của 028 giữ nguyên.
- Q: Chu kỳ chạy rule (027 đang 5 phút) giữ hay đổi? → A: Giữ 5 phút, cho cả 4 rule ngân sách.
- Q: Lưu lượng thấp ở môi trường local (QA_Debt 027: 1–2 lỗi đã vượt mốc) với chu kỳ tuần còn nhạy hơn, có cần xử lý gì không? → A: Chỉ ghi giới hạn, không thêm logic.
- Q: Tuần chuyển tiếp xử lý thế nào; sự kiện "cạn" cũ trong `slo-error-budget-events` giữ hay bỏ? → A: Bắt đầu tính từ thứ Hai 00:00 (giờ Việt Nam) của tuần chứa ngày triển khai. Bỏ sự kiện cũ: không mang dữ liệu, sự kiện "cạn" hay trạng thái đóng băng của tháng sang. Thực tế dữ liệu này đã mất cùng volume Elastic bị xoá.
- Q: Các panel ngân sách của 027 trên dashboard: sửa tạm sang tuần trong spec này hay chờ spec B? → A: Sửa tạm trong spec này. Gồm truy vấn tuần, tiêu đề "tuần này", khoảng thời gian theo cửa sổ mới, và dòng "99.9%/tháng" của panel text thành "99%/tuần". Không tách hay sắp xếp lại dashboard (việc của spec B).
- Q: Tài liệu đi kèm làm những gì? → A: Làm đủ cả bốn nhóm:
  - PO và `functional-debt.md`;
  - QA và `QA_Debt.md`;
  - Architect, `technical-debt.md` và 3 sơ đồ drawio;
  - folder Postman.
- Q: Tên file PO? → A: `docs/PO/029_PO_ngân sách lỗi theo tuần lịch.md`.
- Q: Tên file QA và Architect? → A: Giống PO: `docs/QA/029_QA_ngân sách lỗi theo tuần lịch.md` và `docs/architecture/029_Architect_ngân sách lỗi theo tuần lịch.md`.
- Q: Folder Postman 029 chứa gì? → A: Như folder 027: mỗi service trong 7 service một request gửi header `X-Chaos-Fault: 5xx`.
- Q: Làm việc ở checkout/nhánh nào, có commit không? → A: Nhánh mới `feature/029-error-budget-weekly`, tạo từ master `695bccb`, chuyển ngay trong worktree hiện tại. **Không commit**: người dùng tự xem và commit.
- Q: Giá trị mới của khoá cửa sổ trong chính sách manifest? → A: `calendar-week` (thay `calendar-month`). Ngày bắt đầu tuần (thứ Hai) ghi trong comment và contract, không thêm khoá mới.
- Q: Tên thư mục spec? → A: `specs/029-error-budget-weekly/`.
- Q: Tài liệu và hiện vật hiện có của 027 xử lý thế nào? → A: Sửa tại chỗ tất cả sang tuần lịch và con số mới, gồm:
  - `specs/027/*`;
  - file `07`, `alerts/README.md`;
  - PO/QA/Architect 027 và 3 drawio 027.
- Q: Đổi SLO làm khoảng 45 file của 021/027/028 còn con số cũ; xử lý hiện vật 021 và 028 thế nào? → A: Sửa tại chỗ cả 021, 027 và 028. Nằm ngoài phạm vi sửa vì là lịch sử (người dùng không phản đối điểm này khi được nêu trong câu hỏi):
  - các mục cũ trong `QA_Debt.md`;
  - bản ghi kết quả diễn tập trong `docs/dien-tap-chaos-engineering/ket-qua/`;
  - `specs/002-gateway-bff-routing/`.

### Session 2026-10-05 (phiên `/speckit-plan`)

- Q: Phạm vi test? → A: Sửa giá trị trong test hiện có, và thêm kiểm tra: lọc từ đầu tuần UTC+7 ở 3 rule mốc; cửa sổ rule mốc 7 ngày, rule đóng băng 14 ngày; ngưỡng ngày đạt SLO của rule đóng băng. Không thêm test cho rule 028.
- Q: Khoảng thời gian của 3 panel ngân sách trên dashboard? → A: Cả 3 là `now-7d`.
- Q: Kịch bản đốt ngân sách tuần chạy trên service nào? → A: Cả 7 service, qua folder Postman 029.
- Q: Kiểm chứng ranh giới tuần thế nào? → A: Chạy biểu thức đầu tuần với các thời điểm giả định cố định. Ranh giới thật ghi "chưa quan sát được trong một phiên" như 027.
- Q: Bằng chứng đo thật theo chính sách tháng/0.1% trong tài liệu 027/028 xử lý thế nào? → A: Xoá. Phạm vi gồm:
  - tài liệu vận hành (`07`, `08`, `alerts/README.md`);
  - spec 027/028: mục "Kết quả xác minh" của research, ghi chú kết quả trong tasks, các đoạn kết quả trong quickstart.

  Bằng chứng chỉ còn trong `QA_Debt.md` (giữ nguyên) và lịch sử git. PO/QA/Architect và `functional-debt.md` / `technical-debt.md` chỉ sửa con số và chu kỳ.

### Session 2026-10-05 (phiên `/speckit-tasks`)

- Q: Tên folder Postman 029? → A: `29 - Ngân sách lỗi theo tuần: tiêm 5xx 7 service`.
- Q: Folder 29 chứa gì? → A: Hai phần:
  - 7 request 5xx, mỗi service một request `GET {{<service>Url}}/health/live` kèm `X-Chaos-Fault: 5xx`, kỳ vọng `500` khi cờ bật, chạy theo vòng bằng newman;
  - các truy vấn đọc mức tiêu hao tuần này, alert đang hoạt động và service đang cạn cho cả 7 service (kiểu 27c), chạy riêng, không theo vòng.

  (Người lập kế hoạch đã đính chính: folder 27 hiện chỉ gọi `Orders.Api`, không phải 7 service như mô tả ở phiên `/speckit-specify`.)
- Q: Tiêu đề 3 panel ngân sách? → A: Chỉ đổi "tháng" → "tuần":
  - "Ngân sách lỗi tuần này — mức tiêu hao (%)";
  - "Ngân sách lỗi tuần này — cảnh báo đang hoạt động";
  - panel cạn giữ nguyên;
  - tên saved search mức tiêu hao: "Ngân sách lỗi — mức tiêu hao tuần này (7 service × 4 ngân sách)".
- Q: Tên 3 file drawio? → A: Theo tên thư mục spec: `docs/diagrams/029-error-budget-weekly-component.drawio`, `-flow-nghiep-vu.drawio`, `-sequence.drawio`.

### Session 2026-10-05 (phiên `/speckit-implement`)

- Q: Phase 2–5 chạy trên stack nào, khi stack `ecomerce-local` đang chạy (dựng từ repo chính, có dữ liệu traces từ 03/10 và 21 sự kiện "cạn" của chế độ tháng)? → A: Dùng stack đang chạy. Chỉ xoá riêng index `slo-error-budget-events` rồi tạo lại, không dựng stack mới, không xoá volume.
- Q: Nhãn cột "Error-rate — Thực tế (ngưỡng ≤ 0.1%, cả 7 service)" trong panel "Bảng SLO — 7 service" của 021 (ngoài phần ngân sách) xử lý thế nào? → A: Sửa nhãn thành "ngưỡng < 1%". Đây là **ngoại lệ của FR-010**: chỉ đổi chữ, không đổi công thức hay bố cục.
- Q: Tên hai folder con của folder 29? → A: `29a - Tiêm 5xx 7 service (chạy theo vòng)`, `29b - Trạng thái ngân sách tuần (chạy một lần)`.
- Q: `Parties.Api` đã cạn ngân sách tuần nhưng không bị đóng băng: alert 100 active liên tục từ trước khi sửa rule nên không ghi sự kiện mới, còn sự kiện cũ đã xoá. Xử lý thế nào? → A: Disable rồi Enable rule `error-budget-100` để ghi lại sự kiện.
- Q: Container đang chạy với override của buổi diễn tập 028 lúc 11:29; có chạy đốt ngân sách và tiêm lỗi không? → A: Buổi diễn tập đã xong, chạy ngay. Dùng cờ `CHAOS_ALLOW_FAULT_INJECTION` đã bật sẵn trên container, không tạo lại container.
- Q: Bằng chứng đo thật trong QA 027/028 (cột "Đã quan sát", mục "Kết quả lượt QA") xử lý thế nào? → A: Xoá. Số đo chỉ còn trong QA_Debt.
- Q: `master` đã có PR #69 sửa drawio 027/028 sau commit gốc của nhánh; xử lý thế nào? → A: Đưa `master` vào nhánh trước khi sửa drawio. Thực tế là fast-forward lên `c19ac28`, không tạo commit.
- Q: Đưa cờ tiêm lỗi về mặc định thế nào (cờ có sẵn trong `.env` của repo chính từ buổi diễn tập 028)? → A: Tắt cờ (comment dòng đó) rồi tạo lại 7 container từ `docker-compose.local.yml`, bỏ override diễn tập.
- Q: T054 (chạy lại toàn bộ quickstart từ ndjson) làm thế nào, khi import lại sẽ đặt lại trạng thái alert? → A: Bản rút gọn: so ndjson với Kibana đang chạy, không import lại, không đốt ngân sách lại.
- Q: Dọn Elastic (FR-018)? → A: Xoá volume ES rồi dựng lại, import ndjson, bật rule. Người dùng xác nhận lần hai sau khi được báo sẽ mất 1 Kibana Case của buổi diễn tập 028.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Chính sách và hiến chương nói cùng một chu kỳ tuần và cùng một con số (Priority: P1)

Là người đóng vai SRE, tôi muốn chính sách ngân sách lỗi của cả 7 service được tính trên tuần lịch giờ Việt Nam, với SLO và tỷ lệ cho phép mới (khả dụng 99%, 5xx dưới 1%), và hiến chương cũng nói đúng như vậy. Có như thế, một sự cố trong tuần được nhìn thấy và xử lý trong tuần, chứ không bị pha loãng trong cả tháng. Không còn chỗ nào trong repo nói một chu kỳ hay một con số khác.

**Why this priority**: Mọi phần khác (cảnh báo, dashboard, đóng băng) đều tính theo định nghĩa này. Nếu hiến chương, manifest và test nói ba điều khác nhau thì không có căn cứ nào để kiểm tra phần còn lại.

**Independent Test**: Mở hiến chương và 7 manifest. Xác nhận:
- hiến chương ở phiên bản 2.0.0, ghi "99% weekly availability." và "5xx responses below 1% of requests.";
- mỗi manifest khai SLO khả dụng 99% và 5xx dưới 1%;
- chính sách ngân sách khai cửa sổ tuần lịch giờ Việt Nam với tỷ lệ cho phép 1% / 1% / 5% / 1%;
- bộ test quy ước manifest chạy xanh.

**Acceptance Scenarios**:

1. **Given** hiến chương sau khi sửa, **When** tôi đọc Nguyên tắc VIII, **Then** các mặc định nền tảng ghi "99% weekly availability." và "5xx responses below 1% of requests."; phiên bản là 2.0.0, và báo cáo tác động đồng bộ nêu lý do tăng MAJOR cùng tác động chuyển đổi lên các service hiện có.
2. **Given** manifest của một service bất kỳ trong 7 service, **When** tôi đọc khối SLO và khối chính sách ngân sách lỗi, **Then** SLO khả dụng là 99% (theo tuần), 5xx dưới 1%, ngưỡng độ trễ p95/p99 không đổi. Chính sách khai cửa sổ tuần lịch (thứ Hai 00:00 → Chủ nhật 23:59, UTC+7) với tỷ lệ cho phép: khả dụng 1%, 5xx 1%, độ trễ p95 5%, độ trễ p99 1%.
3. **Given** cả 7 manifest, **When** tôi rà soát toàn bộ, **Then** không manifest nào cần ghi lý do ngoại lệ cho SLO khả dụng/5xx, vì 99%/1% chính là mặc định mới của hiến chương.
4. **Given** một manifest bị sửa ngược về tháng lịch hoặc về tỷ lệ 0.1%, **When** bộ test quy ước manifest chạy, **Then** test báo đỏ chỉ đúng service và khoá bị lệch.

---

### User Story 2 - Cảnh báo mốc và dashboard tính theo tuần lịch (Priority: P1)

Là người đóng vai SRE, tôi muốn các cảnh báo mốc 50%/75%/100% tính mức tiêu hao từ thứ Hai 00:00 giờ Việt Nam của tuần hiện tại, với tỷ lệ cho phép mới. Tôi cũng muốn các panel ngân sách trên dashboard Ngân sách lỗi tuần hiển thị đúng mức tiêu hao của tuần này. Như vậy, thứ tôi thấy trên dashboard và thứ làm cảnh báo bắn là cùng một con số.

**Why this priority**: Tiêu chí "vi phạm được phát hiện bằng cảnh báo, không phải tự soi dashboard" của 027 vẫn phải đúng sau khi đổi chu kỳ. Nếu rule tính theo tuần mà dashboard còn theo tháng, người vận hành sẽ thấy hai con số mâu thuẫn trên cùng một màn hình.

**Independent Test**: Trên stack local đã dựng lại sạch, tạo đủ lỗi tổng hợp cho một service để tiêu hết ngân sách 5xx của tuần. Xác nhận:
- cảnh báo bắn lần lượt ở 50%, 75%, 100%;
- dashboard hiển thị mức tiêu hao "tuần này" khớp với cảnh báo;
- không còn panel ngân sách nào nhắc "tháng".

**Acceptance Scenarios**:

1. **Given** một ngân sách của một service đang dưới 50% trong tuần hiện tại, **When** lượng request xấu từ thứ Hai 00:00 giờ Việt Nam làm mức tiêu hao vượt 50% (tính trên tỷ lệ cho phép mới), **Then** cảnh báo mốc 50% bắn cho đúng service và ngân sách đó trong vòng một chu kỳ đánh giá (5 phút).
2. **Given** lỗi tổng hợp đủ để tiêu hết ngân sách 5xx tuần của một service, **When** mức tiêu hao lần lượt vượt 50%, 75%, 100%, **Then** cảnh báo tương ứng bắn ở đúng từng mốc. Trên môi trường lưu lượng thấp, nhiều mốc có thể bắn cùng một chu kỳ (giới hạn đã biết, xem Edge Cases).
3. **Given** request xấu xảy ra vào Chủ nhật tuần trước (giờ Việt Nam), **When** sang thứ Hai 00:00 giờ Việt Nam, **Then** những request đó không còn được tính vào mức tiêu hao tuần mới, và cảnh báo mốc của tuần cũ tắt trong vòng một chu kỳ đánh giá.
4. **Given** người vận hành mở dashboard Ngân sách lỗi tuần, **When** xem nhóm panel ngân sách, **Then** cả 3 panel (mức tiêu hao, cảnh báo đang hoạt động, cạn ngân sách) ghi "tuần này", hiển thị dữ liệu theo cửa sổ tuần lịch, và panel text nêu cam kết 99%/tuần thay cho 99.9%/tháng.
5. **Given** bố cục dashboard hiện có, **When** spec này hoàn thành, **Then** dashboard không bị tách, không bị sắp xếp lại, và không có panel nào ngoài phần ngân sách và panel text bị thay đổi (việc tách thuộc spec B).

---

### User Story 3 - Đóng băng, hồi phục và phát hiện nhanh dùng SLO mới, giữ đúng quy tắc qua ranh giới tuần (Priority: P2)

Là người đóng vai SRE, tôi muốn trạng thái "cạn ngân sách — ưu tiên độ tin cậy" và rule phát hiện nhanh của 028 dùng cùng SLO mới. Cụ thể: một ngày đạt SLO khi 5xx dưới 1%, và rule phát hiện nhanh bắn khi 5xx ≥ 1% trong 5 phút. Việc ngân sách đặt lại mỗi thứ Hai vẫn không gỡ trạng thái đóng băng. Có như vậy thì hệ quả và tín hiệu phát hiện nhất quán với con số đã cam kết.

**Why this priority**: Đây là phần hệ quả và phần phát hiện sự cố. Nó phụ thuộc vào định nghĩa (US1) và tín hiệu "cạn" từ rule mốc 100% (US2).

**Independent Test**:
- Làm cạn ngân sách một service, xác nhận service hiện trong bảng "cạn".
- Ghi sự kiện "cạn" thử với các mốc thời gian khác nhau, xác nhận logic 3 ngày dùng ngưỡng 5xx 1%.
- Tiêm 5xx với tỷ lệ dưới và trên 1% để xác nhận rule phát hiện nhanh chỉ bắn khi ≥ 1%.

**Acceptance Scenarios**:

1. **Given** một service cạn ngân sách vào thứ Bảy, **When** sang thứ Hai và ngân sách tuần đặt lại về đầy đủ nhưng service chưa đạt SLO 3 ngày liên tiếp, **Then** service vẫn ở trạng thái "cạn ngân sách — ưu tiên độ tin cậy".
2. **Given** một service đang đóng băng, **When** service đạt SLO 3 ngày liên tiếp (5xx dưới 1%, độ trễ trong ngân sách; ngày không traffic tính là đạt), **Then** service được coi là đã hồi phục, kể cả khi tuần chưa kết thúc.
3. **Given** một ngày service có tỷ lệ 5xx 0.5%, **When** rule đóng băng xét ngày đó, **Then** ngày đó được tính là đạt SLO (theo SLO cũ 0.1% thì là không đạt).
4. **Given** sự cố làm tỷ lệ 5xx của một service trong 5 phút gần nhất là 0.5%, **When** rule phát hiện nhanh chạy, **Then** rule không bắn vì 5xx; khi tỷ lệ ≥ 1% thì rule bắn cho đúng service đó. Ngưỡng độ trễ và quy tắc gateway chỉ xét 5xx không đổi.
5. **Given** lỗi do một buổi diễn tập (025/027/028), **When** ngân sách tuần được tính, **Then** lỗi đó được tính như lỗi thật, và nếu ngân sách cạn thì chính sách đóng băng áp dụng đầy đủ.

---

### User Story 4 - Tài liệu 021/027/028 và tài liệu đi kèm của 029 nói đúng chu kỳ và con số mới (Priority: P3)

Là người đọc tài liệu dự án (PO, QA, kiến trúc, người vận hành), tôi muốn mọi tài liệu và hiện vật đang dùng của 021, 027 và 028 được sửa tại chỗ sang tuần lịch và SLO mới. Tôi cũng muốn 029 có đủ bộ tài liệu đi kèm như 027/028. Như vậy, ai đọc bất kỳ tài liệu nào cũng không gặp con số hay chu kỳ cũ, trừ những bản ghi lịch sử.

**Why this priority**: Không ảnh hưởng hành vi cảnh báo, nhưng nếu thiếu thì tài liệu dẫn người đọc làm sai (ví dụ chạy truy vấn theo tháng ở file `07`).

**Independent Test**: Tìm trên toàn repo các tham chiếu tới chu kỳ tháng và tới các con số 99.9% / 0.1% gắn với SLO hoặc ngân sách. Xác nhận chỉ còn ở hiến chương phần lịch sử phiên bản, các mục cũ của QA_Debt, bản ghi kết quả diễn tập và specs/002. Mở các file tài liệu mới của 029 và xác nhận đủ.

**Acceptance Scenarios**:

1. **Given** spec, plan, research, data-model, contracts, quickstart, tasks của 021, 027 và 028, **When** tôi đọc, **Then** chu kỳ ngân sách là tuần lịch giờ Việt Nam, SLO khả dụng 99%, 5xx dưới 1%, các cửa sổ rule là 7 ngày và 14 ngày, ngưỡng phát hiện nhanh 5xx là 1%.
2. **Given** tài liệu PO/QA/Architect, sơ đồ drawio, tóm tắt spec tiếng Việt và tài liệu Kibana (`06`, `07`, `08`, `alerts/README.md`) của 021/027/028, **When** tôi đọc, **Then** chúng nhất quán với chu kỳ và con số mới.
3. **Given** bộ tài liệu đi kèm của 029, **When** tôi kiểm tra, **Then** có đủ:
   - file PO `029_PO_ngân sách lỗi theo tuần lịch.md`, file QA và file Architect cùng tên;
   - mục 029 trong `functional-debt.md`, `QA_Debt.md`, `technical-debt.md`;
   - 3 sơ đồ drawio 029 (thành phần, luồng nghiệp vụ, trình tự);
   - folder Postman 029 gửi `X-Chaos-Fault: 5xx` tới 7 service.
4. **Given** các bản ghi lịch sử (mục cũ trong QA_Debt, kết quả diễn tập, specs/002), **When** spec này hoàn thành, **Then** chúng không bị sửa.

---

### Edge Cases

- **Ranh giới tuần theo giờ Việt Nam**: thứ Hai 00:00 giờ Việt Nam là Chủ nhật 17:00 UTC. Request lúc Chủ nhật 18:00 UTC thuộc tuần mới, không phải tuần cũ. Cách xác định đầu tuần phải được kiểm chứng trên hệ thống thật (như điểm V3 của 027), không suy diễn.
- **Triển khai giữa tuần**: tuần đầu tiên tính từ thứ Hai 00:00 của tuần chứa ngày triển khai. Nếu stack vừa dựng lại thì tuần đầu thiếu dữ liệu những ngày trước, nên mẫu số nhỏ và mức tiêu hao nhạy hơn.
- **Lưu lượng thấp**: chu kỳ tuần có ít request hơn tháng khoảng 4 lần nên chỉ vài request xấu đã vượt mốc. Nới 5xx lên 1% giảm một phần độ nhạy, nhưng ngân sách độ trễ (5%/1%) không đổi. Theo lựa chọn của người dùng, chỉ ghi giới hạn này (Edge Case, QA_Debt, technical-debt), không thêm logic.
- **Đóng băng kéo dài hơn cửa sổ nhìn lại 14 ngày**: nếu service cạn rồi vẫn không đạt SLO liên tục quá 14 ngày, sự kiện "cạn" trôi ra ngoài cửa sổ và trạng thái đóng băng có thể tự biến mất dù chưa hồi phục. Đây là giới hạn đã biết do lựa chọn 14 ngày, phải ghi vào technical-debt.
- **Đóng băng qua ranh giới tuần**: ngân sách đặt lại thứ Hai nhưng trạng thái đóng băng giữ tới khi đạt 3 ngày (User Story 3, kịch bản 1).
- **Không có traffic hoặc thiếu dữ liệu trong tuần**: hiển thị "không có dữ liệu", không bắn hay tắt cảnh báo chỉ vì thiếu dữ liệu (giữ FR-012 của 027). Ngày không traffic vẫn tính là đạt cho hồi phục.
- **Diễn tập đốt ngân sách tuần**: lỗi diễn tập tính như thật nên một buổi diễn tập có thể làm cạn ngân sách tuần và đóng băng service. Điều này được chấp nhận. Khác với tháng, ngân sách sẽ đặt lại sớm hơn (thứ Hai kế tiếp), nhưng đóng băng vẫn theo điều kiện 3 ngày.
- **Ngân sách khả dụng và 5xx cùng nguồn dữ liệu**: hai ngân sách vẫn đo cùng tỷ lệ request trả 5xx và giờ có cùng tỷ lệ cho phép 1%, nên luôn bắn cùng lúc. Điều này được chấp nhận như ở 027.
- **Sửa rule mốc 100% khi có service đang cạn**: QA_Debt 027 ghi rằng sửa rule làm Kibana ghi lại sự kiện "cạn". Việc đổi truy vấn sang tuần cũng là sửa rule. Vì Elastic được dọn sạch sau triển khai và không giữ sự kiện cũ, rủi ro này chỉ ảnh hưởng trong lúc triển khai.
- **Tác động MAJOR lên hiến chương**: nới mặc định nền tảng áp dụng cho mọi service sau này, không chỉ 7 service hiện có. Báo cáo tác động đồng bộ phải nêu điều này.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Ngân sách lỗi của cả 7 service PHẢI được tính trên cửa sổ tuần lịch giờ Việt Nam: từ thứ Hai 00:00 tới Chủ nhật 23:59, UTC+7. Ngân sách đặt lại về đầy đủ vào thứ Hai 00:00 mỗi tuần. Cửa sổ này áp dụng cho cả 4 ngân sách và thay thế hoàn toàn tháng lịch.
- **FR-002**: SLO đã khai báo trong manifest của cả 7 service PHẢI là: khả dụng ≥ 99% theo tuần, tỷ lệ 5xx dưới 1% số request. Ngưỡng độ trễ p95/p99 của từng service (kể cả ngoại lệ của BFF) giữ nguyên.
- **FR-003**: Chính sách ngân sách lỗi của mỗi service PHẢI có tỷ lệ request xấu được phép: khả dụng 1%, tỷ lệ lỗi 5xx 1%, độ trễ p95 5%, độ trễ p99 1%. Mỗi tỷ lệ bằng đúng 1 − SLO tương ứng của chính service đó.
- **FR-004**: Hiến chương PHẢI được sửa trong spec này:
  - Nguyên tắc VIII ghi "99% weekly availability." và "5xx responses below 1% of requests.";
  - phiên bản tăng từ 1.0.0 lên 2.0.0 và cập nhật ngày sửa đổi;
  - báo cáo tác động đồng bộ nêu lý do tăng MAJOR và tác động chuyển đổi lên các service hiện có.
- **FR-005**: Mọi quy tắc khác của chính sách 027 PHẢI giữ nguyên:
  - các mốc cảnh báo 50%/75%/100% riêng cho từng ngân sách;
  - "cạn" = bất kỳ ngân sách nào đạt 100%;
  - hệ quả khi cạn: người vận hành dừng merge tính năng mới vào service đó và chỉ làm việc nâng độ tin cậy;
  - hồi phục khi đạt SLO 3 ngày liên tiếp (ngày theo giờ Việt Nam, ngày không traffic tính là đạt);
  - đặt lại ngân sách đầu tuần không gỡ trạng thái đóng băng.
- **FR-006**: Cảnh báo mốc PHẢI chỉ tính request từ thứ Hai 00:00 giờ Việt Nam của tuần hiện tại. Phạm vi dữ liệu mà mỗi lần đánh giá xét tới là 7 ngày.
- **FR-007**: Cả 4 cảnh báo ngân sách PHẢI được đánh giá mỗi 5 phút. Cảnh báo đã bắn giữ ở trạng thái hoạt động liên tục chừng nào mức tiêu hao còn trên mốc.
- **FR-008**: Trạng thái "cạn ngân sách — ưu tiên độ tin cậy" PHẢI được suy ra từ sự kiện cạn và kết quả SLO theo ngày trong 14 ngày gần nhất. Một ngày được tính là đạt SLO khi 5xx dưới 1% và độ trễ trong ngân sách p95 5% / p99 1%.
- **FR-009**: Cảnh báo phát hiện nhanh của 028 PHẢI bắn khi tỷ lệ 5xx của service trong 5 phút gần nhất ≥ 1%, hoặc khi p95/p99 vượt ngưỡng đã khai báo của service. Riêng gateway vẫn chỉ xét 5xx.
- **FR-010**: Dashboard Ngân sách lỗi tuần PHẢI hiển thị 3 panel ngân sách (mức tiêu hao, cảnh báo đang hoạt động, cạn ngân sách) theo cửa sổ tuần lịch, với tiêu đề "tuần này" và khoảng thời gian khớp cửa sổ mới. Panel text PHẢI nêu cam kết 99%/tuần. Dashboard KHÔNG được tách hay sắp xếp lại trong spec này.
- **FR-011**: Khi triển khai, ngân sách tuần PHẢI bắt đầu tính từ thứ Hai 00:00 giờ Việt Nam của tuần chứa ngày triển khai. Không mang dữ liệu tiêu hao, sự kiện "cạn" hay trạng thái đóng băng của chu kỳ tháng sang.
- **FR-012**: Lỗi phát sinh từ diễn tập (025/027/028) PHẢI được tính vào ngân sách tuần như lỗi thật.
- **FR-013**: Khi một service không có request trong tuần, hoặc thiếu dữ liệu do gián đoạn telemetry, hệ thống PHẢI hiển thị "không có dữ liệu" và KHÔNG được bắn hay tắt cảnh báo chỉ vì thiếu dữ liệu.
- **FR-014**: Định nghĩa cảnh báo PHẢI tiếp tục được lưu dạng file export trong repo. Kiểm thử tự động PHẢI bảo vệ:
  - cửa sổ tuần lịch và tỷ lệ cho phép mới trong 7 manifest;
  - mặc định SLO nền tảng mới (99% / 1%);
  - trong định nghĩa cảnh báo: tỷ lệ cho phép mới, chu kỳ 5 phút và các mốc 50/75/100.
- **FR-015**: Thay đổi KHÔNG được làm đổi hành vi phản hồi của bất kỳ endpoint nào, và KHÔNG được đổi ngưỡng độ trễ đã khai báo.
- **FR-016**: Tài liệu và hiện vật đang dùng của 021, 027 và 028 PHẢI được sửa tại chỗ sang tuần lịch và con số mới, gồm:
  - spec, plan, research, data-model, contracts, quickstart, tasks;
  - PO/QA/Architect, sơ đồ drawio, tóm tắt spec tiếng Việt;
  - tài liệu thiết kế dashboard SLO, tài liệu Kibana `06`/`07`/`08`, `alerts/README.md`;
  - mô tả trong collection Postman.

  KHÔNG được sửa các bản ghi lịch sử: mục cũ trong `QA_Debt.md`, kết quả diễn tập trong `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.

  Bằng chứng đo thật theo chính sách tháng/0.1% PHẢI bị xoá khỏi tài liệu vận hành (`07`, `08`, `alerts/README.md`) và khỏi research/tasks/quickstart của 027/028. Các ràng buộc kỹ thuật rút ra từ những bằng chứng đó (ví dụ "kết quả rule chỉ giữ cột định danh alert") vẫn phải được giữ lại dưới dạng quyết định.
- **FR-017**: Spec này PHẢI có bộ tài liệu đi kèm theo khuôn 027/028:
  - `docs/PO/029_PO_ngân sách lỗi theo tuần lịch.md`;
  - `docs/QA/029_QA_ngân sách lỗi theo tuần lịch.md`;
  - `docs/architecture/029_Architect_ngân sách lỗi theo tuần lịch.md`;
  - mục 029 trong `docs/PO/functional-debt.md`, `docs/QA/QA_Debt.md`, `docs/architecture/technical-debt.md`;
  - 3 sơ đồ drawio 029 (thành phần, luồng nghiệp vụ, trình tự);
  - folder Postman 029, mỗi service trong 7 service một request gửi `X-Chaos-Fault: 5xx`.
- **FR-018**: Việc dọn toàn bộ Elastic sau khi triển khai CHỈ được thực hiện sau khi người dùng xác nhận lại ngay trước lúc xoá.

### Key Entities *(include if feature involves data)*

- **Chính sách ngân sách lỗi của service**: như 027, nhưng cửa sổ là tuần lịch giờ Việt Nam, tỷ lệ cho phép 1% / 1% / 5% / 1%.
- **SLO mặc định nền tảng**: khả dụng 99% theo tuần, 5xx dưới 1%, ngưỡng độ trễ theo loại service. Được định nghĩa trong hiến chương 2.0.0, được manifest và kiểm thử quy ước tuân theo.
- **Mức tiêu hao ngân sách tuần**: với mỗi ngân sách của mỗi service, phần trăm ngân sách đã tiêu từ thứ Hai 00:00 giờ Việt Nam của tuần hiện tại.
- **Cảnh báo ngân sách**: gắn với một service, một ngân sách và một mốc (50/75/100%). Tắt khi sang tuần mới nếu mức tiêu hao không còn trên mốc.
- **Sự kiện cạn ngân sách**: ghi khi một ngân sách đạt 100%. Được xét trong 14 ngày gần nhất để suy ra trạng thái đóng băng. Sự kiện của chu kỳ tháng không được mang sang.
- **Trạng thái cạn ngân sách của service**: bắt đầu khi bất kỳ ngân sách nào đạt 100%, kết thúc khi đạt SLO mới 3 ngày liên tiếp. Không bị gỡ bởi việc đặt lại ngân sách thứ Hai.
- **Cảnh báo phát hiện nhanh**: gắn với một service, bắn khi 5xx ≥ 1% hoặc độ trễ vượt ngưỡng trong 5 phút gần nhất.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Cả 7/7 manifest khai cửa sổ tuần lịch giờ Việt Nam, SLO khả dụng 99% và 5xx dưới 1%, tỷ lệ cho phép 1% / 1% / 5% / 1%. Hiến chương ở phiên bản 2.0.0 với hai dòng mặc định mới.
- **SC-002**: Khi tạo đủ lỗi tổng hợp để tiêu hết ngân sách 5xx tuần của một service, cảnh báo bắn ở cả ba mốc 50%, 75% và 100%. Mỗi mốc bắn trong vòng một chu kỳ đánh giá (5 phút) kể từ khi mức tiêu hao thực tế vượt mốc.
- **SC-003**: Sau thứ Hai 00:00 giờ Việt Nam, mức tiêu hao của tuần mới không chứa request của tuần trước, và cảnh báo mốc của tuần cũ (nếu không còn trên mốc) tắt trong vòng một chu kỳ đánh giá. Trạng thái đóng băng vẫn giữ nếu chưa đủ 3 ngày đạt SLO.
- **SC-004**: 100% panel ngân sách trên dashboard Ngân sách lỗi tuần hiển thị theo tuần lịch, không còn nhãn "tháng". Con số trên panel khớp với điều kiện làm cảnh báo bắn ở cùng thời điểm.
- **SC-005**: Rule phát hiện nhanh không bắn vì 5xx khi tỷ lệ 5xx trong 5 phút dưới 1%, và bắn trong vòng một chu kỳ khi tỷ lệ ≥ 1%.
- **SC-006**: Tìm trên toàn repo không còn tham chiếu nào tới chu kỳ ngân sách tháng hay SLO 99.9% / 0.1% trong hiện vật và tài liệu đang dùng của 021/027/028. Chỉ còn ở phần lịch sử phiên bản của hiến chương và các bản ghi lịch sử đã loại trừ ở FR-016.
- **SC-007**: Toàn bộ kiểm thử quy ước manifest và kiểm thử định nghĩa cảnh báo chạy xanh. Mỗi kiểm thử chu kỳ tuần/tỷ lệ mới được chạy thấy đỏ trước khi sửa manifest/rule (Nguyên tắc III).
- **SC-008**: Không có cảnh báo nào bắn cho service không có request trong tuần hoặc chỉ do thiếu dữ liệu telemetry.

## Assumptions

- Cơ chế cảnh báo, index sự kiện cạn, connector, các panel Discover và công cụ tiêm 5xx của 027 được giữ nguyên. Spec này chỉ đổi chu kỳ, con số và phạm vi dữ liệu, không dựng cơ chế mới.
- Elastic đang trống (người dùng đã xoá volume). Việc kiểm chứng trên Kibana thật bắt đầu từ stack dựng mới, nên không có dữ liệu tháng hay sự kiện "cạn" cũ cần chuyển đổi.
- Cách hệ thống xác định thứ Hai 00:00 giờ Việt Nam (làm tròn theo tuần có bắt đầu từ thứ Hai hay không, cách dịch múi giờ) là chi tiết triển khai. Nó phải được kiểm chứng trên hệ thống thật ở giai đoạn lập kế hoạch/triển khai, như điểm V3 của 027.
- Cửa sổ dữ liệu 7 ngày của rule mốc được chọn vì một tuần lịch kéo dài tối đa 7 ngày tính từ thứ Hai 00:00. Nếu kiểm chứng thực tế cho thấy 7 ngày làm hụt dữ liệu đầu tuần, phải dừng và hỏi lại người dùng, không tự nới.
- "Người vận hành" vẫn là một người đóng vai SRE/Dev; việc dừng merge là cam kết quy trình, không có cơ chế chặn merge tự động (như 027).
- Lưu lượng thấp ở local làm vượt mốc rất nhanh là giới hạn đã biết, chỉ được ghi lại theo lựa chọn của người dùng.
- Việc tách dashboard thuộc spec B, làm sau spec này.
