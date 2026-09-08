# Bản đồ đọc tài liệu — `docs/`

Tài liệu này chỉ có 1 việc: giúp bạn tìm **đúng chỗ nên đọc** tuỳ vai trò và mục đích, không lặp lại
nội dung kỹ thuật đã có ở nơi khác.

## Đi thẳng vào việc — theo vai trò

**Bạn là Product Owner / quản lý sản phẩm, muốn biết 1 tính năng làm được gì, cho ai, lợi ích gì?**
→ [`docs/summary/`](summary/) — đọc theo số tăng dần (`001_PO_*.md` → `019_PO_*.md`). Mỗi file viết
cho người không đọc code, có mục "Giới hạn hiện tại" trung thực về những gì CHƯA làm được.

**Bạn là kỹ sư mới gia nhập dự án, cần đọc code hiểu hệ thống từ đầu?**
→ [`docs/onboarding/`](onboarding/) — đọc theo đúng số 01→11, mỗi file có phần "Đi đâu tiếp theo" dẫn
sang file kế. Trích code thật, có commit/dòng cụ thể, tách rõ "sự thật đã kiểm chứng" khỏi "khuyến
nghị". Khác `docs/architecture/`: đi theo TRÌNH TỰ LỊCH SỬ (giai đoạn xây dựng), không theo từng spec
riêng lẻ.

**Bạn là software architect / kỹ sư đã quen hệ thống, cần tra cứu sâu đúng 1 tính năng cụ thể?**
→ [`docs/architecture/`](architecture/) — 1 file `0NN_Architect_*.md` cho mỗi spec (001-019, trừ 012
đổi số thành 013). Có bảng kết quả test theo từng project, bằng chứng xác minh thật trích từ
`tasks.md` của chính spec đó — không suy đoán.

**Bạn cần biết VÌ SAO 1 công nghệ/công cụ được chọn (không phải cách nó hoạt động)?**
→ [`docs/adr/`](adr/) — Architecture Decision Record, bản gốc viết bằng tiếng Anh, mỗi file đều có
bản dịch tiếng Việt song song `0NNN-*.vi.md` (xem ghi chú cuối bài). `0007-secrets-delivery.md` có 1
mục "Amendment" đáng đọc — ví dụ mẫu cho việc cập nhật 1 ADR khi thực tế triển khai lệch khỏi quyết
định gốc.

**Bạn muốn xem sơ đồ trực quan thay vì đọc chữ?**
→ [`docs/diagrams/`](diagrams/) — mở bằng [draw.io](https://app.diagrams.net/) hoặc extension VS
Code. Mỗi spec có 3 file: `-component` (kiến trúc kỹ thuật), `-flow-nghiep-vu` (luồng phi kỹ thuật,
dành cho PO), `-sequence` (trình tự kỹ thuật chi tiết).

**Bạn cần bản tóm tắt FR/Acceptance Criteria/Success Criteria song song bản tiếng Anh gốc?**
→ [`docs/spec-summary-vi/`](spec-summary-vi/) — file JSON, chỉ có 001-012 (từ spec 013 trở đi,
`specs/0NN-*/spec.md` đã viết trực tiếp bằng tiếng Việt nên không cần bản tóm tắt riêng nữa).

**Bạn cần quy tắc thực hành cụ thể (không phải kiến trúc) đang áp dụng cho 1 phần code?**
→ [`docs/engineering/`](engineering/) — hiện có `test-first-commits.md` (quy tắc TDD bắt buộc cho
logic tính tiền `Basket.cs`/`Order.cs`), có bản dịch `test-first-commits.vi.md` song song.

**Bạn cần chạy thử hệ thống trên máy mình?**
→ [`local-development.md`](local-development.md) (khởi động bằng 1 lệnh) và
[`local-testing.md`](local-testing.md) (kịch bản test tay, đối chiếu request/response thật).

**Bạn cần biết bức tranh lớn — dự án này đang ở giai đoạn nào, sắp làm gì?**
→ [`roadmap.md`](roadmap.md) (lộ trình theo giai đoạn) và [`tech-stack-decisions.md`](tech-stack-decisions.md)
(bảng tổng hợp công nghệ đã chọn, dẫn sang từng ADR).

**Bạn chưa có kinh nghiệm với Kibana/Elasticsearch, muốn tự tay học cách quan sát hệ thống (traces,
metrics, logs, các sự kiện liên quan bảo mật)?**
→ [`docs/kibana-quan-sat-he-thong/`](kibana-quan-sat-he-thong/) — 7 file hands-on, đọc theo số
`00`→`06`, mọi lệnh/field/thao tác đều lấy trực tiếp từ dữ liệu thật và chính giao diện Kibana đang
chạy trên máy bạn, không phải ví dụ Kibana chung chung.

## Vì sao có 2 tài liệu tưởng như trùng nhau: `docs/summary/` vs `docs/onboarding/`

Không trùng — khác góc nhìn:

| | `docs/summary/` (PO) + `docs/architecture/` (kỹ thuật) | `docs/onboarding/` |
|---|---|---|
| Tổ chức theo | Từng **spec** riêng lẻ (016, 017, 018...) | Từng **giai đoạn lịch sử** của cả repo (01→11) |
| Trả lời câu hỏi | "Tính năng X làm được gì / hoạt động ra sao?" | "Đọc code kiểu gì, service này gọi service kia thế nào, tại sao lại viết thế?" |
| Nên đọc khi | Cần tra cứu đúng 1 tính năng | Cần hiểu tổng thể trước khi tự sửa code |

`docs/summary/0NN_PO_*.md` và `docs/architecture/0NN_Architect_*.md` luôn dẫn link sang nhau (PO ↔
Architect, cùng số) — bắt đầu từ đâu cũng tìm được đường sang phần còn lại.

## Vì sao một số tài liệu gốc vẫn bằng tiếng Anh — và cách đọc bản dịch

12 file trong `docs/adr/`, `docs/engineering/test-first-commits.md`, `local-development.md`,
`local-testing.md`, `roadmap.md`, `tech-stack-decisions.md` (tổng cộng 17 file) là các tài liệu ra
đời từ giai đoạn đầu dự án, viết bằng tiếng Anh. Bản gốc tiếng Anh của các file này **được giữ
nguyên, không sửa** — mỗi file giờ có 1 file `.vi.md` song song cùng tên, cùng thư mục (ví dụ
`docs/adr/0001-identity-provider.md` ↔ `docs/adr/0001-identity-provider.vi.md`), là bản dịch tiếng
Việt đầy đủ. Mở file `.vi.md` để đọc bằng tiếng Việt; nếu 2 bản lệch nhau (do bản gốc được cập nhật
sau khi dịch), bản gốc tiếng Anh là nguồn đúng — mỗi file `.vi.md` đều tự ghi rõ điều này ở đầu bài.

`docs/summary/`, `docs/architecture/`, `docs/diagrams/`, `docs/onboarding/` đều đã là tiếng Việt có
dấu hoàn toàn ngay từ bản gốc, không cần bản dịch riêng.
