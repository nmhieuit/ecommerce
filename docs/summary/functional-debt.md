# Ghi chú thực tế — bằng chứng đã kiểm chứng và giới hạn hiện tại của toàn bộ 20 tính năng

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

File này gom lại 2 mục vốn nằm rải rác trong từng file `0NN_PO_*.md` — "Điều đặc biệt" (bằng chứng đã
kiểm chứng thật, hoặc lỗi thật bắt được ngay lúc kiểm chứng) và "Giới hạn hiện tại" — để mỗi file tính
năng đọc gọn hơn, xúc tích hơn. Không có nội dung nào bị bỏ, chỉ gom lại 1 chỗ; những gì lặp lại giữa
nhiều tính năng đã được gộp thành 1 dòng duy nhất.

## 1. Điều đặc biệt — bằng chứng đã kiểm chứng thật / lỗi thật bắt được lúc kiểm chứng

- **[001](001_PO_dựng%20khung%204%20dịch%20vụ.md)** — Đội đã thử nghiệm cụ thể: cố tình để mã nguồn
  của 1 khối tìm cách chạm vào kho dữ liệu của khối khác — xác nhận không có bất kỳ đường nào cho phép
  điều đó xảy ra. Một đảm bảo cấu trúc, kiểm tra lặp lại được, không phải lời hứa.
- **[002](002_PO_định%20tuyến%20gateway-BFF.md)** — Trong lúc chạy thử toàn bộ luồng thật, phát hiện 1
  lỗi tinh vi: mã số theo dõi 1 yêu cầu không được giữ nguyên xuyên suốt hành trình — mã hiển thị cho
  người dùng và mã ghi trong nhật ký hệ thống là 2 giá trị khác nhau, khiến việc tra cứu sự cố vô nghĩa
  đúng lúc cần nó nhất. Đã phát hiện và sửa ngay trước khi coi tính năng hoàn thành.
- **[003](003_PO_danh%20tính%20giả%20lập%20và%20tenant.md)** — Đội đã quét toàn bộ mã nguồn để xác
  nhận không có 1 nơi nào trong hệ thống có thể chạm tới dữ liệu lưu trữ mà không yêu cầu biết trước
  thông tin khách hàng doanh nghiệp — không phải kiểm tra vài trường hợp mẫu, mà toàn bộ. Kết quả: zero
  ngoại lệ.
- **[004](004_PO_SPA%20mua%20sắm%20tối%20thiểu.md)** — Toàn bộ luồng 4 bước đã kiểm thử bằng cách điều
  khiển 1 trình duyệt thật đi qua đúng hành trình người mua sắm thật sẽ làm — không lỗi nào hiện ra
  trong bảng điều khiển trình duyệt. Đội cũng đo thời lượng tải trang thực tế và đặt giới hạn tự động
  chặn phát hành nếu 1 bản cập nhật sau này làm trang chậm hơn ngưỡng cho phép.
- **[005](005_PO_chạy%20local%20một%20lệnh.md)** — Đội đã chạy đi chạy lại và đo thời gian thật: lần
  đầu ~85 giây tới khi mọi thành phần sẵn sàng; luồng mua sắm đầu-cuối qua giao diện thật hoàn tất dưới
  11 giây; cố tình gỡ 1 thành phần cần thiết, hệ thống báo lỗi rõ ràng trong 89 giây, nêu đúng tên
  thành phần thiếu. Trong lúc kiểm chứng cũng phát hiện và sửa 1 vấn đề thật: 1 tuỳ chọn hiển thị thêm
  thông tin kỹ thuật ban đầu không hoạt động đúng.
- **[006](006_PO_demo%20đặt%20hàng%20end-to-end.md)** — Trong lúc tự động hoá buổi demo, phát hiện và
  sửa 3 vấn đề thật: kịch bản demo tự động từng dừng nhầm vì hiểu sai 1 cảnh báo vô hại là lỗi nghiêm
  trọng; cơ chế "bằng chứng mỗi thành phần đã thực sự tham gia xử lý" ban đầu bị nhiễu bởi tín hiệu
  kiểm tra sức khoẻ định kỳ (không phải hoạt động thật); 1 trong 4 ảnh chụp minh hoạ ban đầu giống hệt
  ảnh trước đó — không thực sự chứng minh điều nó được cho là chứng minh, đã thay bằng ảnh khác.
