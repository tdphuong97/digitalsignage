---
description: Tắt lịch đồng bộ OneDrive cũ, cài phần mềm cho người đưa nội dung lên TV, thử với một ảnh và dọn file nén dở còn sót trên TV.
---

# Chuyển sang phần mềm mới

**Ai làm:** bộ phận IT · **Khi nào:** một lần, trước khi người dùng bắt đầu dùng phần mềm · **Mất khoảng:** 20 phút

{% hint style="info" %}
**Trước khi bắt đầu:** chuẩn bị **mã API** (xem [Mã API](ma-api.md)) và ngồi được vào máy đang chạy script đồng bộ cũ, tức là máy có thư mục OneDrive `Signage-VienBa`.
{% endhint %}

{% stepper %}
{% step %}
### Tắt lịch đồng bộ cũ

Script cũ `sync-to-cloudflare-final.ps1` xóa trên TV mọi file không có trong thư mục OneDrive. Nếu còn chạy, nó xóa luôn những gì người dùng đưa lên bằng phần mềm.

1. Trên máy chạy script cũ, nhấn `Win`, gõ `Task Scheduler` rồi mở **Task Scheduler**.
2. Ở cột trái, bấm **Task Scheduler Library**.
3. Bấm lần lượt từng tác vụ trong danh sách giữa, mở thẻ **Actions** bên dưới. Tác vụ cần tắt có cột **Details** chứa `sync-to-cloudflare-final.ps1`. Thường mỗi màn hình có một tác vụ riêng.
4. Bấm chuột phải vào tác vụ đó, chọn **Disable**. Cột **Status** đổi thành **Disabled**. Làm tương tự với mọi tác vụ tìm thấy.

Tắt (**Disable**) thay vì xóa, để còn bật lại được nếu cần quay về cách cũ.

<details>

<summary>Tìm và tắt bằng PowerShell</summary>

Mở **Windows PowerShell** trên máy chạy script cũ. Lệnh sau liệt kê mọi tác vụ chạy script đồng bộ cũ, kể cả tác vụ nằm trong thư mục con:

```powershell
Get-ScheduledTask | Where-Object { "$($_.Actions.Execute) $($_.Actions.Arguments)" -like '*sync-to-cloudflare*' } |
  Select-Object TaskPath, TaskName, State
```

Thấy đúng các tác vụ cần tắt thì chạy lệnh sau. Cột **State** của chúng chuyển thành `Disabled`.

```powershell
Get-ScheduledTask | Where-Object { "$($_.Actions.Execute) $($_.Actions.Arguments)" -like '*sync-to-cloudflare*' } |
  Disable-ScheduledTask
```

</details>
{% endstep %}

{% step %}
### Cài phần mềm cho người dùng

Trên máy của người sẽ đưa nội dung lên TV, đăng nhập bằng tài khoản Windows của chính người đó, rồi làm theo [Cài đặt](../bat-dau/cai-dat.md) đến hết bước **Nhập mã API**.

Phần mềm cài riêng cho từng tài khoản Windows, và mã API được mã hóa theo tài khoản đó. Cài bằng tài khoản IT thì người dùng sẽ không thấy phần mềm.
{% endstep %}

{% step %}
### Thử với một ảnh

1. Trong phần mềm, bấm **TV 1**, kéo một ảnh JPG vào cửa sổ, rồi bấm **Đưa lên TV 1 (1 file)**. Ảnh hiện trong danh sách **Đang chiếu trên TV 1**.
2. Nhìn TV 1. Ảnh hiện ra sau khi TV chiếu hết vòng hiện tại.
3. Bấm **Xóa** ở dòng của ảnh thử, rồi bấm **Yes** (Windows tiếng Việt: **Có**) để gỡ ảnh khỏi TV.
{% endstep %}

{% step %}
### Dọn file nén dở còn sót

Script nén video cũ ghi ra một file tạm có `.tmp` trong tên, và script đồng bộ đôi khi đưa luôn file đang nén dở đó lên TV. File như vậy có thể chiếu lỗi hoặc làm TV đứng hình.

1. Bấm **TV 1**, xem danh sách **Đang chiếu trên TV 1**. Tìm dòng có `.tmp` trong tên, ví dụ `video/TVNews09.tmp.mp4` (đang có trên TV 2).
2. Bấm **Xóa** ở dòng đó rồi bấm **Yes**. Bản đầy đủ cùng tên, không có `.tmp`, vẫn ở lại.
3. Bấm **TV 2** và làm tương tự.
{% endstep %}
{% endstepper %}

{% hint style="success" %}
**Kết quả:** từ giờ người dùng đưa nội dung lên TV bằng phần mềm. Thư mục OneDrive `Signage-VienBa` không còn được dùng: bỏ file vào đó sẽ không lên TV nữa, hãy báo người dùng điều này. Nội dung đang chiếu từ hệ thống cũ vẫn ở trên TV và xóa được trong phần mềm.
{% endhint %}

Gửi người dùng bộ hướng dẫn này, bắt đầu từ trang [Bắt đầu nhanh](../bat-dau/bat-dau-nhanh.md).

## Lỗi thường gặp

<details>

<summary>Ảnh, video vừa đưa lên tự biến mất khỏi TV sau một lúc</summary>

**Nguyên nhân:** vẫn còn một lịch đồng bộ cũ đang chạy, có thể trên một máy khác cũng có thư mục OneDrive `Signage-VienBa`.

**Cách khắc phục:** làm lại bước **Tắt lịch đồng bộ cũ** trên mọi máy từng cài script cũ, rồi đưa lại file bị mất.

</details>

<details>

<summary>Access is denied khi tắt tác vụ</summary>

**Nguyên nhân:** tác vụ được tạo bằng tài khoản quản trị, hoặc đặt chạy với quyền cao nhất.

**Cách khắc phục:** bấm chuột phải vào **Task Scheduler** hoặc **Windows PowerShell**, chọn **Run as administrator**, rồi tắt lại.

</details>

<details>

<summary>Không thấy tác vụ nào chạy sync-to-cloudflare-final.ps1</summary>

**Nguyên nhân:** tác vụ nằm trong thư mục con của **Task Scheduler Library**, hoặc chạy trên một máy khác.

**Cách khắc phục:** dùng lệnh PowerShell ở bước 1, lệnh này tìm cả trong thư mục con. Vẫn không thấy thì kiểm tra các máy khác có thư mục OneDrive `Signage-VienBa`.

</details>
