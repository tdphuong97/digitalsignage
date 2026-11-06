---
description: Các ô và nút trong cửa sổ Cài đặt, giá trị mặc định và ý nghĩa.
---

# Tùy chọn

Bấm **Cài đặt** ở góc trên bên phải cửa sổ chính. Cửa sổ **Cài đặt kết nối** mở ra. Bấm **Lưu** để ghi lại, **Hủy** để bỏ thay đổi.

## Kết nối

| Tùy chọn | Mặc định | Ý nghĩa |
| --- | --- | --- |
| **Địa chỉ máy chủ** | `https://signage.vienba.workers.dev` | Địa chỉ máy chủ mà các TV đang mở. Chỉ đổi khi bộ phận IT yêu cầu. |
| **Mã API** | Trống | Mật khẩu để được đưa file lên và xóa file. Do bộ phận IT cấp, xem [Mã API](../quan-tri/ma-api.md). Được mã hóa, chỉ dùng được với tài khoản Windows đang dùng. |
| **Kiểm tra kết nối** | | Thử đọc danh sách TV 1 và thử mã API. Kết quả hiện ngay bên cạnh nút: **Kết nối được, mã API đúng.** hoặc lý do lỗi. |

## Phiên bản

| Tùy chọn | Ý nghĩa |
| --- | --- |
| **Phiên bản …** | Số phiên bản đang dùng. Ghi lại số này khi báo lỗi. |
| **Kiểm tra bản mới** | Kiểm tra ngay thay vì chờ lần mở sau. Nếu có bản mới, thanh **Có bản mới …** hiện ở cửa sổ chính, xem [Cập nhật phần mềm](../su-dung/cap-nhat-phan-mem.md). |

## Biến môi trường (dành cho IT)

<details>

<summary>Các biến môi trường phần mềm đọc khi khởi động</summary>

Dùng khi thử nghiệm hoặc cài theo cách riêng. Người dùng bình thường không cần đặt.

| Biến | Ý nghĩa |
| --- | --- |
| `SIGNAGE_DATA` | Thư mục lưu `settings.json` và `log.txt` thay cho `%APPDATA%\SignageVienBa`. |
| `SIGNAGE_FFMPEG_DIR` | Thư mục chứa `ffmpeg.exe` và `ffprobe.exe` thay cho thư mục `ffmpeg` cạnh phần mềm. |
| `SIGNAGE_UPDATE_FEED` | Địa chỉ `latest.json` thay cho bản phát hành mới nhất trên GitHub, để thử cập nhật. |

</details>
