# Signage Viên Ba

Phần mềm Windows đưa ảnh và video lên các TV trình chiếu (digital signage) của tòa nhà Viên Ba. Chọn TV, kéo file vào, bấm một nút: phần mềm tự nén video ngay trên máy rồi gửi lên máy chủ Cloudflare Worker mà các TV đang mở.

Hướng dẫn sử dụng đầy đủ (cho người dùng và bộ phận IT, định dạng GitBook) nằm trong [docs/](docs/README.md).

## Dùng

1. Cài bằng `SignageVienBa-Setup-<phiên bản>.exe` ở mục [Releases](../../releases). Không cần quyền admin.
2. Lần đầu mở, cửa sổ **Cài đặt** tự hiện: nhập mã API (hỏi bộ phận IT) rồi bấm **Kiểm tra kết nối**. Mã được mã hóa bằng DPAPI, chỉ dùng được với tài khoản Windows đó.
3. Chọn **TV 1** hoặc **TV 2**, kéo thả ảnh, video hoặc cả thư mục vào cửa sổ (hoặc bấm **Chọn file…**), rồi bấm **Đưa lên TV**.

- Ảnh JPG/PNG được đưa lên nguyên bản.
- Video được chỉnh về khổ dọc 1080×1920, nền mờ, H.264, 30 hình/giây, dưới 85 MB (Cloudflare chặn file trên 100 MB). Video đã đúng chuẩn thì đưa lên thẳng.
- Cột bên trái là những gì TV đang chiếu, theo thứ tự tên file từ A đến Z; bấm **Xóa** để gỡ khỏi TV.

## Cập nhật

Mỗi lần mở, phần mềm đọc `latest.json` của bản phát hành mới nhất:

```
https://github.com/tdphuong97/digitalsignage/releases/latest/download/latest.json
```

Nếu có bản mới hơn, phần mềm hỏi người dùng. Bấm **Cập nhật ngay** thì phần mềm tải bộ cài, kiểm tra dung lượng và SHA-256 khớp với `latest.json`, chạy bộ cài ở chế độ im lặng (`/VERYSILENT /UPDATE=1`) rồi tự đóng. Bộ cài chờ phần mềm đóng hẳn, cài đè, rồi mở lại phần mềm.

## Build

Cần .NET 10 SDK và Inno Setup 6 (`winget install JRSoftware.InnoSetup`).

```powershell
.\build.ps1                 # tests, app, bộ cài trong dist\
.\build.ps1 -SkipTests      # bỏ qua tests
.\build.ps1 -SkipInstaller  # dừng sau dist\app
```

`build.ps1` cần ffmpeg dạng *shared build* (ffmpeg.exe, ffprobe.exe và các DLL) trong `deps\ffmpeg`; nếu thư mục trống, script thử chép từ một bản có sẵn trên máy build. Bộ cài mang theo thư mục `ffmpeg` này.

## Phát hành bản mới

1. Tăng `<Version>` trong `src\SignageVienBa.App\SignageVienBa.App.csproj`.
2. Thêm mục `## <phiên bản>` vào `CHANGELOG.md`, mỗi thay đổi một dòng.
3. Commit và push.
4. Chạy `.\build.ps1 -Publish`.

`build\publish.ps1` từ chối phát hành khi còn thay đổi chưa commit, khi commit chưa được push, hoặc khi bản phát hành đó đã có. Nó tạo release `v<phiên bản>` bằng GitHub CLI (`gh`), đính kèm bộ cài và `latest.json`. Chạy `.\build\publish.ps1 -DryRun` để xem trước `latest.json` và ghi chú phát hành mà không thay đổi gì trên GitHub.

## Kiểm thử

```powershell
dotnet test tests\SignageVienBa.Tests
```

- Các test dùng ffmpeg thật chỉ chạy khi `SIGNAGE_FFMPEG_DIR` trỏ tới thư mục có ffmpeg.exe và ffprobe.exe.
- Các test đầu-cuối (E2E) chạy với một bản Worker giả lập trên máy, không bao giờ với máy chủ thật. `tests\e2e\start-local-worker.ps1` chạy `wrangler dev` (cần Node.js) với mã nguồn Worker ở thư mục `..\worker`, tức là thư mục cha của repo này trong dự án gốc; repo này không chứa mã Worker. Sau khi Worker giả lập báo *Ready*:

```powershell
$env:SIGNAGE_E2E_URL = 'http://127.0.0.1:8787'
$env:SIGNAGE_E2E_KEY = 'test-key'
dotnet test tests\SignageVienBa.Tests
.\tests\e2e\stop-local-worker.ps1
```

## Cấu trúc

```
build.ps1                  build tests, app, bộ cài; -Publish để phát hành
build\SignageVienBa.iss    bộ cài Inno Setup (cài theo người dùng)
build\publish.ps1          tạo GitHub release + latest.json
src\SignageVienBa.Core     tên file, gọi API Worker, nén video, cập nhật
src\SignageVienBa.App      giao diện WPF
tests\SignageVienBa.Tests  unit, ffmpeg thật, E2E
tests\e2e                  bật/tắt Worker giả lập
CHANGELOG.md               ghi chú từng phiên bản
```