- **[009](009_PO_TDD%20hồi%20tố%20giỏ%20hàng%20và%20đơn%20hàng.md)** — Sau khi rà soát kỹ, xác nhận
  toàn bộ quy tắc tính tiền quan trọng đã được xây đúng và đã có bài kiểm tra bảo vệ từ trước — không
  có lỗ hổng thực sự nào cần vá. Thay vì viết lại mã đã hoạt động đúng, đội chọn chứng minh lại bằng
  thực nghiệm (cố tình phá vỡ từng quy tắc, xác nhận test bắt được, rồi khôi phục) và bổ sung ghi chú
  kỷ luật làm việc cho tương lai.
- **[013](013_PO_cổng%20chất%20lượng%20CI.md)** — Đội đã thử nghiệm bằng cách cố tình tạo 1 thay đổi
  không đạt chuẩn, rồi cố đưa vào hệ thống chính bằng chính tài khoản có quyền cao nhất — nút "đưa vào
  hệ thống chính" bị vô hiệu hoá hoàn toàn, không có bất kỳ tuỳ chọn "cứ làm bất chấp" nào xuất hiện ở
  đâu cả.
- **[014](014_PO_máy%20chủ%20định%20danh%20thật.md)** — Hai điều đã được thử nghiệm thật: đi vòng qua
  cổng vào chính (gửi thẳng 1 vé giả mạo tới bộ phận phía sau) vẫn bị chặn ngay, không cần ai "báo
  trước"; công tắc khẩn cấp hoạt động thật ngay trên hệ thống đang chạy (không dừng, không triển khai
  lại) — cổng vào chính lập tức ngừng đòi vé, trong khi các bộ phận phía sau vẫn tiếp tục tự đòi vé của
  riêng chúng, chứng minh công tắc chỉ kiểm soát đúng lớp được thiết kế để kiểm soát.
- **[015](015_PO_phân%20quyền%20từ%20chối%20theo%20mặc%20định.md)** — 3 điều đã kiểm chứng thật: thợ
  xây "quên dán biển" thật sự bị chặn (thêm 1 cửa thử nghiệm không dán biển, thanh tra tự động từ chối
  ngay, nêu đúng cửa vi phạm); từ chối "thiếu đúng loại thẻ" hoạt động thật ở cả 5 bộ phận nghiệp vụ,
  không chỉ 1 nơi; quy tắc nghiệp vụ mà giao diện web tự kiểm tra cũng được xác nhận có kiểm tra độc
  lập ở máy chủ bằng phép thử gọi thẳng, bỏ qua hoàn toàn giao diện web.
- **[016](016_PO_truy%20vết%201%20yêu%20cầu%20xuyên%20suốt%20hệ%20thống.md)** — 3 điều đã kiểm chứng
  thật: gõ 1 đơn hàng thật rồi tra lại đúng dấu vết của nó — mã theo dõi xuất hiện ở mọi bộ phận tham
  gia xử lý, kể cả phần xử lý nền không đồng thời; phát hiện và vá 1 lỗ hổng thật đang tồn tại — trước
  tính năng này, các bộ phận nghiệp vụ được gọi qua tầng tổng hợp tự sinh mã theo dõi riêng thay vì
  mang theo mã từ cổng chính, khiến log của cổng chính và log các bộ phận phía sau không hề nối được
  với nhau; nhiều yêu cầu cùng lúc không bị lẫn lộn — đã thử gửi đồng thời nhiều yêu cầu, xác nhận mã
  theo dõi của mỗi yêu cầu luôn tách biệt rõ ràng.
