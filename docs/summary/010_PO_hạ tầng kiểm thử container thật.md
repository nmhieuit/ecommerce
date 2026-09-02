# Kiểm thử bằng hàng thật, không phải hàng giả — để không lỗi nào lọt lưới vì đã "test giả"

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành, xác minh bằng kiểm tra tự động.*

## Vấn đề trước đây

Một cách kiểm thử phổ biến (nhưng rủi ro) là dùng "hàng giả" thay cho cơ sở dữ liệu hoặc hệ thống hạ
tầng thật khi chạy bài kiểm tra — nhanh hơn, nhưng có thể **che giấu những lỗi chỉ xảy ra với hệ thống
thật** (ví dụ một ràng buộc dữ liệu bị vi phạm mà chỉ cơ sở dữ liệu thật mới phát hiện được). Một bài
kiểm tra "xanh" (pass) trong trường hợp này có thể đang lừa dối, không thực sự chứng minh điều gì.

## Giải pháp: kiểm thử bằng đúng phần mềm hạ tầng thật, khởi động tạm thời chỉ để phục vụ bài kiểm tra

Nền tảng này chọn cách kiểm thử nghiêm ngặt hơn: mỗi lần chạy bài kiểm tra, một bản sao thật của phần
mềm hạ tầng cần thiết (cơ sở dữ liệu, bộ nhớ đệm, hàng đợi tin nhắn) được khởi động tạm thời chỉ để
phục vụ đúng lượt kiểm tra đó, rồi dọn dẹp ngay sau khi xong — không dùng hàng giả nào cả. Tính năng
này rà soát lại cơ chế đã có cho cơ sở dữ liệu, và dựng thêm cơ chế tương tự cho hai thành phần hạ
tầng còn lại mà nền tảng đã cam kết dùng nhưng chưa có tính năng nào thực sự cần tới.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Xác nhận lại bằng thực nghiệm rằng cơ chế kiểm thử cơ sở dữ liệu hiện có là kiểm thử thật**: đội
   cố tình gỡ bỏ một ràng buộc dữ liệu quan trọng và xác nhận bài kiểm tra bắt được lỗi ngay — không
   phải "trông có vẻ đúng", mà chứng minh bằng cách cố tình phá vỡ.
2. **Dựng sẵn cơ chế kiểm thử tương tự cho bộ nhớ đệm và hàng đợi tin nhắn** — hai thành phần hạ tầng
   chưa có tính năng nào dùng tới, nhưng khi tính năng đầu tiên cần tới chúng trong tương lai, cơ chế
   kiểm thử đã sẵn sàng, không phải xây từ đầu.
3. **Nếu một trong các thành phần hạ tầng không khởi động được khi chạy kiểm tra**, hệ thống báo lỗi
   rõ ràng nêu đúng tên thành phần gặp vấn đề — không bao giờ âm thầm bỏ qua bài kiểm tra liên quan.
4. **Nếu hàng đợi tin nhắn "chết" đột ngột giữa lúc đang kiểm tra**, bài kiểm tra liên quan thất bại
   trong thời gian giới hạn rõ ràng — không bao giờ treo vô thời hạn chờ đợi một thứ đã không còn.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/010-testcontainers-integration-tests-flow-nghiep-vu.drawio`](../diagrams/010-testcontainers-integration-tests-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Độ tin cậy của bộ kiểm tra tự động cao hơn hẳn** — một bài kiểm tra "xanh" thực sự có nghĩa là
  "đã kiểm chứng với hệ thống thật", không phải một lời hứa suông.
- **Sẵn sàng cho các tính năng tương lai cần bộ nhớ đệm hoặc hàng đợi tin nhắn** — không phải dựng hạ
  tầng kiểm thử từ số 0 mỗi lần một tính năng mới cần tới.
- **Không lãng phí thời gian chờ đợi vô ích khi hạ tầng gặp sự cố lúc kiểm tra** — mọi trường hợp lỗi
  đều được báo nhanh, rõ ràng.

## Giới hạn hiện tại — trung thực cần biết

- Tính năng này **chỉ dựng cơ chế kiểm thử**, không thêm bất kỳ chức năng nghiệp vụ mới nào dùng bộ
  nhớ đệm hay hàng đợi tin nhắn — chưa có mảng nghiệp vụ nào thực sự sử dụng hai thành phần hạ tầng
  này trong hệ thống đang chạy. Đó là công việc của các tính năng riêng trong tương lai.
- Việc chịu đựng sự cố hàng đợi tin nhắn ở mức toàn diện (tự động thử lại, ngắt mạch khi lỗi liên
  tục...) chưa nằm trong phạm vi này — tính năng này chỉ đảm bảo bài kiểm tra không bị treo, chưa phải
  toàn bộ chiến lược chịu lỗi cho hệ thống thật.
