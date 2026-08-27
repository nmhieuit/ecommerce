# Mô hình dữ liệu: Cổng chất lượng SonarQube chặn merge Pull Request

Tính năng này **không** thêm database hay schema ứng dụng nào (xem mục Giả định của `spec.md`: đây
là việc nối một chuỗi công cụ CI đã chuẩn hoá, không thêm dữ liệu nghiệp vụ). Các thực thể nêu ở
mục "Thực thể chính" của `spec.md` là thực thể khái niệm — mỗi thực thể ánh xạ tới một bản ghi cụ
thể đã thuộc về một hệ thống có sẵn (Jenkins hoặc GitHub hoặc SonarQube), không phải bảng mới do
tính năng này tạo ra hay migrate.

## Thực thể

### Lượt chạy pipeline (Pipeline Run)

- **Đại diện cho**: Một lần thực thi chuỗi build → unit test → integration test → contract test →
  cổng chất lượng SonarQube cho một commit cụ thể của PR (spec Thực thể chính; FR-001, FR-002).
- **Nơi lưu trữ vật lý**: Một lượt build của Jenkins Multibranch Pipeline (chạy `Jenkinsfile`). Kết
  quả từng stage là chính stage view của lượt build đó; không có kho lưu trữ riêng nào được tạo
  thêm.
- **Thuộc tính chính**: commit SHA, số PR/nhánh, trạng thái từng stage (pending/success/failure),
  trạng thái tổng thể, thời điểm bắt đầu/kết thúc, liên kết tới kết quả test và artefact coverage.
- **Quan hệ**: Một Lượt chạy pipeline tạo ra đúng một Kết quả cổng chất lượng (nếu chạy tới stage
  SonarQube) và góp phần quyết định trạng thái Chặn merge hiện tại của PR đó.

### Kết quả cổng chất lượng (Quality Gate Result)

- **Đại diện cho**: Kết quả (đạt/không đạt) của SonarQube cho một lần phân tích, cùng các chỉ số
  coverage, tỷ lệ trùng lặp, và số lượng issue mới, được đánh giá theo quality profile đã cấu hình
  (spec Thực thể chính; FR-003, FR-005).
- **Nơi lưu trữ vật lý**: Bản ghi phân tích/cổng chất lượng trên máy chủ SonarQube, tham chiếu bởi
  Sonar project + mã phân tích. Được pipeline lấy về qua bước `waitForQualityGate()` của Jenkins
  SonarQube Scanner plugin (research.md Decision 3), và được reviewer xem qua PR decoration một khi
  Community Branch Plugin được cài (research.md Decision 8).
- **Thuộc tính chính**: trạng thái cổng (OK/ERROR), % coverage, % dòng trùng lặp, số issue
  blocker/critical mới, mã phân tích, project key.
- **Quan hệ**: Thuộc về đúng một Lượt chạy pipeline; trạng thái của nó là yếu tố quyết định (cùng
  với kết quả các stage trước) cho trạng thái Chặn merge của PR đó.

### Chặn merge (Merge Block)

- **Đại diện cho**: Trạng thái gắn với một PR ngăn không cho merge trong khi Lượt chạy pipeline mới
  nhất có một stage thất bại (bao gồm cả Kết quả cổng chất lượng thất bại), và được gỡ bỏ ngay khi
  một lượt chạy đạt hoàn tất cho commit đầu hiện tại (spec Thực thể chính; FR-003, FR-004, FR-007,
  FR-008).
- **Nơi lưu trữ vật lý**: Cơ chế required-status-checks của GitHub branch protection, tính theo
  commit đầu của PR từ năm check Jenkins báo cáo (research.md Decision 2, 5). Không có bản ghi
  "Chặn merge" riêng nào được lưu trữ — đây là trạng thái phái sinh mà GitHub tự tính từ kết quả
  check.
- **Thuộc tính chính**: phái sinh, không lưu trữ: có thể merge hay không (boolean), danh sách check
  bắt buộc và trạng thái mới nhất của từng check.
- **Quan hệ**: Được tính từ Lượt chạy pipeline / Kết quả cổng chất lượng gần nhất cho commit đầu
  hiện tại của PR.
- **Lưu ý vận hành quan trọng**: tại thời điểm viết tài liệu này, cơ chế vật lý phía sau thực thể
  này (branch protection trên GitHub) **chưa được bật thành công** do giới hạn gói dịch vụ
  (research.md Decision 7) — thực thể này mô tả hành vi *mục tiêu*, không phải hiện trạng đã xác
  minh trên GitHub thật.

### Nỗ lực vượt rào (Override Attempt — chỉ phục vụ audit)

- **Đại diện cho**: Ghi nhận một nỗ lực merge PR trong khi đang bị chặn, hoặc merge thành công kèm
  xác nhận cổng đã đạt tại thời điểm đó (spec Thực thể chính; FR-009), lưu lại như một cơ chế dự
  phòng vì cờ "Do not allow bypassing" của GitHub được kỳ vọng ngăn hành động đó xảy ra hoàn toàn,
  chứ không chỉ ghi log lại.
- **Nơi lưu trữ vật lý**: Audit log tổ chức/repository của GitHub (cho các thay đổi cấu hình branch
  protection có thể vô tình mở lại đường vòng) và phản hồi từ chối merge gốc của GitHub (cho một nỗ
  lực merge khi có check bắt buộc đang thất bại/đang chờ). Không có bảng audit tùy biến nào được
  tạo bởi tính năng này.
- **Thuộc tính chính**: người thực hiện, PR, thời điểm, hành động đã thử, kết quả (bị từ chối hay
  cấu hình đã thay đổi).
- **Quan hệ**: Tham chiếu tới một PR và, nếu có, trạng thái Chặn merge đang hiệu lực tại thời điểm
  của nỗ lực đó.

## Thời gian lưu trữ

Dữ liệu Lượt chạy pipeline và Kết quả cổng chất lượng đã tồn tại theo đúng chu kỳ lưu trữ mặc định
của lịch sử build Jenkins (`buildDiscarder(logRotator(numToKeepStr: '50'))`, đã cấu hình trong
`Jenkinsfile`) và lịch sử phân tích của SonarQube — cả hai đều vượt quá "ít nhất bằng thời gian PR
còn mở". Không cần cơ chế lưu trữ mới; chỉ cần xác nhận cấu hình lưu trữ hiện có trên cả hai máy chủ
không bị rút ngắn hơn mức đó (việc kiểm tra vận hành, không phải mã mới).
