# Kiến trúc: Tài liệu luyện troubleshoot theo nhóm lỗi

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Spec D của đợt rà soát nợ kỹ thuật (tài liệu cho danh mục 8 nhóm lỗi của 031 và dashboard
Xử lý sự cố của 030). Đặc tả tại [`specs/032-troubleshoot-practice-docs/`](../../specs/032-troubleshoot-practice-docs/).
Quyết định thiết kế, số đo thật của từng nhóm và các phát hiện nằm ở
[`research.md`](../../specs/032-troubleshoot-practice-docs/research.md) mục "Kết quả xác minh".

**Trạng thái xác minh**: có test tự động cho phần tài liệu
([`tests/TroubleshootGuideConventionTests`](../../tests/TroubleshootGuideConventionTests), 38 test, viết trước khi có tài liệu
và thấy đỏ rồi mới xanh; cố ý làm hỏng 4 kiểu thì đều đỏ đúng bất biến). Cả 8 nhóm / 9 loại A–I đã tiêm và khôi phục thật
trên stack Docker Compose local dưới tải nền `-Load`; mọi truy vấn ES|QL trong tài liệu đã chạy được trên Elasticsearch; mỗi
nhóm có thêm một lần chạy thử bài tập tự làm; một vòng mù (`-Start` → `-Hint` mức 1–3 → `-Restore` → `-Reveal`) đã chạy để
kiểm cấu trúc. Script `incident-drill.ps1` và `catalog.json` **không** đổi và vẫn không có test tự động (sai lệch Nguyên tắc
III của 031 còn mở, xem [technical-debt.md](technical-debt.md) mục 032).

## 1. Ba mảnh, không mảnh nào nằm trong code service

| Mảnh | Nằm ở đâu | Vai trò |
|---|---|---|
| **Chuỗi tài liệu 09–17** | [`docs/kibana-quan-sat-he-thong/`](../kibana-quan-sat-he-thong/00-tong-quan-lo-trinh.md) | 8 file hướng dẫn mỗi nhóm một file (nhóm 2 gộp B và C) + file 17 gợi ý theo triệu chứng; 00 là lộ trình 01–17 |
| **Test quy ước tài liệu** | [`tests/TroubleshootGuideConventionTests`](../../tests/TroubleshootGuideConventionTests) | Đọc `catalog.json` làm khoá phủ, kiểm file nhóm, mục loại, 3 mức, rò rỉ và link; không dựng container |
| **Folder Postman 32** | [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) | 8 subfolder nhóm 1–8: request quan sát triệu chứng và xác nhận khỏi; gây lỗi/khôi phục ngoài Postman |

Không file nào dưới `services/`, `shared/`, `scripts/`, `docker/`, `deploy/` bị đổi; dashboard, rule và `catalog.json` giữ nguyên.

## 2. Khung của một file hướng dẫn nhóm

Mọi file 09–16 theo cùng một khung ([`guide-structure-contract.md`](../../specs/032-troubleshoot-practice-docs/contracts/guide-structure-contract.md)):
`Bạn sẽ thấy` · `Điều kiện` · `## Loại <mã> — …` (đích, lệnh tiêm, nơi nhìn trên Kibana, truy vấn tự đối chiếu, số đo, khôi phục,
xác nhận đã khỏi) · `Giới hạn đã biết` · `Bài tập tự làm` · `Đã đạt khi` · `Xem thêm`. File nhóm N đặt số `08 + N`. Triage, SEV và
bản ghi sự cố **chỉ liên kết** sang README của 028 và file 08, không chép.

File 17 sắp theo triệu chứng, mỗi triệu chứng ba mức mở dần (1 triệu chứng + cách kiểm, 2 nhóm lỗi, 3 đáp án), cùng nghĩa với
`-Hint` nhưng viết riêng và nặng hơn (có cách kiểm). Mức 1–2 **không** liên kết sang file 09–16 vì các file đó ghi sẵn đáp án ở
tiêu đề; các truy vấn Q1–Q9 nằm ngay trong file 17.

## 3. Nhóm lỗi, file hướng dẫn và số đo chính (2026-10-06, có tải nền)

