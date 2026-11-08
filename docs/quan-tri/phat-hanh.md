---
description: Build, kiểm thử và phát hành một bản mới để mọi máy tự cập nhật.
---

# Phát hành bản mới

**Ai làm:** người bảo trì mã nguồn · **Mất khoảng:** 10 phút

{% hint style="info" %}
**Trước khi bắt đầu:** máy build cần .NET 10 SDK, Inno Setup 6 (`winget install JRSoftware.InnoSetup`), GitHub CLI đã đăng nhập tài khoản có quyền ghi vào repo [tdphuong97/digitalsignage](https://github.com/tdphuong97/digitalsignage), một bản ffmpeg dạng *shared build* cho thư mục `deps\ffmpeg`, Python 3 và bộ build hướng dẫn `guidebook.py` (biến `GUIDEBOOK_PY`, mặc định nằm trong `~\.claude\skills\guidebook\scripts`).
{% endhint %}

{% stepper %}
{% step %}
### Tăng số phiên bản

Sửa `<Version>` trong `src\SignageVienBa.App\SignageVienBa.App.csproj`, ví dụ từ `1.0.0` thành `1.0.1`.
{% endstep %}

{% step %}
### Ghi thay đổi

Thêm mục `## 1.0.1` vào đầu `CHANGELOG.md`, mỗi thay đổi một dòng. Phần mềm hiện các dòng này ở thanh **Có bản mới …**, nên viết cho người dùng đọc. Thêm mục tương ứng vào `docs\tham-khao\nhat-ky-thay-doi.md` và sửa `version`, `updated` trong `docs\guidebook.json`.
{% endstep %}

{% step %}
### Commit và push

Commit mọi thay đổi và push lên nhánh `main`.
{% endstep %}

{% step %}
### Build và phát hành

Chạy `.\build.ps1 -Publish`. Script chạy test, build phần mềm, build bản hướng dẫn cho người dùng (`HuongDan.html`, không có các trang Quản trị), tạo bộ cài, rồi tạo release `v<phiên bản>` (ví dụ `v1.0.2`) trên GitHub kèm bộ cài và `latest.json`.

Muốn xem trước mà không thay đổi gì trên GitHub: `.\build\publish.ps1 -DryRun`.
{% endstep %}
{% endstepper %}

{% hint style="success" %}
**Kết quả:** trang [Releases](https://github.com/tdphuong97/digitalsignage/releases) có bản mới. Mỗi máy thấy thanh **Có bản mới 1.0.1** ở lần mở tiếp theo, hoặc trong vòng 6 tiếng nếu phần mềm đang mở.
{% endhint %}

`build\publish.ps1` từ chối phát hành khi còn thay đổi chưa commit, khi commit chưa được push, hoặc khi bản đó đã được phát hành. Chi tiết về build và kiểm thử nằm trong `README.md` ở gốc repo.
