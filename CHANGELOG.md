# Nhật ký thay đổi

Mỗi phiên bản là một mục `## <phiên bản>`, mỗi thay đổi một dòng. `build\publish.ps1` lấy đúng mục của phiên bản đang phát hành làm ghi chú cho bản cập nhật.

## 1.0.3
- Theo dõi TV: chấm trạng thái trên thẻ TV 1, TV 2 và dòng "TV đang chiếu · báo lúc …"; Chẩn đoán có thêm mục Tín hiệu từ TV và nhật ký của từng TV khi chép kết quả. Cần cập nhật máy chủ lên bản 2, bản này cũng cho TV mở được trang chiếu khi chưa có Wi-Fi.
- Nút Chẩn đoán ở góc trên cửa sổ: tự kiểm tra kết nối, mã API, nội dung trên 2 TV (TV trống, file nén dở, file không chiếu được), trang chiếu, bộ nén video, ổ đĩa, phiên bản, lịch đồng bộ cũ trên máy, kèm cách xử lý và nút Chép kết quả để gửi IT.
- Cài đặt > Kiểm tra bản mới: có bản mới thì nút Cập nhật ngay hiện ngay trong cửa sổ Cài đặt, không phải đóng cửa sổ rồi mới cập nhật.

## 1.0.2
- Hướng dẫn mở trong một cửa sổ riêng của phần mềm, không mở trình duyệt nữa. Các liên kết ra Internet trong hướng dẫn vẫn mở bằng trình duyệt.
- Nút Xem ở từng dòng mở ảnh, video trong cửa sổ xem của phần mềm.
- Link TV 1, TV 2 hiện ở đầu cửa sổ chính, có nút Chép và Xem (xem trang TV đang chiếu).
- Tên file trên TV không còn khoảng trắng: mỗi khoảng trắng được đổi thành dấu -, ví dụ "Chào mừng.jpg" thành "Chào-mừng.jpg". File đã đưa lên trước bản này giữ nguyên tên.

## 1.0.1
- Nút Hướng dẫn ở góc trên cửa sổ (hoặc nhấn F1) mở hướng dẫn sử dụng ngay trên máy, không cần mạng.

## 1.0.0
- Bản đầu tiên: chọn TV 1 hoặc TV 2 rồi đưa ảnh, video lên bằng một nút bấm.
- Kéo thả ảnh, video hoặc cả thư mục vào cửa sổ, hoặc bấm Chọn file / Chọn thư mục.
- Video tự chỉnh về khổ dọc 1080×1920, nền mờ, 30 hình/giây, dưới 85 MB; video đã đúng chuẩn được đưa lên thẳng.
- Danh sách nội dung đang chiếu trên từng TV, có nút Xem và Xóa.
- Mã API nhập một lần trong Cài đặt và được mã hóa theo tài khoản Windows.
- Tự kiểm tra bản mới qua Internet, bấm Cập nhật ngay là tải về và cài.