- **[017](017_PO_nhìn%20thấy%20hệ%20thống%20đang%20chạy%20ra%20sao%20qua%20Elastic.md)** — 3 điều đã
  kiểm chứng thật: đặt 1 đơn hàng thật, tìm thấy đủ dấu vết trên Kibana xuyên suốt các bộ phận tham
  gia; rà soát toàn bộ mã nguồn xác nhận không còn cách ghi log kiểu "câu chữ tự do lắp ráp"; thử
  nghiệm "rút thành phần dùng chung ra xem có sao không" — gỡ tạm khỏi 1 bộ phận, xác nhận bộ phận đó
  thực sự mất khả năng gửi báo cáo, chứng minh thành phần dùng chung thực sự cần thiết chứ không phải
  lớp cấu hình trang trí.
- **[018](018_PO_bỏ%20hẳn%20mật%20khẩu%20viết%20cứng%20trong%20code.md)** — Chạy công cụ quét bí mật
  thật trên toàn bộ lịch sử kho mã nguồn, không chỉ thay đổi mới nhất — các mật khẩu cũ (đã xoá khỏi
  phiên bản hiện tại nhưng còn dấu vết lịch sử, không viết lại lịch sử vì rủi ro cao) được ghi nhận "đã
  biết, đã khắc phục" 1 lần, công cụ quét từ nay chỉ báo động phát hiện mới. Đã chuẩn bị sẵn "hợp đồng"
  cho từng bộ phận cần mật khẩu gì, để khi kho bí mật trung tâm thật được dựng, việc kết nối chỉ còn là
  áp dụng đúng hợp đồng có sẵn.
- **[019](019_PO_hệ%20thống%20tự%20biết%20khi%20nào%201%20dịch%20vụ%20sẵn%20sàng.md)** — Ngưỡng thời
  gian không phải số tự nghĩ ra — lấy đúng từ số liệu đã kiểm chứng qua vận hành thật (thời gian cơ sở
  dữ liệu cần để phục hồi sau khởi động lại). Có bộ kiểm tra tự động xác nhận đúng 4 quy tắc cho mọi bộ
  phận, không sót cái nào. Có lớp kiểm tra thứ 2 độc lập, dùng đúng công cụ chuẩn của Kubernetes để xác
  nhận bản thiết kế hợp lệ về mặt kỹ thuật.
- **[020](020_PO_hệ%20thống%20không%20còn%20treo%20vô%20thời%20hạn%20khi%201%20dịch%20vụ%20khác%20gặp%20sự%20cố.md)** —
  Phát hiện 1 lỗi thật đang tồn tại sẵn: cơ chế "ngắt mạch" ở 1 điểm quan trọng thực ra không hoạt động
  đúng như tưởng — hệ thống vẫn âm thầm cố kết nối thật dù đã "báo mạch mở", chỉ vì 1 cấu hình mặc định
  ẩn của công cụ nền tảng; đã xác thực bằng cách chạy thật và đo thời gian phản hồi, sửa đúng chỗ. Phát
  hiện 1 lỗi tiềm ẩn thật đã tồn tại từ trước: cơ chế "thử lại tự động" cũ áp dụng cho cả yêu cầu tạo
  đơn hàng/giỏ hàng — rủi ro tạo dữ liệu trùng lặp thật sự đã tồn tại âm thầm trước khi tính năng này
  được làm, đã khép lại đúng khoảng hở này.
- **[021](021_PO_biết%20ngay%20service%20nào%20đang%20lố%20ngân%20sách%20hiệu%20năng%20đã%20cam%20kết.md)** —
  Việc viết bài kiểm tra TRƯỚC khi biết kết quả đã tự sửa 2 điều đội ngũ tưởng nhầm: (1) tưởng "mọi bộ
  phận đã khai báo đầy đủ" — chạy kiểm tra thật phát hiện đúng 1 bộ phận thiếu 1 trường bắt buộc, đã
  sửa ngay; (2) tưởng "1 bộ phận cụ thể là ngoại lệ cần giải trình" — kiểm tra thật cho thấy không
  đúng, bộ phận đó chỉ thuộc 1 nhóm tiêu chuẩn khác tương đương, đã sửa lại đúng ghi chép, không bịa lý
  do giả. Đã tự tạo 1 đợt tải giả lập thật chứng minh nơi tra cứu phản ánh đúng: độ trễ đo được tăng
  ~95 lần ngay khi tạo tải cao. Đã xác nhận phân biệt đúng "không có dữ liệu" với "không có lỗi".

