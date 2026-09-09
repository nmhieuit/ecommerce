# Research: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-09

## Quyết định 0: Xác nhận hiện trạng trước khi thiết kế

Trước khi quyết định cách tiếp cận, đã rà soát trực tiếp trạng thái hiện tại của repository (không
suy đoán):

- Cả 7 `services/*/src/*/service-manifest.yaml` (parties, products, baskets, orders, identity,
  gateway, bff) đều đã có khối `slos:` với đủ 4 giá trị (`availability`, `error-rate.max-5xx-ratio`,
  `latency.p95`, `latency.p99`). 6/7 service mang `classification: internal-service-api` và khớp
  đúng hồ sơ mặc định của chính phân loại đó (p95 150ms / p99 500ms / 99.9% / 0.1%); `bff` mang
  `classification: client-facing-bff` và khớp đúng hồ sơ mặc định của CHÍNH phân loại đó (p95 300ms /
  p99 800ms) — kèm một comment YAML giải thích vì sao phân loại "client-facing-bff" hợp lý cho service
  này (fan-out gọi nhiều service phía sau).
  **Sửa lại sau khi triển khai (T008, xem contracts/service-manifest-slo-shape.md)**: nhận định ban
  đầu ở đây (và ở Quyết định 2 bên dưới) rằng "`bff` có một ngoại lệ so với mặc định" là **sai** — hai
  hồ sơ mặc định (`client-facing-bff`, `internal-service-api`) của constitution Principle VIII song
  song và bình đẳng, không cái nào là "ngoại lệ" của cái kia; `bff` chỉ đơn giản khớp đúng hồ sơ của
  đúng phân loại nó mang. `SloDefaultComplianceTests` (T008) xác nhận: cả 7/7 service đều khớp mặc
  định của chính phân loại của mình, không có `slos.justification` nào thực sự cần thiết hiện tại.
- KHÔNG có bất kỳ test tự động nào trong `tests/` đọc `service-manifest.yaml` (đã grep toàn bộ
  `tests/` cho `service-manifest`, `slo`, `Slo`, `SLO` — không có kết quả liên quan). Nghĩa là FR-001
  đến FR-003 hiện đúng "tại một thời điểm kiểm tra thủ công", không có gì ngăn một service mới thiếu
  SLO hoặc một giá trị bị sửa lệch mặc định mà không ai để ý.
- Đã có sẵn một dashboard Kibana đo 3/4 chỉ số SLO (Error-rate, Latency p95, Latency p99; Availability
  suy ra xấp xỉ từ `100% − Error-rate`) từ dữ liệu traces OTel thật, dựng và xác minh khớp dữ liệu thô
  tại `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` (đối chiếu Lens Formula
  với truy vấn Elasticsearch thô cho `Orders.Api`: error-rate khớp tuyệt đối, p95/p99 khớp trong sai
  số ~0.25% do cửa sổ thời gian trượt) và có sẵn artifact export tại
  `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`. Tài liệu này cũng đã ghi
  nhận rõ cách phân biệt "không có dữ liệu" với "0% lỗi" (mục FR-006 của spec) và đã đề cập trong
  `docs/roadmap.md` rằng việc đo SLO thật thuộc phạm vi `SCRUM-29`.
- Kết luận: phần việc còn thiếu thực sự của SCRUM-29 không phải là "khai báo" hay "xây dashboard" từ
  đầu, mà là (a) làm cho việc khai báo đúng có tính bền vững/tự bảo vệ, và (b) chính thức hoá dashboard
  đã có làm một phần được công nhận, có hợp đồng, của tính năng — thay vì một tài liệu vận hành rời
  rạc không ai đảm bảo còn đúng.

## Quyết định 1: Cơ chế bảo vệ khai báo SLO khỏi trôi dạt (US1, US2)

**Decision**: Thêm một dự án xUnit mới `tests/ServiceManifestSloConventionTests`, dùng `YamlDotNet`
để parse trực tiếp cả 7 `service-manifest.yaml` trên đĩa (không cần service nào build/chạy), rồi
assert: (a) đủ 4 giá trị SLO, không rỗng/không placeholder; (b) giá trị khớp đúng hồ sơ mặc định
tương ứng với `service.classification` của service đó, TRỪ KHI service có trường
`slos.justification` khác rỗng.

