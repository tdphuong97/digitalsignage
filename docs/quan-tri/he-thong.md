---
description: Các thành phần của hệ thống màn hình, đường đi của một file, link TV đang mở và nơi phần mềm lưu dữ liệu.
---

# Cách hệ thống hoạt động

**Ai đọc:** bộ phận IT

## Các thành phần

| Thành phần | Ở đâu | Vai trò |
| --- | --- | --- |
| Phần mềm **Signage Viên Ba** | Máy tính của người phụ trách nội dung | Nén video, gửi file lên, xóa file, xem danh sách đang chiếu. |
| Cloudflare Worker `signage` | `https://signage.vienba.workers.dev` | Nhận file, kiểm tra mã API, lưu file vào kho, phục vụ trang trình chiếu và file cho TV. |
| Kho R2 `signage-vienba` | Cloudflare | Chứa file ảnh, video, danh sách chiếu của từng TV, và trang trình chiếu `index.html`. |
| TV 1, TV 2 | Sảnh tòa nhà | Mở trang trình chiếu, chiếu lần lượt từng file rồi lặp lại. |

Màn hình LED (app Xiaohui) không thuộc hệ thống này; nội dung LED vẫn được đưa lên bằng app Xiaohui.

## Đường đi của một file

1. Người dùng thêm file và bấm **Đưa lên TV 1**. Video được nén trên máy.
2. Phần mềm gửi file tới `POST /upload?name=<tên>`, kèm mã API trong header `X-Api-Key`.
3. Worker lưu file vào R2 rồi cập nhật danh sách chiếu của TV đó.
4. TV tải lại danh sách khi hết mỗi vòng chiếu, nên nội dung mới lên từ vòng sau.

Xóa đi theo đường tương tự với `DELETE /upload?name=<tên>`. Xem danh sách dùng `GET /manifest.json?screen=<số TV>` và không cần mã.

## Link TV đang mở

| TV | Link | Tên file trong R2 |
| --- | --- | --- |
| TV 1 | `https://signage.vienba.workers.dev/` | Ở gốc kho, ví dụ `01-Chào mừng.jpg` |
| TV 2 | `https://signage.vienba.workers.dev/?screen=2` | Có tiền tố `screen2/`, ví dụ `screen2/01-Tin tức.jpg` |

Phần mềm không đổi link TV. Cài phần mềm trên máy mới không cần làm gì trên TV.

## Dữ liệu trên máy dùng phần mềm

| Đường dẫn | Chứa gì |
| --- | --- |
| `%LOCALAPPDATA%\Programs\SignageVienBa` | Phần mềm và bộ nén video (thư mục `ffmpeg`) |
| `%APPDATA%\SignageVienBa\settings.json` | **Địa chỉ máy chủ** và **Mã API** (đã mã hóa) |
| `%APPDATA%\SignageVienBa\log.txt` | Ghi lại lỗi không mong muốn, để gửi bộ phận IT |
| `%TEMP%\SignageVienBa` | Video đang nén và bộ cài bản mới tải về; tự dọn |

## Thay cho hệ thống cũ

Trước đây, nhân viên bỏ file vào thư mục OneDrive `Signage-VienBa\Screen1`, `Screen2`, và script `sync-to-cloudflare-final.ps1` chạy theo lịch trong Task Scheduler để đẩy lên. Phần mềm thay toàn bộ cách này.

{% hint style="danger" %}
Tắt mọi lịch chạy `sync-to-cloudflare-final.ps1` trong Task Scheduler. Script cũ xóa trên TV mọi file không có trong thư mục OneDrive, tức là xóa luôn những gì đưa lên bằng phần mềm.
{% endhint %}

Các file đưa lên từ hệ thống cũ vẫn ở trên TV và hiện trong phần mềm. File video cũ của TV 1 có tên bắt đầu bằng `video/`.