## 2. Giới hạn hiện tại

- **[001](001_PO_dựng%20khung%204%20dịch%20vụ.md)** — Đây thuần tuý là bước dựng khung — 4 khối chỉ
  mới báo cáo tình trạng của chính mình, chưa có chức năng nghiệp vụ thật nào. Việc xác định "1 người
  dùng thuộc khách hàng doanh nghiệp nào" chưa nằm trong phạm vi này — bước tiếp theo trong lộ trình.
- **[002](002_PO_định%20tuyến%20gateway-BFF.md)** — Đây là bước đầu về định tuyến và ghép nối dữ liệu
  — chưa bao gồm xác thực người dùng thật hay xác định khách hàng doanh nghiệp nào đang gọi. Cơ chế
  phục hồi sự cố nâng cao hơn (tự động thử lại thông minh khi 1 mảng chập chờn) là bước tiếp theo, chưa
  nằm trong phạm vi này.
- **[003](003_PO_danh%20tính%20giả%20lập%20và%20tenant.md)** — Vẫn là danh tính giả lập — chưa có đăng
  nhập thật, chưa có mật khẩu (đã hoàn thành sau đó ở "014 — Máy chủ định danh thật"). Việc lan truyền
  thông tin khách hàng doanh nghiệp sang các sự kiện bất đồng bộ chưa nằm trong phạm vi này, vì hạ tầng
  đó chưa tồn tại tại thời điểm triển khai.
- **[004](004_PO_SPA%20mua%20sắm%20tối%20thiểu.md), [005](005_PO_chạy%20local%20một%20lệnh.md)** —
  Việc tách biệt dữ liệu theo từng khách hàng doanh nghiệp (tenant) ở tầng lưu trữ đã được ĐẶC TẢ từ
  trước nhưng trên thực tế **chưa được triển khai đầy đủ**. Lần đầu nêu ra ở 004 (thêm dữ liệu nghiệp
  vụ đầu tiên ngay trên nền khoảng cách này, chưa có quyết định đóng lại). Nêu lại ở 005 (chạy chung 1
  máy chủ cơ sở dữ liệu khiến dễ nhận ra hơn, không làm tệ hơn) — đây là quyết định đang chờ người phụ
  trách kỹ thuật xác nhận, không phải bị bỏ sót trong im lặng.
- **[004](004_PO_SPA%20mua%20sắm%20tối%20thiểu.md)** (riêng) — Đây là luồng tối thiểu: chưa có thanh
  toán thật, chưa có địa chỉ giao hàng, chưa có thuế/giảm giá, chưa có giữ chỗ tồn kho. Chưa có xoá bớt
  sản phẩm khỏi giỏ hay sửa số lượng trực tiếp. Chưa có lịch sử đơn hàng.
- **[005](005_PO_chạy%20local%20một%20lệnh.md)** (riêng) — Đây là môi trường thử nghiệm cục bộ trên
  máy cá nhân, không phải hạ tầng vận hành chính thức — một số cách sắp xếp (dùng chung 1 máy chủ cơ sở
  dữ liệu) là lựa chọn có chủ đích riêng cho việc chạy thử.
- **[006](006_PO_demo%20đặt%20hàng%20end-to-end.md)** — Một bước cuối cùng vẫn cần người thực hiện thủ
  công: đính video vào Jira — công cụ tự động không hỗ trợ đính kèm file, và đăng nội dung công khai là
  việc cần người chủ động thực hiện. Demo chạy trên môi trường thử nghiệm cục bộ, chưa phải hạ tầng vận
  hành chính thức. Demo vẫn dùng danh tính giả lập (1 khách hàng doanh nghiệp duy nhất) — chưa chứng
  minh tách biệt dữ liệu giữa nhiều khách hàng doanh nghiệp.
- **[007](007_PO_hợp%20đồng%20OpenAPI%20cho%20BFF.md)** — Phạm vi chỉ gồm 3 mảng nghiệp vụ (sản phẩm,
  giỏ hàng, đơn hàng) — mảng khách hàng, luồng thanh toán, đường kiểm tra sức khoẻ hệ thống chưa áp
  dụng cơ chế này. Đây chủ yếu là bước xác nhận và củng cố 1 thực hành đã có sẵn, không phải xây từ
  số 0.