**Rationale**:
- Đúng khuôn mẫu convention-test đã có trong repo cho các mối quan tâm cấu trúc tương tự
  (`tests/DeploymentManifestConventionTests` cho manifest triển khai, `tests/ContainerConventionTests`
  cho Dockerfile, `tests/CrossServiceIsolation.Tests` cho ranh giới dữ liệu) — dùng lại đúng thư viện
  (`YamlDotNet`) và cách tổ chức (đọc file cấu hình trên đĩa, không cần hạ tầng thật) mà repo đã chọn.
- Constitution Principle III (Test-First, NON-NEGOTIABLE) yêu cầu có test bảo vệ hành vi; hiện trạng
  "đúng nhưng không test" chính là dạng vi phạm tinh thần nguyên tắc này mà Quyết định 0 đã phát hiện.
- Test tĩnh (đọc file, không cần cluster/CI đặc biệt) phù hợp với bản chất của yêu cầu: đây là một
  ràng buộc cấu trúc tại thời điểm build, không phải hành vi runtime cần một môi trường thật để quan
  sát (khác với US3 — xem Quyết định 3).

**Alternatives considered**:
- **Chỉ rà soát thủ công định kỳ (checklist vận hành)**: Bị loại vì không có gì ngăn trôi dạt giữa
  hai lần rà soát, và vi phạm trực tiếp Principle III (NON-NEGOTIABLE).
- **Thêm một script kiểm tra Bash/PowerShell riêng trong CI thay vì dự án xUnit**: Bị loại vì không
  nhất quán với cách repo đã chọn cho mọi mối quan tâm "đọc cấu hình, assert cấu trúc" khác — luôn là
  dự án xUnit song song, không phải script rời rạc; giữ một cơ chế test duy nhất giúp coverage report
  và pipeline CI không phải xử lý hai loại kiểm tra khác nhau cho cùng một lớp vấn đề.

## Quyết định 2: Cách ghi lý do ngoại lệ để máy đọc được (US2, FR-003)

**Decision**: Thêm một trường có cấu trúc `slos.justification: <string>` vào schema `service-manifest.yaml`
(tuỳ chọn, chỉ bắt buộc khi giá trị SLO khác hồ sơ mặc định của CHÍNH phân loại mà service đó mang).
Không sửa `bff` hay bất kỳ manifest nào khác để "lấp" trường này — xác nhận bằng `SloDefaultComplianceTests`
(T008) rằng hiện tại KHÔNG có service nào thực sự lệch khỏi mặc định của chính phân loại của mình
(xem sửa lại ở Quyết định 0), nên không có gì để chuyển đổi. Trường này tồn tại như một cơ chế phòng
ngừa cho tương lai, đúng theo FR-003, không phải để hợp thức hoá một ngoại lệ có thật của `bff` — kết
luận ban đầu ở đây rằng `bff` "có một ngoại lệ" đã bị chứng minh sai khi chạy test thật.

**Rationale**:
- FR-003 yêu cầu: NẾU một service tương lai khai báo giá trị khác mặc định của chính phân loại nó,
  lý do PHẢI ghi ngay tại chỗ khai báo và máy đọc được. Một trường YAML có tên (`slos.justification`)
  thỏa yêu cầu này tốt hơn một comment prose (không máy đọc được), nên được thêm vào schema dù hiện
  chưa có instance nào dùng tới.
- Đây là một thay đổi nhỏ, thêm-vào (additive) — không có tooling nào hiện đọc `service-manifest.yaml`
  ngoài `tests/ServiceManifestSloConventionTests` mới, nên không có rủi ro phá vỡ consumer nào.
- KHÔNG chỉnh sửa `services/bff/src/Bff.Api/service-manifest.yaml`: làm vậy sẽ là thêm dữ liệu giả
  (một "lý do" cho một ngoại lệ không có thật) chỉ để khớp với một giả định sai ban đầu — vi phạm
  chính tinh thần của tính năng này ("ngân sách là cam kết thật, không phải hình thức").

**Alternatives considered**:
- **Ép `bff` phải có `slos.justification` để "cho đủ ví dụ"**: Bị loại — đây chính là điều research
  ban đầu định làm trước khi T008 phát hiện premise sai; giữ nó sẽ là dữ liệu không trung thực.
- **Giữ nguyên comment prose, không thêm trường**: Bị loại vì không máy đọc được — nếu một ngoại lệ
  thật xuất hiện trong tương lai, suy đoán "có comment nghĩa là có lý do" là một điều kiện quá lỏng
  lẻo (bất kỳ comment nào, kể cả không liên quan, cũng sẽ "pass"), không thỏa được FR-003 một cách
  đáng tin cậy.