| Nhóm / Loại | File | Triệu chứng đo được | Khôi phục |
|---|---|---|---|
| 1 / **A** | [09](../kibana-quan-sat-he-thong/09-dich-ket-noi-sai.md) | orders 53,3% 5xx, BFF 24,6%, gateway 32,3%; `/health/ready` 503; chuỗi 15 phút sạch từ 10:05 tới 10:19 | 51 s |
| 2 / **B** | [10](../kibana-quan-sat-he-thong/10-nghen-va-loi-theo-ty-le.md) | **không có triệu chứng** (100 request song song: p95 989 ms khi tiêm, 889 ms khi gỡ) | 26–31 s |
| 2 / **C** | 10 | products 23,1–25,2% 5xx từng phút, p95 7–19 ms, 0 log lỗi, không lan | 0,5 s |
| 3 / **D** | [11](../kibana-quan-sat-he-thong/11-do-tre-orders.md) | orders p95 2009–2437 ms, 0% 5xx; 311/554 span chậm (404) | 1,5 s |
| 4 / **E** | [12](../kibana-quan-sat-he-thong/12-ha-tang-dung.md) | orders 51,6% 5xx, container `unhealthy` sau vài phút | 48 s |
| 5 / **F** | [13](../kibana-quan-sat-he-thong/13-xac-thuc-hong.md) | 110/159 span 401, p95 10581 ms, container `healthy` | 57 s |
| 6 / **G** | [14](../kibana-quan-sat-he-thong/14-thieu-tai-nguyen.md) | p95 ≈ 100 ms (so với 8 ms nền), 0% 5xx; không OOM | 9 s |
| 7 / **H** | [15](../kibana-quan-sat-he-thong/15-mang-dut.md) | service biến mất khỏi dữ liệu; BFF 504, span Client lỗi không mã HTTP p50 1000 ms | 48 s |
| 8 / **I** | [16](../kibana-quan-sat-he-thong/16-container-chet-hoac-khoi-dong-lai.md) | như H; `Exited (137)`, `restartPolicy=no` | 9 s |

## 4. Test quy ước tài liệu

[`guide-convention-test-contract.md`](../../specs/032-troubleshoot-practice-docs/contracts/guide-convention-test-contract.md) liệt kê 8
bất biến: mỗi nhóm có đúng một file; mỗi loại có mục `## Loại`; đủ 6 mục bắt buộc; file 17 có mức 3 cho mọi loại A–I; mỗi triệu chứng
đủ mức 1→2→3 theo thứ tự; mức 1–2 không chứa `Loại <chữ cái>` hay tên một trong 7 service; mọi link nội bộ (kể cả neo `#…`) có đích thật;
00 liệt kê đủ 09–17. Test **không** kiểm nội dung khớp giữa tài liệu và `catalog.json` — hai nơi gợi ý được viết riêng theo
quyết định người dùng. Gốc repo tìm bằng `Ecommerce.slnx` (hỗ trợ worktree); không thêm package.

## 5. Giới hạn đã biết

- **Hai nơi gợi ý có thể lệch**: file 17 và 27 đoạn trong `catalog.json` (dùng cho `-Hint`) do hai người viết riêng; test chỉ kiểm phủ.
- **Sai lệch Nguyên tắc III của 031 còn mở**: script và catalog vẫn không có test; hạn "đến khi spec D hoàn tất" hết ở đây, người dùng quyết mở một spec riêng cho test script (2026-10-06, chưa tạo).
- **Loại B không có triệu chứng**, **G chỉ nhẹ** (chưa vượt SLO), **H/I biến mất khỏi bảng SLO** nên không hiện đỏ; **E/H/I không che đích** trong bài mù.
- **Cảnh báo không phân biệt khi tiêm nối tiếp**: cả 7 alert `incident-fast-detection` còn `active` liên tục từ 10:34:59 tới ≥ 12:07; không đo được thời gian báo của từng nhóm riêng (chỉ C và D có số).
- **Số đo phụ thuộc máy**: mỗi file ghi ngày và điều kiện đo kèm truy vấn tự đo lại.
- **Chuỗi 15 phút chỉ đo trọn cho nhóm 1**; các nhóm còn lại đo phút đầu tiên `dat = true`.

Đầy đủ: [technical-debt.md](technical-debt.md) mục 032.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/032-troubleshoot-practice-docs-component.drawio`](../diagrams/032-troubleshoot-practice-docs-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/032-troubleshoot-practice-docs-flow-nghiep-vu.drawio`](../diagrams/032-troubleshoot-practice-docs-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/032-troubleshoot-practice-docs-sequence.drawio`](../diagrams/032-troubleshoot-practice-docs-sequence.drawio)

## 7. Tham khảo thêm

Kiến trúc công cụ gây lỗi: [`031_Architect_danh mục 8 nhóm lỗi luyện troubleshoot.md`](031_Architect_danh%20mục%208%20nhóm%20lỗi%20luyện%20troubleshoot.md).
Quy trình diễn tập: [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md).