- **Bộ nhớ đệm/hàng đợi tin nhắn đã dựng sẵn nhưng chưa có chức năng nào dùng** —
  [005](005_PO_chạy%20local%20một%20lệnh.md), [008](008_PO_event%20schema%20có%20version.md),
  [010](010_PO_hạ%20tầng%20kiểm%20thử%20container%20thật.md),
  [011](011_PO_kiểm%20thử%20hợp%20đồng%20tiêu%20dùng.md),
  [020](020_PO_hệ%20thống%20không%20còn%20treo%20vô%20thời%20hạn%20khi%201%20dịch%20vụ%20khác%20gặp%20sự%20cố.md)
  — chưa có cơ chế truyền thông điệp thật sự nào được kết nối/đang chạy; các thành phần hạ tầng đứng
  sẵn chờ tính năng đầu tiên thực sự cần tới chúng.
- **[008](008_PO_event%20schema%20có%20version.md)** (riêng) — Phạm vi chỉ giới hạn ở đúng 2 loại
  thông báo quan trọng nhất ("đơn hàng vừa đặt", "giỏ hàng vừa thanh toán") — loại khác trong tương lai
  cần lặp lại đúng khuôn mẫu riêng.
- **[009](009_PO_TDD%20hồi%20tố%20giỏ%20hàng%20và%20đơn%20hàng.md)** — Phạm vi rà soát chỉ dừng ở các
  quy tắc tính toán nội bộ, không phải kiểm thử toàn bộ đường đi qua hệ thống thật — mức kiểm thử này
  là đủ và đúng loại theo yêu cầu ban đầu. Không có thay đổi hành vi nào cho người dùng cuối trong tính
  năng này.
- **[010](010_PO_hạ%20tầng%20kiểm%20thử%20container%20thật.md)** (riêng) — Việc chịu đựng sự cố hàng
  đợi tin nhắn ở mức toàn diện (tự động thử lại, ngắt mạch khi lỗi liên tục...) chưa nằm trong phạm vi
  này — tính năng này chỉ đảm bảo bài kiểm tra không bị treo.
- **[011](011_PO_kiểm%20thử%20hợp%20đồng%20tiêu%20dùng.md)** (riêng) — Phạm vi hiện tại chỉ dừng ở
  đúng 4 đường giao tiếp quan trọng nhất — mở rộng ra đường khác trong tương lai là việc riêng. Với cặp
  thông báo nội bộ, đây là bước thí điểm đi trước, chuẩn bị cho khi hạ tầng nhắn tin được kết nối.
- **Hạ tầng vận hành sản phẩm thật chưa dựng, mỗi tính năng 1 mảnh khác nhau** —
  [013](013_PO_cổng%20chất%20lượng%20CI.md),
  [017](017_PO_nhìn%20thấy%20hệ%20thống%20đang%20chạy%20ra%20sao%20qua%20Elastic.md),
  [018](018_PO_bỏ%20hẳn%20mật%20khẩu%20viết%20cứng%20trong%20code.md),
  [019](019_PO_hệ%20thống%20tự%20biết%20khi%20nào%201%20dịch%20vụ%20sẵn%20sàng.md) — mỗi tính năng còn
  thiếu 1 mảnh hạ tầng vận hành chính thức khác nhau, không phải cùng 1 việc:
  - 013: đây là môi trường thử nghiệm cục bộ, không phải hạ tầng vận hành chính thức trên máy chủ
    chuyên dụng — việc cơ chế *hoạt động đúng* đã kiểm chứng đầy đủ, việc *dựng nó trên hạ tầng vận
    hành chính thức lâu dài* là việc riêng. Một số điều chỉnh kỹ thuật đã xác minh hoạt động tốt trong
    thử nghiệm nhưng hiện chưa được đưa chính thức vào nhánh mã nguồn chính.
  - 017: kho Elastic/Kibana hiện chỉ chạy trên máy phát triển — chưa phải hạ tầng vận hành thật, luôn
    sẵn sàng cho môi trường sản phẩm chính thức.
  - 018: kho bí mật trung tâm thật của cluster (HashiCorp Vault, và bộ phận đồng bộ nó) **CHƯA được
    dựng ở đâu cả** — quyết định phạm vi có chủ đích: chỉ hoàn thành nửa ứng dụng (mã nguồn sẵn sàng
    nhận mật khẩu đúng cách), nửa hạ tầng (dựng kho bí mật thật, nối vào) vẫn để ngỏ, thuộc đội hạ
    tầng.
  - 019: **chưa có 1 hệ thống Kubernetes thật nào đang chạy hệ thống này** — những gì hoàn thành là bản
    thiết kế triển khai đã kiểm chứng kỹ (khai báo đúng, đã lint, có bộ test tự động canh giữ quy ước),
    sẵn sàng áp dụng lên 1 cluster thật ngay khi cluster đó tồn tại, nhưng việc áp dụng thật vẫn chưa
    xảy ra.
