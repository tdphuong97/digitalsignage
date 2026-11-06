---
description: Mã API là gì, nằm ở đâu, cách đổi mã và cách lấy lại mã đã lưu trên một máy.
---

# Mã API

**Ai làm:** bộ phận IT

**Mã API** là mật khẩu riêng của hệ thống màn hình. Mỗi lần đưa file lên hoặc xóa file, phần mềm gửi kèm mã này. Máy chủ so với mã nó đang giữ: khớp thì cho làm, sai thì từ chối với lỗi **Mã API không đúng. Vào Cài đặt để nhập lại.** Xem danh sách đang chiếu thì không cần mã.

Mã nằm ở hai nơi và phải giống nhau:

| Nơi | Tên ở đó |
| --- | --- |
| Cloudflare: Worker **signage** > **Settings** > **Variables and Secrets** | Dòng `API_KEY`, loại **Secret**, giá trị bị ẩn |
| Phần mềm Signage Viên Ba trên từng máy: **Cài đặt** | Ô **Mã API** |

{% hint style="warning" %}
Đừng nhầm với **API Tokens**, **Global API Key** hay **Account API Tokens** trong hồ sơ tài khoản Cloudflare. Đó là khóa quản lý cả tài khoản Cloudflare, không liên quan đến hệ thống màn hình và không cần đụng tới.
{% endhint %}

## Đổi mã API

Nên đổi khi mã bị lộ, khi có người phụ trách nghỉ việc, hoặc khi mã quá dễ đoán.

{% stepper %}
{% step %}
### Tạo mã mới

Mở PowerShell, dán lệnh sau rồi nhấn `Enter`. Lệnh in ra một mã 48 ký tự ngẫu nhiên và chép sẵn vào clipboard.

```powershell
$b = New-Object byte[] 24; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
$key = -join ($b | ForEach-Object { $_.ToString('x2') }); Set-Clipboard $key; $key
```

Lưu mã vào trình quản lý mật khẩu ngay. Cloudflare không cho xem lại mã sau khi lưu.
{% endstep %}

{% step %}
### Đổi mã trên Cloudflare

{% tabs %}
{% tab title="Trang web Cloudflare" %}
1. Đăng nhập [dash.cloudflare.com](https://dash.cloudflare.com) bằng tài khoản quản lý Worker **signage**.
2. Ở menu trái, chọn **Workers & Pages** (có giao diện ghi **Compute (Workers)**), rồi bấm Worker **signage**.
3. Mở thẻ **Settings**, kéo xuống mục **Variables and Secrets**.
4. Ở dòng **API_KEY**, bấm biểu tượng sửa, dán mã mới, rồi bấm **Deploy**.
{% endtab %}

{% tab title="Dòng lệnh wrangler" %}
Trong thư mục mã nguồn Worker:

```powershell
npx wrangler@4 login               # mở trình duyệt để đăng nhập Cloudflare
npx wrangler@4 secret put API_KEY  # dán mã mới khi được hỏi, rồi Enter
```

Lệnh `secret put` áp dụng ngay lên Worker đang chạy, không cần triển khai lại.
{% endtab %}
{% endtabs %}

Từ lúc này, mã cũ hết hiệu lực.
{% endstep %}

{% step %}
### Nhập mã mới trên từng máy

Trên mỗi máy dùng phần mềm, bấm **Cài đặt**, dán mã mới vào ô **Mã API**, bấm **Kiểm tra kết nối**. Thấy **Kết nối được, mã API đúng.** thì bấm **Lưu**.
{% endstep %}
{% endstepper %}

{% hint style="danger" %}
Làm bước 3 ngay sau bước 2. Trong khoảng giữa, không máy nào đưa file lên hay xóa file được. TV vẫn chiếu bình thường.
{% endhint %}

## Lấy lại mã đã lưu trên một máy

Cloudflare không cho xem lại giá trị `API_KEY`. Nếu quên mã nhưng còn một máy đã lưu mã đúng, mở PowerShell **trên đúng máy đó, bằng đúng tài khoản Windows đó** và chạy:

```powershell
Add-Type -AssemblyName System.Security
$s = Get-Content "$env:APPDATA\SignageVienBa\settings.json" -Raw | ConvertFrom-Json
[Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String($s.ApiKeyProtected), [Text.Encoding]::UTF8.GetBytes('SignageVienBa.v1'), 'CurrentUser'))
```

Lệnh in ra mã. Trên máy khác hoặc tài khoản khác, lệnh báo lỗi, vì mã được mã hóa bằng Windows (DPAPI) riêng cho tài khoản đó. Không còn máy nào giữ mã thì tạo mã mới theo phần **Đổi mã API** ở trên.