- **Tạo một file khai báo ngoại lệ tập trung riêng (ví dụ `slo-exceptions.yaml` ở gốc repo)**: Bị
  loại vì hiện có 0 ngoại lệ trong 7 service — một file tập trung cho trường hợp chưa từng xảy ra là
  phức tạp không tương xứng (Principle I: "complexity proportional to the domain").

## Quyết định 3: Cơ chế đo lường liên tục (US3, FR-004 đến FR-007)

**Decision**: Chính thức hoá dashboard Kibana đã dựng (`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`,
artifact `dashboards/slo-van-hanh-hang-ngay.ndjson`) làm cơ chế đo lường liên tục chính thức của tính
năng này, thay vì xây một cơ chế mới. Ghi lại hợp đồng (bất biến bắt buộc) mà dashboard này phải thỏa
tại `contracts/continuous-measurement-contract.md`, và một `quickstart.md` lặp lại đúng 3 kịch bản
kiểm thử của Jira (đọc manifest → đối chiếu dashboard → làm chậm một endpoint và xác nhận ngân sách
bị tiêu hao thể hiện rõ) như một quy trình xác thực lặp lại được, không phải một lần build-rồi-quên.

**Rationale**:
- Dashboard đã tồn tại, đã dùng đúng dữ liệu telemetry mà Principle VII yêu cầu (traces OTel qua
  Elasticsearch), và đã được xác minh khớp dữ liệu thô với sai số chấp nhận được — xây lại là công
  việc trùng lặp không cần thiết, đi ngược nguyên tắc tránh trùng lặp công sức.
  # Ghi chú: Elastic alert rule tự động (cảnh báo chủ động khi vượt ngân sách) được đề cập trong
  # docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md như thuộc phạm vi SCRUM-35 —
  # nằm ngoài phạm vi đặc tả này (spec.md chỉ yêu cầu "tra cứu được", không yêu cầu cảnh báo chủ động).
- Dashboard hiện là một tài liệu vận hành đơn lẻ, không có "hợp đồng" tách biệt mà một dự án test
  hay một quickstart có thể tham chiếu để xác nhận nó còn đúng theo thời gian (ví dụ sau khi ai đó
  chỉnh sửa Lens Formula). Tách hợp đồng ra một file riêng (`contracts/continuous-measurement-contract.md`)
  giúp có một danh sách bất biến ngắn gọn, ổn định để đối chiếu, mà không cần đọc lại toàn bộ nhật ký
  xây dựng chi tiết trong file `06-...md`.
- Vì đây là hành vi runtime cần dữ liệu thật/Kibana thật để quan sát (khác Quyết định 1), xác thực
  bằng kịch bản thủ công/định kỳ tại `quickstart.md` là phù hợp — đúng logic constitution đã dùng cho
  performance test động (Principle VIII: "performance tests for critical paths run on a scheduled
  pipeline") và đúng tiền lệ 019 (smoke test động trên cluster `kind`, không chặn PR).

**Alternatives considered**:
- **Xây một dashboard/cơ chế đo lường mới từ đầu cho tính năng này**: Bị loại — trùng lặp hoàn toàn
  với công việc đã làm và đã xác minh, không mang lại giá trị thêm, chỉ tốn công.
- **Viết một test tự động gọi Elasticsearch API thật trong pipeline CI để assert dashboard luôn đúng**:
  Cân nhắc nhưng loại ở phạm vi tính năng này — đòi hỏi một môi trường Elastic thật chạy trong mọi
  lần CI (chi phí/độ không ổn định không tương xứng, cùng lý do 019 đã loại phương án tương tự cho
  cluster K8s thật); `quickstart.md` định kỳ/thủ công là đủ để thỏa FR-004 đến FR-007 mà không phải
  trả chi phí đó cho mọi PR.

## Tổng kết: NEEDS CLARIFICATION đã được giải quyết

Không có mục nào trong Technical Context còn để "NEEDS CLARIFICATION". Ba quyết định trên đều dựa
trên: (a) hiện trạng đã xác nhận trực tiếp trong repository (Quyết định 0), (b) khuôn mẫu convention-test
đã tồn tại và được kiểm chứng, và (c) artifact đo lường liên tục đã được xây dựng và xác minh từ
trước — không có quyết định nào cần thêm thông tin từ bên ngoài đặc tả hay constitution.