- **[014](014_PO_máy%20chủ%20định%20danh%20thật.md)** — Màn hình đăng nhập tương tác chưa được xây
  trong phần này — công việc riêng, đã ghi nhận để làm tiếp; toàn bộ phần "cấp vé, kiểm tra vé, từ
  chối vé giả/hết hạn" đã hoạt động và kiểm chứng thật. Trong lúc kiểm chứng lần chạy thử cuối cùng,
  đội phát hiện và vá luôn 3 lỗ hổng cấu hình thật mà không bài kiểm tra tự động nào bắt được trước đó.
- **[015](015_PO_phân%20quyền%20từ%20chối%20theo%20mặc%20định.md)** — Một số phép thử cần nhiều bộ
  phận nói chuyện qua mạng nội bộ đã không chạy được trong đúng phiên làm việc cuối — vấn đề môi trường
  máy phát triển tại đúng thời điểm đó (máy chủ container vừa khởi động lại, mạng nội bộ chưa ổn định),
  không phải lỗi tính năng; mọi phép thử chạy trực tiếp trên từng bộ phận riêng lẻ đều đạt kết quả
  đúng. Phân quyền theo vai trò chi tiết chưa nằm trong phạm vi tính năng này.
- **[016](016_PO_truy%20vết%201%20yêu%20cầu%20xuyên%20suốt%20hệ%20thống.md)** — Mã theo dõi là công cụ
  theo dõi vận hành, không phải cơ chế bảo mật — chỉ kiểm tra loại bỏ ký tự nguy hiểm và giới hạn độ
  dài, không thiết kế để khó đoán/chống giả mạo như 1 token xác thực.
- **[020](020_PO_hệ%20thống%20không%20còn%20treo%20vô%20thời%20hạn%20khi%201%20dịch%20vụ%20khác%20gặp%20sự%20cố.md)** (riêng) —
  Việc bảo vệ này mới áp dụng cho các lời gọi qua HTTP giữa các bộ phận đã tồn tại thật. Chưa có cơ chế
  phát hiện & loại bỏ đơn hàng trùng lặp mang tính triệt để — giải pháp hiện tại là biện pháp giảm
  thiểu hợp lý, không phải lời giải cuối cùng. Khả năng "nhìn thấy" các sự kiện bảo vệ này qua công cụ
  giám sát vận hành chưa được xác nhận hoạt động thật trong phiên hoàn thành tính năng — mới xác nhận ở
  mức cấu hình.
- **[021](021_PO_biết%20ngay%20service%20nào%20đang%20lố%20ngân%20sách%20hiệu%20năng%20đã%20cam%20kết.md)** —
  Chưa có cảnh báo tự động — hệ thống hiện chỉ hỗ trợ "tra cứu khi cần". Chưa có cơ chế kiểm tra tự động
  chạy liên tục để đảm bảo nơi tra cứu luôn khớp đúng dữ liệu gốc — việc đối chiếu hiện làm định kỳ/thủ
  công. 1 chỉ tiêu (độ trễ ở mức hiếm gặp nhất, p99) của bộ phận xử lý đơn hàng hiện đo được khá gần
  với ngưỡng đã cam kết — đáng theo dõi tiếp, chưa phải vấn đề cần xử lý gấp.
